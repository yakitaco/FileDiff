using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileDiff
{
    public class FileComparer
    {
        // 比較条件のフラグ
        public bool CheckSize { get; set; }
        public bool CheckTimestamp { get; set; }
        public bool CheckHash { get; set; }

        public string HashAlgorithmName { get; set; } = "MD5";

        public int Compare(string sourceDir, string targetDir,
                           Action<int, int> progressCallback,
                           Action<string> logCallback,
                           CancellationToken ct)
        {

            int diffCount = 0;
            var sourceFiles = GetAllFiles(sourceDir);
            int total = sourceFiles.Count;
            int processed = 0;

            // ディレクトリパスの末尾を安全に処理
            if (!sourceDir.EndsWith(Path.DirectorySeparatorChar.ToString())) sourceDir += Path.DirectorySeparatorChar;
            if (!targetDir.EndsWith(Path.DirectorySeparatorChar.ToString())) targetDir += Path.DirectorySeparatorChar;

            // 1. ソース(Dir1)を基準に比較
            Parallel.ForEach(sourceFiles, new ParallelOptions { CancellationToken = ct }, (file, state) =>
            {
                if (ct.IsCancellationRequested) state.Stop();

                // キャンセル信号を受け取った場合はループを抜ける
                ct.ThrowIfCancellationRequested();

                int currentProgress = Interlocked.Increment(ref processed);
                if (currentProgress % 10 == 0 || currentProgress == total)
                {
                    progressCallback?.Invoke(currentProgress, total);
                }

                string relativePath = file.Substring(sourceDir.Length);
                string targetPath = Path.Combine(targetDir, relativePath);

                // 存在チェック
                if (!File.Exists(targetPath))
                {
                    Interlocked.Increment(ref diffCount);
                    logCallback?.Invoke($"{targetPath} : Not Exist");
                    return;
                }

                var f1 = new FileInfo(file);
                var f2 = new FileInfo(targetPath);

                // ファイルサイズ比較
                if (CheckSize && f1.Length != f2.Length)
                {
                    Interlocked.Increment(ref diffCount);
                    logCallback?.Invoke($"{targetPath} : Size Diff");
                    return;
                }

                // タイムスタンプ比較
                if (CheckTimestamp && f1.LastWriteTime != f2.LastWriteTime)
                {
                    Interlocked.Increment(ref diffCount);
                    logCallback?.Invoke($"{targetPath} : Time Diff");
                    return;
                }

                // ハッシュ比較
                if (CheckHash)
                {
                    try
                    {
                        if (ComputeHash(file) != ComputeHash(targetPath))
                        {
                            Interlocked.Increment(ref diffCount);
                            logCallback?.Invoke($"{targetPath} : Hash Diff");
                        }
                    } catch (OperationCanceledException)
                    {
                        // キャンセルされた場合そのまま上に投げる
                        throw;
                    } catch (Exception ex)
                    {
                        logCallback?.Invoke($"{targetPath} : Hash Error ({ex.Message})");
                    }
                }
            });

            // 2. ターゲット(Dir2)にしかないファイルをチェック
            var targetFiles = GetAllFiles(targetDir);
            foreach (var file in targetFiles)
            {
                ct.ThrowIfCancellationRequested();

                string relativePath = file.Substring(targetDir.Length);
                string sourcePath = Path.Combine(sourceDir, relativePath);

                if (!File.Exists(sourcePath))
                {
                    Interlocked.Increment(ref diffCount);
                    logCallback?.Invoke($"{sourcePath} : Not Exist");
                }
            }

            return diffCount;
        }

        // --- ハッシュ計算エンジン ---
        private string ComputeHash(string path)
        {
            // アルゴリズム名に基づいてプロバイダーを生成
            using (HashAlgorithm algorithm = CreateHashAlgorithm(HashAlgorithmName))
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] hashBytes = algorithm.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).ToLower().Replace("-", "");
            }
        }

        private HashAlgorithm CreateHashAlgorithm(string name)
        {
            switch (name.ToUpper())
            {
                case "SHA-1":
                case "SHA1":
                    return SHA1.Create();
                case "SHA-256":
                case "SHA256":
                    return SHA256.Create();
                case "MD5":
                default:
                    return MD5.Create();
            }
        }

        private List<string> GetAllFiles(string path)
        {
            var list = new List<string>();
            try
            {
                list.AddRange(Directory.GetFiles(path));
                foreach (var d in Directory.GetDirectories(path))
                {
                    list.AddRange(GetAllFiles(d));
                }
            } catch (UnauthorizedAccessException) { }
            return list;
        }

        // --- 1. ハッシュリストの作成 ---
        public void CreateHashList(string targetDir, string savePath, Action<int, int> progressCallback, CancellationToken ct)
        {
            var files = GetAllFiles(targetDir);
            int total = files.Count;
            int processed = 0;

            if (!targetDir.EndsWith(Path.DirectorySeparatorChar.ToString())) targetDir += Path.DirectorySeparatorChar;

            using (var sw = new StreamWriter(savePath, false, Encoding.UTF8))
            {
                // ヘッダー: 相対パス, ハッシュ, アルゴリズム
                sw.WriteLine("RelativePath,Hash,Algorithm");

                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();

                    string relativePath = file.Substring(targetDir.Length);
                    string hash = ComputeHash(file);
                    sw.WriteLine($"{EscapeCsv(relativePath)},{hash},{HashAlgorithmName}");

                    processed++;
                    progressCallback?.Invoke(processed, total);
                }
            }
        }

        // --- 2. ハッシュリストとの照合 ---
        public int CompareWithHashList(string targetDir, string listPath, Action<int, int> progressCallback, Action<string> logCallback, CancellationToken ct)
        {
            int diffCount = 0;
            var lines = File.ReadAllLines(listPath, Encoding.UTF8);
            int total = lines.Length - 1; // ヘッダー除く
            int processed = 0;

            if (!targetDir.EndsWith(Path.DirectorySeparatorChar.ToString())) targetDir += Path.DirectorySeparatorChar;

            for (int i = 1; i < lines.Length; i++)
            { // 1行目はヘッダー
                ct.ThrowIfCancellationRequested();
                var parts = ParseCsv(lines[i]);
                if (parts.Length < 3) continue;

                string relativePath = parts[0];
                string expectedHash = parts[1];
                string algo = parts[2]; // 将来的にここでアルゴリズムを切り替える

                string fullPath = Path.Combine(targetDir, relativePath);

                if (!File.Exists(fullPath))
                {
                    diffCount++;
                    logCallback?.Invoke($"{relativePath} : ファイルが存在しません");
                } else
                {
                    string actualHash = ComputeHash(fullPath);
                    if (actualHash != expectedHash)
                    {
                        diffCount++;
                        logCallback?.Invoke($"{relativePath} : ハッシュ不一致");
                    }
                }

                processed++;
                progressCallback?.Invoke(processed, total);
            }
            return diffCount;
        }

        private string EscapeCsv(string s) => s.Contains(",") ? $"\"{s}\"" : s;
        private string[] ParseCsv(string line) => line.Split(','); // 簡易的な分割

    }
}