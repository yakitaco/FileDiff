using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileDiff {
    public partial class Form1 : Form {
        static readonly HashAlgorithm hashProvider = new MD5CryptoServiceProvider();
        public static Form1 instance;
        bool doflag = false;
        public Form1() {
            instance = this;
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e) {
            //FolderBrowserDialogクラスのインスタンスを作成
            FolderBrowserDialog fbd = new FolderBrowserDialog();

            //上部に表示する説明テキストを指定する
            fbd.Description = "フォルダを指定してください。";
            //ルートフォルダを指定する
            //デフォルトでDesktop
            fbd.RootFolder = Environment.SpecialFolder.Desktop;
            //最初に選択するフォルダを指定する
            //RootFolder以下にあるフォルダである必要がある
            fbd.SelectedPath = @"C:\Windows";
            //ユーザーが新しいフォルダを作成できるようにする
            //デフォルトでTrue
            fbd.ShowNewFolderButton = true;

            //ダイアログを表示する
            if (fbd.ShowDialog(this) == DialogResult.OK) {
                //選択されたフォルダを表示する
                Console.WriteLine(fbd.SelectedPath);
                textBox1.Text = fbd.SelectedPath;
            }
        }

        private void button2_Click(object sender, EventArgs e) {
            //FolderBrowserDialogクラスのインスタンスを作成
            FolderBrowserDialog fbd = new FolderBrowserDialog();

            //上部に表示する説明テキストを指定する
            fbd.Description = "フォルダを指定してください。";
            //ルートフォルダを指定する
            //デフォルトでDesktop
            fbd.RootFolder = Environment.SpecialFolder.Desktop;
            //最初に選択するフォルダを指定する
            //RootFolder以下にあるフォルダである必要がある
            fbd.SelectedPath = @"C:\Windows";
            //ユーザーが新しいフォルダを作成できるようにする
            //デフォルトでTrue
            fbd.ShowNewFolderButton = true;

            //ダイアログを表示する
            if (fbd.ShowDialog(this) == DialogResult.OK) {
                //選択されたフォルダを表示する
                Console.WriteLine(fbd.SelectedPath);
                textBox2.Text = fbd.SelectedPath;
            }
        }

        private void button3_Click(object sender, EventArgs e) {
            if (doflag == false) {
                //処理が行われているときは、何もしない
                if (backgroundWorker1.IsBusy) return;
                button3.Text = "Stop";
                textBox3.Clear();
                //Program.diffFiles(textBox1.Text, textBox2.Text, checkBox1.Checked, checkBox2.Checked, checkBox3.Checked);

                //BackgroundWorkerのProgressChangedイベントが発生するようにする
                backgroundWorker1.WorkerReportsProgress = true;
                //キャンセルできるようにする
                backgroundWorker1.WorkerSupportsCancellation = true;

                backgroundWorker1.RunWorkerAsync();

                doflag = true;
            } else {
                //Cancel
                backgroundWorker1.CancelAsync();
                button3.Text = "Start";
                doflag = false;
            }
        }

        public static void _setText(string text){
            instance.setText(text);
        }

        void setText(string text) {
            textBox3.AppendText(text + Environment.NewLine);
        }

        public static void _setProgress(int val, int max) {
            instance.setProgress(val, max);
        }

        void setProgress(int val, int max) {
            progressBar1.Minimum = 0;
            progressBar1.Maximum = max;
            progressBar1.Value = val;
            label1.Text = val + "/" + max;
        }

        private void BackgroundWorker1_DoWork(object sender, DoWorkEventArgs e) {
            BackgroundWorker bgWorker = (BackgroundWorker)sender;

            string oDir = textBox1.Text;
            string dDir = textBox2.Text;
            bool s1 = checkBox1.Checked;
            bool s2 = checkBox2.Checked;
            bool s3 = checkBox3.Checked;

            string[] names = Directory.GetFiles(@oDir, "*", SearchOption.AllDirectories);
            int cnt = 0;
            int diff = 0;

            bgWorker.ReportProgress(cnt, new object[] { names.Length, "" });

            foreach (string n in names) {
                cnt++;

                //キャンセルされたか調べる
                if (bgWorker.CancellationPending) {
                    //キャンセルされたとき
                    e.Cancel = true;
                    return;
                }

                var f = new FileInfo(n);
                f.Refresh();
                Debug.WriteLine(n + " : " + f.Length);
                string d = @dDir + n.Substring(oDir.Length);
                var df = new FileInfo(d);
                if (System.IO.File.Exists(d)) { // ファイル存在

                } else {
                    Debug.WriteLine(d + " : Not Exist");
                    bgWorker.ReportProgress(cnt, new object[] { names.Length, d + " : Not Exist" });
                    //Form1._setText(d + " : Not Exist");
                    diff++;
                    continue;
                }
                if (s1) { // ファイルサイズ
                    if (f.Length == df.Length) {

                    } else {
                        Debug.WriteLine(d + " : Size Diff");
                        bgWorker.ReportProgress(cnt, new object[] { names.Length, d + " : Size Diff" });
                        //Form1._setText(d + " : Size Diff");
                        diff++;
                        continue;
                    }
                }
                if (s2) { // タイムスタンプ
                    if (f.LastWriteTime == df.LastWriteTime) {

                    } else {
                        Debug.WriteLine(d + " : Time Diff");
                        bgWorker.ReportProgress(cnt, new object[] { names.Length, d + " : Time Diff" });
                        //Form1._setText(d + " : Time Diff");
                        diff++;
                        continue;
                    }
                }
                if (s3) { // ハッシュ
                    if (ComputeFileHash(n) == ComputeFileHash(d)) {

                    } else {
                        Debug.WriteLine(d + " : Hash Diff");
                        bgWorker.ReportProgress(cnt, new object[] { names.Length, d + " : Hash Diff" });
                        //Form1._setText(d + " : Hash Diff");
                        diff++;
                        continue;
                    }
                }
                bgWorker.ReportProgress(cnt, new object[] { names.Length, "" });//差分なし
            }



            // 反対側(存在のみチェック)
            string[] names2 = Directory.GetFiles(@dDir, "*", SearchOption.AllDirectories);
            foreach (string n in names2) {

                //キャンセルされたか調べる
                if (bgWorker.CancellationPending) {
                    //キャンセルされたとき
                    e.Cancel = true;
                    return;
                }

                var f = new FileInfo(n);
                f.Refresh();
                Debug.WriteLine(n + " : " + f.Length);
                string o = @oDir + n.Substring(dDir.Length);
                if (System.IO.File.Exists(o)) { // ファイル存在

                } else {
                    Debug.WriteLine(o + " : Not Exist");
                    bgWorker.ReportProgress(names.Length, new object[] { names.Length, o + " : Not Exist" });
                    diff++;
                }
            }

            e.Result = diff;

        }

        private void BackgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e) {

            object[] o = (object[])e.UserState;
            progressBar1.Value = e.ProgressPercentage;
            progressBar1.Maximum = (int)o[0];
            string str = (string)o[1];
            if (str?.Length > 0) setText(str);
            label1.Text = progressBar1.Value + "/" + progressBar1.Maximum;
            label1.Update();
        }

        private void BackgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            if (e.Error != null) {
                //エラーが発生したとき
                MessageBox.Show("エラー : " + e.Error.Message,
                "エラー",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            } else if (e.Cancelled) {
                //キャンセルされたとき
                MessageBox.Show("中断しました",
                "中断",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            } else {
                //正常に終了したとき
                //結果を取得する
                int result = (int)e.Result;
                MessageBox.Show("完了しました[差分 : " + result + " ]",
                "終了",
                MessageBoxButtons.OK,
                MessageBoxIcon.Asterisk);
            }

            button3.Text = "Start";
            doflag = false;

        }

        public static string ComputeFileHash(string filePath) {
            var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var bs = hashProvider.ComputeHash(fs);
            return BitConverter.ToString(bs).ToLower().Replace("-", "");
        }

    }


}
