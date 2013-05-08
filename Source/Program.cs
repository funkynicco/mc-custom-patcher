using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace MC_Custom_Updater
{
    static class Program
    {
        private static uint _executableCrc = 0;
        private static bool _loadCrc = true;
        private static object _lock = new object();
        /// <summary>
        /// Gets the crc hash of this program.
        /// </summary>
        public static uint ExecutableCrc
        {
            get
            {
                lock (_lock)
                {
                    if (_loadCrc)
                    {
                        _executableCrc = Crc32.ComputeFile(Process.GetCurrentProcess().MainModule.FileName);
                        _loadCrc = false;
                    }

                    return _executableCrc;
                }
            }
        }

        public static Random Random { get; private set; }

        static void Main()
        {
            Random = new Random();
            bool silent = false;

#if DEBUG
            //Environment.CurrentDirectory = @"C:\Users\Nicco\AppData\Roaming\.techniclauncher\voltz";
            Environment.CurrentDirectory = @"D:\Coding\C#\2013\MC_Custom_Updater\Test";
#endif // DEBUG

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                string[] args = Environment.GetCommandLineArgs();
                if (args.Length >= 3 && args[1] == "-crc")
                {
                    uint crc = Crc32.ComputeFile(args[2]);

                    MessageBox.Show(
                        args[2] + "\ncrc: " + crc + "\nhex: " + crc.ToString("x8"),
                        "MC Patcher",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    return;
                }
                else if (args.Length >= 2 && args[1] == "-makelist")
                {
                    using (var dlg = new ShowListDlg())
                    {
                        dlg.ListXml = MakeList.Create();
                        dlg.ShowDialog();
                    }
                    
                    return;
                }
                else if (args.Length >= 2 && args[1] == "-silent") // So it can be used with batching, if no update is needed itl automatically close.
                {
                    silent = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "MC Patcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            var patchList = Preloader.Load();
            if (patchList == null)
                return;

            if (patchList.FinalActions.Count > 0)
                Application.Run(new MainForm(patchList));
            else if (!silent)
                MessageBox.Show("Your copy is fully updated, nothing to do.", "MC Patcher", MessageBoxButtons.OK, MessageBoxIcon.Information);

            try
            {
                // Clean up
                if (Directory.Exists("mcpatcher_temp"))
                    Directory.Delete("mcpatcher_temp", true);
            }
            catch { }
        }
    }
}