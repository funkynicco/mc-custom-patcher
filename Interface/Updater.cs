/*
 * Copyright (C) Nicco, 2013
 * 
 * You may use, distribute or modify the following source code and all its content as long as the following tag stays.
 * 
 * Origin: http://mcvoltz.nprog.com/patch/
 * 
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MC_Custom_Updater
{
    public partial class Updater : Form
    {
        private const int WM_NCHITTEST = 0x00000084;
        private const int HTCLIENT = 0x00000001;
        private const int HTCAPTION = 0x00000002;

        private LinkedList<ProgressFile> _files = new LinkedList<ProgressFile>();
        private int _totalFiles = 0;
        private Thread _downloadThread = null;
        private object _lock = new object();
        private EventWaitHandle _forceUpdateEvent = new EventWaitHandle(false, EventResetMode.AutoReset);

        public Updater(IEnumerable<PatchFile> files)
        {
            Directory.CreateDirectory("mcpatcher_temp");
            foreach (var file in files)
                _files.AddLast(new ProgressFile(file));

            _totalFiles = _files.Count;

            InitializeComponent();

            Shown += Updater_Shown;
        }

        void Updater_Shown(object sender, EventArgs e)
        {
            try
            {
                UpdateState();
            }
            catch (NothingToDoException)
            {
                return;
            }

            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Tick += (_sender, _e) =>
                {
                    try
                    {
                        UpdateState();
                    }
                    catch (NothingToDoException)
                    {
                    }
                };
            timer.Interval = 100;
            timer.Enabled = true;

            _downloadThread = new Thread(DownloadThread);
            _downloadThread.Start();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Close();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                if ((int)m.Result == HTCLIENT)
                {
                    m.Result = (IntPtr)HTCAPTION;
                    return;
                }
            }

            base.WndProc(ref m);
        }

        void DownloadThread()
        {
            ProgressFile file = null;
            do
            {
                lock (_lock)
                {
                    if (_files.Count > 0)
                        file = _files.First();
                }

                _forceUpdateEvent.Set();
                file.DownloadThread();

                lock (_lock)
                {
                    _files.RemoveFirst();
                    if (_files.Count == 0)
                        break;
                }
            }
            while (file != null);
        }

        int nextUpdateText = 0;
        void UpdateState()
        {
            lock (_lock)
            {
                if (_files.Count > 0)
                {
                    int filenum = _totalFiles - _files.Count;
                    double percent = (double)filenum / _totalFiles * 100.0;

                    ProgressFile file = _files.First();
                    if (Environment.TickCount >= nextUpdateText || _forceUpdateEvent.WaitOne(0))
                    {
                        label2.Text = file.PatchFile.RelativePhysicalPath + "\r\n" +
                            Enum.GetName(typeof(UpdateStatus), file.Status) + "\r\n" +
                            file.AdvancedProgress + "\r\n" +
                            file.Speed.GetSize() + "/s\r\n" +
                            string.Format("{0} / {1} ({2:0.00}%)", filenum, _totalFiles, percent);
                        nextUpdateText = Environment.TickCount + 1000;
                    }

                    progressBar1.Value = Math.Min(progressBar1.Maximum, (int)(file.Progress * 100.0f));
                    progressBar2.Value = Math.Min(progressBar2.Maximum, (int)(percent * 100.0));
                }
                else
                {
                    if (_downloadThread != null)
                        _downloadThread.Join();
                    Close();
                    
                    throw new NothingToDoException();
                }
            }
        }
    }

    class ProgressFile
    {
        /// <summary>
        /// Gets the current status.
        /// </summary>
        public UpdateStatus Status { get; private set; }

        public PatchFile PatchFile { get; private set; }

        /// <summary>
        /// Gets the temporary file path.
        /// </summary>
        public string TempFile { get; private set; }

        private long
            _position = 0,
            _size = 0,
            _lastPos = 0,
            _speed = 0;
        private object _lock = new object();
        
        /// <summary>
        /// Gets the current downloaded bytes.
        /// </summary>
        public long Position { get { return _position; } }

        /// <summary>
        /// Gets the file size.
        /// </summary>
        public long Size { get { return _size; } }

        /// <summary>
        /// Gets the speed in bytes per second of the download.
        /// </summary>
        public long Speed { get { return _speed; } }


        /// <summary>
        /// Gets the percentual progress of the download.
        /// </summary>
        public double Progress
        {
            get
            {
                lock (_lock)
                {
                    if (Size == 0)
                        return 0.0;

                    return (double)Position / Size * 100;
                }
            }
        }

        /// <summary>
        /// Gets a more advanced progress in the format as follows: pos / size (percent)
        /// </summary>
        public string AdvancedProgress
        {
            get
            {
                lock (_lock)
                {
                    return Utilities.GetProgress(_position, _size, ProgressType.Size);
                }
            }
        }

        public ProgressFile(PatchFile patchfile)
        {
            Status = UpdateStatus.Idle;
            PatchFile = patchfile;
            do
            {
                TempFile = "mcpatcher_temp/" + Program.Random.Next() + ".tmp";
            }
            while (File.Exists(TempFile));
        }

        public void DownloadThread()
        {
            _speed = _position = _lastPos = 0;
            int nextUpdateSpeed = Environment.TickCount + 1000;
            Status = UpdateStatus.Connecting;
            var request = HttpWebRequest.Create(PatchFile.PatchUrl);
            try
            {
                using (var response = request.GetResponse())
                {
                    lock (_lock)
                    {
                        Status = UpdateStatus.Downloading;
                        _size = response.ContentLength;
                    }

                    using (var fs = new FileStream(TempFile, FileMode.Create, FileAccess.Write))
                    using (var stream = response.GetResponseStream())
                    {
                        byte[] buffer = new byte[65536];
                        while (_position < _size)
                        {
                            int read = stream.Read(buffer, 0, buffer.Length);
                            fs.Write(buffer, 0, read);

                            _position += read;

                            if (Environment.TickCount >= nextUpdateSpeed)
                            {
                                _speed = _position - _lastPos;
                                _lastPos = _position;
                                nextUpdateSpeed = Environment.TickCount + 1000;
                            }
                        }
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(PatchFile.RelativePhysicalPath));
                    File.Copy(TempFile, PatchFile.RelativePhysicalPath, true);
                    try { File.Delete(TempFile); }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (File.Exists(TempFile))
                        File.Delete(TempFile);
                }
                catch { }

                Debug.Write(ex.StackTrace);
                MessageBox.Show(
                    "An error occurred while downloading " + PatchFile.RelativePhysicalPath +
                    "\nTry patch this file later at a later time.\n\nError: " + ex.Message + "\nUrl: " + PatchFile.PatchUrl,
                    "MC Patcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    enum UpdateStatus
    {
        Connecting,
        Downloading,
        Idle
    }

    class NothingToDoException : Exception
    {
        public NothingToDoException()
            : base("Nothing to do ...")
        {
        }
    }
}