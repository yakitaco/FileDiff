using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace FileDiff {
    static class Program {
        static readonly HashAlgorithm hashProvider = new MD5CryptoServiceProvider();

        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }

        public static void diffFiles(string oDir, string dDir, bool s1, bool s2, bool s3) {
            string[] names = Directory.GetFiles(@oDir, "*", SearchOption.AllDirectories);
            int cnt = 0;
            foreach (string n in names) {
                cnt++;
                Form1._setProgress(cnt, names.Length);
                var f = new FileInfo(n);
                f.Refresh();
                Debug.WriteLine(n + " : " + f.Length);
                string d = @dDir + n.Substring(oDir.Length);
                var df = new FileInfo(d);
                if (System.IO.File.Exists(d)) { // ファイル存在

                } else {
                    Debug.WriteLine(d + " : Not Exist");
                    Form1._setText(d + " : Not Exist");
                    continue;
                }
                if (s1) { // ファイルサイズ
                    if (f.Length == df.Length) {

                    } else {
                        Debug.WriteLine(d + " : Size Diff");
                        Form1._setText(d + " : Size Diff");
                        continue;
                    }
                }
                if (s2) { // タイムスタンプ
                    if (f.LastWriteTime == df.LastWriteTime) {

                    } else {
                        Debug.WriteLine(d + " : Time Diff");
                        Form1._setText(d + " : Time Diff");
                        continue;
                    }
                }
                if (s3) { // ハッシュ
                    if (ComputeFileHash(n) == ComputeFileHash(d)) {

                    } else {
                        Debug.WriteLine(d + " : Hash Diff");
                        Form1._setText(d + " : Hash Diff");
                        continue;
                    }
                }
            }

            // 反対側(存在のみチェック)
            string[] names2 = Directory.GetFiles(@dDir, "*", SearchOption.AllDirectories);
            foreach (string n in names2) {
                var f = new FileInfo(n);
                f.Refresh();
                Debug.WriteLine(n + " : " + f.Length);
                string o = @oDir + n.Substring(dDir.Length);
                if (System.IO.File.Exists(o)) { // ファイル存在

                } else {
                    Debug.WriteLine(o + " : Not Exist");
                }
            }
        }

        public static string ComputeFileHash(string filePath) {
            var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var bs = hashProvider.ComputeHash(fs);
            return BitConverter.ToString(bs).ToLower().Replace("-", "");
        }

    }
}
