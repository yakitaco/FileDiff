using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
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
            foreach (string n in names) {
                var f = new FileInfo(n);
                f.Refresh();
                Debug.WriteLine( n + " : " + f.Length);
                string d = @dDir + n.Substring(oDir.Length);
                if (System.IO.File.Exists(d)) { // ファイル存在

                } else {
                    Debug.WriteLine( d + " : Not Exist");
                    continue;
                }
                if ((s1) && (f.Length == d.Length)) { // ファイルサイズ

                } else {
                    Debug.WriteLine(d + " : Size Diff");
                    continue;
                }
                if ((s2) && (f.Length == d.Length)) { // タイムスタンプ

                } else {
                    Debug.WriteLine(d + " : Time Diff");
                    continue;
                }
                if ((s3) && (ComputeFileHash(f.FullName) == ComputeFileHash(d))) { // ハッシュ

                } else {
                    Debug.WriteLine(d + " : Hash Diff");
                    continue;
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
