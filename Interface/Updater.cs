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

            var timer = new System.Windows.Forms.Timer();
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