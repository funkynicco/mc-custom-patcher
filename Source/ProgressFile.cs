using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MC_Custom_Updater
{
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
}
