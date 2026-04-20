using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.Threading;

namespace FileDiff
{
    public partial class Form1 : Form
    {
        // 動作モードの定義
        enum WorkMode { CompareFolders, CreateList, VerifyList }
        WorkMode currentMode;

        bool isRunning = false;

        // キャンセル信号を発行するためのオブジェクト
        CancellationTokenSource cts;

        public Form1()
        {
            InitializeComponent();
        }

        // フォルダ選択処理
        private void SelectFolder(TextBox textBox)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "フォルダを指定してください。";
                fbd.RootFolder = Environment.SpecialFolder.Desktop;
                fbd.ShowNewFolderButton = true;

                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    textBox.Text = fbd.SelectedPath;
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SelectFolder(textBox1);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SelectFolder(textBox2);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (!isRunning)
            {
                // ビジー状態なら実行しない
                if (backgroundWorker1.IsBusy) return;

                if (string.IsNullOrWhiteSpace(textBox1.Text) || string.IsNullOrWhiteSpace(textBox2.Text))
                {
                    MessageBox.Show("比較するフォルダを指定してください。");
                    return;
                }

                textBox3.Clear();
                button3.Text = "Stop";
                label1.Text = "-/-";
                progressBar1.Value = 0;
                isRunning = true;

                // キャンセルトークンを作成
                cts = new CancellationTokenSource();

                // UIスレッド上でチェックボックスなどの状態を取得し、設定用オブジェクトにまとめる
                var options = new ComparisonOptions
                {
                    SourceDir = textBox1.Text,
                    TargetDir = textBox2.Text,
                    CheckSize = checkBox1.Checked,
                    CheckTimestamp = checkBox2.Checked,
                    CheckHash = checkBox3.Checked
                };

                backgroundWorker1.WorkerReportsProgress = true;
                backgroundWorker1.WorkerSupportsCancellation = true;

                // 引数として設定用オブジェクトを渡す
                backgroundWorker1.RunWorkerAsync(options);
            } else
            {
                backgroundWorker1.CancelAsync();

                // 並列処理にストップの信号を送る
                cts.Cancel();

                button3.Text = "Stopping...";
                button3.Enabled = false;
            }
        }

        // リスト作成ボタン
        private void button4_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBox1.Text))
            {
                MessageBox.Show("対象フォルダを選択してください。");
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSVファイル(*.csv)|*.csv|テキストファイル(*.txt)|*.txt";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    StartWork(WorkMode.CreateList, sfd.FileName);
                }
            }

            backgroundWorker1.WorkerReportsProgress = true;
            backgroundWorker1.WorkerSupportsCancellation = true;

        }

        // リスト照合ボタン
        private void button5_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBox1.Text))
            {
                MessageBox.Show("対象フォルダを選択してください。");
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "CSVファイル(*.csv)|*.csv|テキストファイル(*.txt)|*.txt";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    StartWork(WorkMode.VerifyList, ofd.FileName);
                }
            }

            backgroundWorker1.WorkerReportsProgress = true;
            backgroundWorker1.WorkerSupportsCancellation = true;

        }

        private void StartWork(WorkMode mode, string extraPath = "")
        {
            currentMode = mode;
            textBox3.Clear();
            button3.Text = "Stop";
            isRunning = true;
            cts = new CancellationTokenSource();

            var options = new ComparisonOptions
            {
                SourceDir = textBox1.Text,
                TargetDir = textBox2.Text,
                ListPath = extraPath, // 新設プロパティ
                CheckHash = true // リスト系はハッシュ必須
            };

            backgroundWorker1.RunWorkerAsync(options);
        }

        private void BackgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            var bg = (BackgroundWorker)sender;

            // 渡された設定オブジェクトを受け取る
            var options = (ComparisonOptions)e.Argument;

            // ロジッククラスに設定を反映
            var comparer = new FileComparer();

            try
            {
                if (currentMode == WorkMode.CreateList)
                {
                    comparer.CreateHashList(options.SourceDir, options.ListPath,
                        (c, t) => bg.ReportProgress(0, new int[] { c, t }), cts.Token);
                    e.Result = 0;
                } else if (currentMode == WorkMode.VerifyList)
                {
                    e.Result = comparer.CompareWithHashList(options.SourceDir, options.ListPath,
                        (c, t) => bg.ReportProgress(0, new int[] { c, t }),
                        msg => this.Invoke(new Action(() => textBox3.AppendText(msg + Environment.NewLine))), cts.Token);
                } else
                {
                    int diffs = comparer.Compare(
                        options.SourceDir,
                        options.TargetDir,
                        (curr, total) => bg.ReportProgress(0, new int[] { curr, total }), // 進捗通知
                        msg => this.Invoke(new Action(() => textBox3.AppendText(msg + Environment.NewLine))), // ログ出力
                        cts.Token
                    );

                    // キャンセル判定
                    if (bg.CancellationPending)
                    {
                        e.Cancel = true;
                        return;
                    }

                    e.Result = diffs;
                }
            } catch (Exception ex)
            {
                e.Cancel = true;
                if (bg.CancellationPending) e.Cancel = true;
                else throw new Exception("処理中にエラーが発生しました。", ex);
            }
        }

        private void BackgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            int[] status = (int[])e.UserState;
            int current = status[0];
            int max = status[1];

            if (max > 0)
            {
                progressBar1.Maximum = max;
                progressBar1.Value = Math.Min(current, max);
                label1.Text = $"{current}/{max}";
            }
        }

        private void BackgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                MessageBox.Show("中断しました", "中断", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            } else if (e.Error != null)
            {
                MessageBox.Show("エラー : " + e.Error.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } else
            {
                int result = (int)e.Result;
                MessageBox.Show($"完了しました[差分 : {result} ]", "終了", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }

            // 処理が完全に終了したこのタイミングで、ボタンの状態やフラグを初期化する
            button3.Text = "Start";
            button3.Enabled = true;
            isRunning = false;

            // キャンセル用のオブジェクトもここで破棄
            cts.Dispose();
            cts = null;
        }

        // --- パラメータ受け渡し用の内部クラス ---
        private class ComparisonOptions
        {
            public string SourceDir { get; set; }
            public string TargetDir { get; set; }
            public string ListPath { get; set; }
            public bool CheckSize { get; set; }
            public bool CheckTimestamp { get; set; }
            public bool CheckHash { get; set; }
        }

    }
}