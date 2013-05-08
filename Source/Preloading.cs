using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace MC_Custom_Updater
{
    public class Preloader
    {
        public static PatchList Load()
        {
            var param = new ThreadParameters();

            using (var form = new PreloadForm())
            {
                form.Show();

                Thread thread = new Thread(FetchAndParseListThread);
                thread.Start(param);

                PatcherFetchState pfs = PatcherFetchState.Connecting;
                while (!param.Finished.WaitOne(100))
                {
                    if (param.State != pfs)
                    {
                        pfs = param.State;
                        form.PatchState = Enum.GetName(typeof(PatcherFetchState), pfs);
                    }
                    Application.DoEvents();
                }

                thread.Join();

                if (pfs == PatcherFetchState.Failed)
                {
                    MessageBox.Show(
                        "An error occurred while trying to parse patch list!\n\n" + param.Exception.Message,
                        "MC Patcher",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return null;
                }
            }

            return param.PatchList;
        }

        static void FetchAndParseListThread(object obj)
        {
            ThreadParameters param = (ThreadParameters)obj;

            param.State = PatcherFetchState.Connecting;

            try
            {
                var request = HttpWebRequest.Create(MainForm.PatchUrl + "/list.xml");
                using (var response = request.GetResponse())
                {
                    param.State = PatcherFetchState.Downloading;
                    using (var stream = new StreamReader(response.GetResponseStream()))
                    {
                        string data = stream.ReadToEnd();

                        param.State = PatcherFetchState.Parsing;

                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(data);

                        foreach (XmlNode node in doc.ChildNodes)
                        {
                            if (node.Name == "MCPatcher")
                            {
                                param.PatchList = PatchList.FromXml(node);
                                break;
                            }
                        }
                    }
                }

                // parse
                HashSet<string> configHashTree = GetFileTree("config");
                HashSet<string> coreModsHashTree = GetFileTree("coremods");
                HashSet<string> modsHashTree = GetFileTree("mods");

                // do a comparision against param.PatchList
                CompareHashSets(param.PatchList.IgnoredDirectories, param.PatchList.ConfigMode, configHashTree, param.PatchList.ConfigHashTree, param.PatchList.FinalActions);
                CompareHashSets(param.PatchList.IgnoredDirectories, param.PatchList.CoreModsMode, coreModsHashTree, param.PatchList.CoreModsHashTree, param.PatchList.FinalActions);
                CompareHashSets(param.PatchList.IgnoredDirectories, param.PatchList.ModsMode, modsHashTree, param.PatchList.ModsHashTree, param.PatchList.FinalActions);


                param.State = PatcherFetchState.Finished;
            }
            catch (Exception ex)
            {
                param.State = PatcherFetchState.Failed;
                param.Exception = ex;
            }

            param.Finished.Set();
        }

        public static void CompareHashSets(HashSet<string> ignoreDirectories, PatchListMode plm, HashSet<string> local, Dictionary<string, PatchFile> server, LinkedList<FileAction> finalActions)
        {
            foreach (string file in server.Keys)
            {
                if (local.Contains(file))
                {
                    if (server[file].PhysicalFileCrc != server[file].Crc)
                        finalActions.AddLast(new FileAction(FileActionResult.Update, file));
                }
                else
                    finalActions.AddLast(new FileAction(FileActionResult.Add, file));
            }

            foreach (string file in local)
            {
                if (!server.ContainsKey(file) &&
                    plm == PatchListMode.Identical)
                {
                    string directory = Path.GetDirectoryName(file);
                    if (!ignoreDirectories.Contains(directory))
                        finalActions.AddLast(new FileAction(FileActionResult.Remove, file));
                }
            }
        }

        public static HashSet<string> GetFileTree(string directory, HashSet<string> output = null)
        {
            var set = output != null ? output : new HashSet<string>();

            try
            {
                foreach (string dir in Directory.GetDirectories(directory))
                    GetFileTree(dir, set);
            }
            catch { }

            try
            {
                foreach (string file in Directory.GetFiles(directory))
                    set.Add(file.ToLower());
            }
            catch { }

            return set;
        }
    }

    public class ThreadParameters
    {
        public ManualResetEvent Finished { get; private set; }
        public PatchList PatchList { get; set; }
        public Exception Exception { get; set; }

        private PatcherFetchState _state = PatcherFetchState.Connecting;
        private object _locker = new object();
        public PatcherFetchState State
        {
            get
            {
                lock (_locker)
                    return _state;
            }
            set
            {
                lock (_locker)
                    _state = value;
            }
        }

        public ThreadParameters()
        {
            Finished = new ManualResetEvent(false);
            PatchList = null;
            Exception = null;
        }
    }

    public enum PatcherFetchState
    {
        Connecting,
        Downloading,
        Parsing,
        Finished,
        Failed
    }

    public class PatchFile
    {
        /// <summary>
        /// Parent directory where this file resides in.
        /// <para>If Directory is null, the file resides in a root folder.</para>
        /// </summary>
        public PatchDirectory Directory { get; private set; }
        /// <summary>
        /// Name of file.
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// CRC of latest file.
        /// </summary>
        public uint Crc { get; private set; }
        /// <summary>
        /// Gets the patch url to use when updating this file.
        /// </summary>
        public string PatchUrl { get; private set; }

        /// <summary>
        /// Returns the relative physical path combined by walking the directory tree up until the root folder.
        /// </summary>
        public string RelativePhysicalPath
        {
            get
            {
                if (Directory != null)
                    return Directory.RelativePhysicalPath + "\\" + Name;

                return Name;
            }
        }

        private uint _fileCrc32 = 0;
        private bool _loadFileCrc = true;
        /// <summary>
        /// Gets the CRC32 hash of the unpatched file on disk.
        /// </summary>
        public uint PhysicalFileCrc
        {
            get
            {
                try
                {
                    if (_loadFileCrc)
                    {
                        _fileCrc32 = Crc32.ComputeFile(RelativePhysicalPath);
                        _loadFileCrc = false;
                    }
                }
                catch { } // Try again later ...

                return _fileCrc32;
            }
        }

        public PatchFile(PatchDirectory directory, string name, uint crc, string patchurl)
        {
            Directory = directory;
            Name = name;
            Crc = crc;
            PatchUrl = patchurl;
        }

        public static PatchFile FromXml(PatchDirectory directory, XmlNode node)
        {
            XmlAttribute name = node.Attributes["Name"];
            XmlAttribute crc = node.Attributes["Crc"];
            XmlAttribute patchurl = node.Attributes["PatchUrl"];

            uint _crc;

            if (name != null &&
                crc != null &&
                patchurl != null &&
                uint.TryParse(crc.Value, out _crc))
            {
                string url = patchurl.Value
                    .Replace("{PatchUrl}", MainForm.PatchUrl)
                    .Replace("{Name}", name.Value)
                    .Replace("{Crc}", crc.Value);

                return new PatchFile(directory, name.Value, _crc, url);
            }

            return null;
        }

        public override string ToString()
        {
            return RelativePhysicalPath;
        }
    }

    public class PatchDirectory
    {
        public PatchDirectory Parent { get; private set; }
        public string Name { get; private set; }
        public LinkedList<PatchDirectory> Subdirectories { get; private set; }
        public LinkedList<PatchFile> Files { get; private set; }
        public bool RemoveUnpatchedFiles { get; private set; }

        /// <summary>
        /// Returns the relative physical path combined by walking the directory tree up until the root folder.
        /// </summary>
        public string RelativePhysicalPath
        {
            get
            {
                if (Parent != null)
                    return Parent.RelativePhysicalPath + "\\" + Name;

                return Name;
            }
        }

        public PatchDirectory(PatchDirectory parent, string name)
        {
            Parent = parent;
            Name = name;
            Subdirectories = new LinkedList<PatchDirectory>();
            Files = new LinkedList<PatchFile>();
            RemoveUnpatchedFiles = true;
        }

        public static PatchDirectory FromXml(PatchList list, PatchDirectory parent, XmlNode node)
        {
            XmlAttribute name = node.Attributes["Name"];
            XmlAttribute removeUnpatchedFiles = node.Attributes["RemoveUnpatchedFiles"];

            if (name != null)
            {
                var directory = new PatchDirectory(parent, name.Value);

                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.Name == "Directory")
                    {
                        directory.Subdirectories.AddLast(PatchDirectory.FromXml(list, directory, child));
                    }
                    else if (child.Name == "File")
                    {
                        directory.Files.AddLast(PatchFile.FromXml(directory, child));
                    }
                }

                if (removeUnpatchedFiles != null &&
                    removeUnpatchedFiles.Value == "false")
                {
                    directory.RemoveUnpatchedFiles = true;
                    list.IgnoredDirectories.Add(directory.RelativePhysicalPath.ToLower());
                }

                return directory;
            }

            return null;
        }

        public override string ToString()
        {
            return RelativePhysicalPath;
        }
    }

    public class PatchList
    {
        public uint PatcherCrc { get; private set; }

        public PatchListMode ConfigMode { get; private set; }
        public PatchDirectory Config { get; private set; }
        public Dictionary<string, PatchFile> ConfigHashTree { get; private set; }

        public PatchListMode CoreModsMode { get; private set; }
        public PatchDirectory CoreMods { get; private set; }
        public Dictionary<string, PatchFile> CoreModsHashTree { get; private set; }

        public PatchListMode ModsMode { get; private set; }
        public PatchDirectory Mods { get; private set; }
        public Dictionary<string, PatchFile> ModsHashTree { get; private set; }

        /// <summary>
        /// Gets the compiled list of actions to be accepted.
        /// </summary>
        public LinkedList<FileAction> FinalActions { get; private set; }

        /// <summary>
        /// Gets all ignored directories.
        /// </summary>
        public HashSet<string> IgnoredDirectories { get; private set; }

        public PatchList(uint patchercrc)
        {
            PatcherCrc = patchercrc;

            ConfigMode = PatchListMode.Partial;
            Config = new PatchDirectory(null, "config");
            ConfigHashTree = new Dictionary<string, PatchFile>();

            CoreModsMode = PatchListMode.Identical;
            CoreMods = new PatchDirectory(null, "coremods");
            CoreModsHashTree = new Dictionary<string, PatchFile>();

            ModsMode = PatchListMode.Identical;
            Mods = new PatchDirectory(null, "mods");
            ModsHashTree = new Dictionary<string, PatchFile>();

            FinalActions = new LinkedList<FileAction>();
            IgnoredDirectories = new HashSet<string>();
        }

        public static PatchList FromXml(XmlNode root)
        {
            var list = new PatchList(0);

            foreach (XmlNode node in root.ChildNodes)
            {
                if (node.Name == "Patcher")
                {
                    uint _crc;
                    XmlAttribute crc = node.Attributes["Crc"];
                    if (crc != null &&
                        uint.TryParse(crc.Value, out _crc))
                    {
                        list.PatcherCrc = _crc;

                        if (_crc != Program.ExecutableCrc)
                        {
                            if (MessageBox.Show(
                                "A newer version of the MCPatcher is available, do you want to go to the website to download?\n" +
                                "If you click No, MCPatcher will continue but may or may not be stable.",
                                "MCPatcher", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                            {
                                Process.Start(MainForm.PatchUrl);
                                Environment.Exit(0);
                            }
                        }
                    }
                }
                
                if (node.Name == "Config")
                {
                    XmlAttribute _mode = node.Attributes["Mode"];
                    PatchListMode mode = PatchListMode.Partial;
                    if (_mode != null &&
                        Enum.TryParse(_mode.Value, out mode))
                    {
                        list.ConfigMode = mode;
                        foreach (XmlNode child in node.ChildNodes)
                        {
                            if (child.Name == "File")
                            {
                                list.Config.Files.AddLast(PatchFile.FromXml(list.Config, child));
                            }
                            else if (child.Name == "Directory")
                            {
                                list.Config.Subdirectories.AddLast(PatchDirectory.FromXml(list, list.Config, child));
                            }
                        }
                    }
                }
                else if (node.Name == "CoreMods")
                {
                    XmlAttribute _mode = node.Attributes["Mode"];
                    PatchListMode mode = PatchListMode.Partial;
                    if (_mode != null &&
                        Enum.TryParse(_mode.Value, out mode))
                    {
                        list.CoreModsMode = mode;
                        foreach (XmlNode child in node.ChildNodes)
                        {
                            if (child.Name == "File")
                            {
                                list.CoreMods.Files.AddLast(PatchFile.FromXml(list.CoreMods, child));
                            }
                            else if (child.Name == "Directory")
                            {
                                list.CoreMods.Subdirectories.AddLast(PatchDirectory.FromXml(list, list.CoreMods, child));
                            }
                        }
                    }
                }
                else if (node.Name == "Mods")
                {
                    XmlAttribute _mode = node.Attributes["Mode"];
                    PatchListMode mode = PatchListMode.Partial;
                    if (_mode != null &&
                        Enum.TryParse(_mode.Value, out mode))
                    {
                        list.ModsMode = mode;
                        foreach (XmlNode child in node.ChildNodes)
                        {
                            if (child.Name == "File")
                            {
                                list.Mods.Files.AddLast(PatchFile.FromXml(list.Mods, child));
                            }
                            else if (child.Name == "Directory")
                            {
                                list.Mods.Subdirectories.AddLast(PatchDirectory.FromXml(list, list.Mods, child));
                            }
                        }
                    }
                }
            }

            list.CreateVirtualHashTree();
            return list;
        }

        private void HashTreeWalkDirectory(PatchDirectory directory, Dictionary<string, PatchFile> output)
        {
            foreach (PatchDirectory dir in directory.Subdirectories)
                HashTreeWalkDirectory(dir, output);

            foreach (PatchFile file in directory.Files)
                output.Add(file.RelativePhysicalPath.ToLower(), file);
        }

        private void CreateVirtualHashTree()
        {
            ConfigHashTree.Clear();
            CoreModsHashTree.Clear();
            ModsHashTree.Clear();

            HashTreeWalkDirectory(Config, ConfigHashTree);
            HashTreeWalkDirectory(CoreMods, CoreModsHashTree);
            HashTreeWalkDirectory(Mods, ModsHashTree);
        }

        public PatchFile FindFile(string filename)
        {
            filename = filename.ToLower();
            PatchFile file = null;

            if (ConfigHashTree.TryGetValue(filename, out file))
                return file;
            if (CoreModsHashTree.TryGetValue(filename, out file))
                return file;
            if (ModsHashTree.TryGetValue(filename, out file))
                return file;

            return file;
        }
    }

    public enum PatchListMode
    {
        Partial,
        Identical
    }

    public enum FileActionResult
    {
        Remove,
        Update,
        Add
    }

    public class FileAction
    {
        public FileActionResult Action { get; private set; }
        public string File { get; private set; }

        public FileAction(FileActionResult action, string file)
        {
            Action = action;
            File = file;
        }
    }
}