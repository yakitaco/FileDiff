using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileDiff {
    public partial class Form1 : Form {
        bool doflag = false;

        public Form1() {
            InitializeComponent();
        }

        // フォルダ選択処理を共通化
        private void SelectFolder(TextBox textBox) {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog()) {
                fbd.Description = "フォルダを指定してください。";
                fbd.RootFolder = Environment.SpecialFolder.Desktop;
                fbd.ShowNewFolderButton = true;

                if (fbd.ShowDialog(this) == DialogResult.OK) {
                    textBox.Text = fbd.SelectedPath;
                }
            }
        }

        private void button1_Click(object sender, EventArgs e) {
            SelectFolder(textBox1);
        }

        private void button2_Click(object sender, EventArgs e) {
            SelectFolder(textBox2);
        }

        private void button3_Click(object sender, EventArgs e) {
            if (!doflag) {
                if (backgroundWorker1.IsBusy) return;
                
                // 入力チェック
                if (string.IsNullOrWhiteSpace(textBox1.Text) || string.IsNullOrWhiteSpace(textBox2.Text)) {
                    MessageBox.Show("比較するフォルダを指定してください。");
                    return;
                }

                button3.Text = "Stop";
                textBox3.Clear();

                backgroundWorker1.WorkerReportsProgress = true;
                backgroundWorker1.WorkerSupportsCancellation = true;

                backgroundWorker1.RunWorkerAsync();
                label1.Text = "-/-";
                progressBar1.Minimum = 0;
                progressBar1.Maximum = 100;
                progressBar1.Value = 0;
                doflag = true;
            } else {
                backgroundWorker1.CancelAsync();
                button3.Text = "Start";
                doflag = false;
            }
        }

        // 別スレッドから安全にテキストボックスに追記するためのメソッド
        private void AppendText(string text) {
            if (this.InvokeRequired) {
                this.Invoke(new Action<string>(AppendText), text);
            } else {
                textBox3.AppendText(text + Environment.NewLine);
            }
        }

        private void BackgroundWorker1_DoWork(object sender, DoWorkEventArgs e) {
            BackgroundWorker bgWorker = (BackgroundWorker)sender;

            string oDir = textBox1.Text;
            string dDir = textBox2.Text;
            
            // ディレクトリの末尾にセパレータ(\)がない場合の対策
            if (!oDir.EndsWith(Path.DirectorySeparatorChar.ToString())) oDir += Path.DirectorySeparatorChar;
            if (!dDir.EndsWith(Path.DirectorySeparatorChar.ToString())) dDir += Path.DirectorySeparatorChar;

            bool s1 = checkBox1.Checked;
            bool s2 = checkBox2.Checked;
            bool s3 = checkBox3.Checked;

            List<string> names = GetAllFiles(oDir);

            int cnt = 0;
            int diff = 0;
            int totalFiles = names.Count;

            bgWorker.ReportProgress(0, new object[] { totalFiles, "" });

            // Parallel.ForEach を使用して並列処理を簡素化・安全化
            ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
            
            try {
                Parallel.ForEach(names, options, (name, loopState) => {
                    if (bgWorker.CancellationPending) {
                        loopState.Stop();
                        return;
                    }

                    // スレッドセーフにカウントアップ
                    int currentCnt = Interlocked.Increment(ref cnt);
                    
                    // UI更新頻度を抑える（10ファイルごと、または最後）
                    if (currentCnt % 10 == 0 || currentCnt == totalFiles) {
                        bgWorker.ReportProgress(currentCnt, new object[] { totalFiles, "" });
                    }

                    FileInfo f = new FileInfo(name);
                    
                    // 相対パスを取得して比較先パスを安全に生成
                    string relativePath = name.Substring(oDir.Length);
                    string targetPath = Path.Combine(dDir, relativePath);
                    FileInfo df = new FileInfo(targetPath);

                    if (!df.Exists) {
                        Interlocked.Increment(ref diff); // スレッドセーフに差分追加
                        string msg = targetPath + " : Not Exist";
                        AppendText(msg);
                        return; 
                    }

                    if (s1 && f.Length != df.Length) {
                        Interlocked.Increment(ref diff);
                        string msg = targetPath + " : Size Diff";
                        AppendText(msg);
                        return;
                    }

                    if (s2 && f.LastWriteTime != df.LastWriteTime) {
                        Interlocked.Increment(ref diff);
                        string msg = targetPath + " : Time Diff";
                        AppendText(msg);
                        return;
                    }

                    if (s3) {
                        try {
                            if (ComputeFileHash(name) != ComputeFileHash(targetPath)) {
                                Interlocked.Increment(ref diff);
                                string msg = targetPath + " : Hash Diff";
                                AppendText(msg);
                                return;
                            }
                        } catch (Exception ex) {
                            AppendText(name + " : Hash Compute Error (" + ex.Message + ")");
                        }
                    }
                });
            } catch (OperationCanceledException) {
                e.Cancel = true;
                return;
            }

            if (bgWorker.CancellationPending) {
                e.Cancel = true;
                return;
            }

            // 反対側(存在のみチェック)
            List<string> names2 = GetAllFiles(dDir);
            foreach (string n in names2) {
                if (bgWorker.CancellationPending) {
                    e.Cancel = true;
                    return;
                }

                string relativePath = n.Substring(dDir.Length);
                string originalPath = Path.Combine(oDir, relativePath);
                
                if (!File.Exists(originalPath)) {
                    Interlocked.Increment(ref diff);
                    string msg = originalPath + " : Not Exist";
                    AppendText(msg);
                }
            }

            e.Result = diff;
        }

        private void BackgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            object[] o = (object[])e.UserState;
            int current = e.ProgressPercentage;
            int max = (int)o[0];

            if (max > 0) {
                progressBar1.Maximum = max;
                // 値が最大値を超えないように保護
                progressBar1.Value = Math.Min(current, max); 
                label1.Text = current + "/" + max;
            }
        }

        private void BackgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            if (e.Error != null) {
                MessageBox.Show("エラー : " + e.Error.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } else if (e.Cancelled) {
                MessageBox.Show("中断しました", "中断", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            } else {
                int result = (int)e.Result;
                MessageBox.Show("完了しました[差分 : " + result + " ]", "終了", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }

            button3.Text = "Start";
            doflag = false;
        }

        public static string ComputeFileHash(string filePath) {
            // usingを使用することで、不要になった瞬間にインスタンスとファイルロックを確実に解放します
            using (var hashProvider = new MD5CryptoServiceProvider())
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                var bs = hashProvider.ComputeHash(fs);
                return BitConverter.ToString(bs).ToLower().Replace("-", "");
            }
        }

        public static List<string> GetAllFiles(string dirPath) {
            List<string> lstStr = new List<string>();
            try {
                // List.AddRange を使ってコードを短縮
                lstStr.AddRange(Directory.GetFiles(dirPath));

                string[] directories = Directory.GetDirectories(dirPath);
                foreach (string d in directories) {
                    lstStr.AddRange(GetAllFiles(d));
                }
            } catch (UnauthorizedAccessException) {
                // アクセス権限がない場合は無視
            }
            return lstStr;
        }
    }
}