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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MC_Custom_Updater
{
    public class MakeList
    {
        private static void WriteIndent(int indent, ref string output, char indentChar = '\t')
        {
            while (indent-- > 0)
                output += indentChar;
        }

        private static void WriteIndentLine(int indent, ref string output, string text, char indentChar = '\t')
        {
            while (indent-- > 0)
                output += indentChar;
            output += text;
            output += "\r\n";
        }

        private static void WalkDirectories(int indent, string directory, ref string output)
        {
            foreach (string dir in Directory.GetDirectories(directory))
            {
                string name = dir.Contains("\\") ? dir.Substring(dir.LastIndexOf("\\") + 1) : dir;
                WriteIndentLine(indent, ref output, "<Directory Name=\"" + name + "\">");
                WalkDirectories(indent + 1, dir, ref output);
                WriteIndentLine(indent, ref output, "</Directory>");
            }

            foreach (string file in Directory.GetFiles(directory))
            {
                uint crc = Crc32.ComputeFile(file);
                WriteIndentLine(indent, ref output, "<File Name=\"" + Path.GetFileName(file) + "\" Crc=\"" + crc + "\" PatchUrl=\"{PatchUrl}/" + directory.Replace("\\", "/") + "/{Name}\" />");
            }
        }

        public static string Create()
        {
            string output =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<!-- List generated at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " -->\r\n\r\n" +
                "<MCPatcher>\r\n\r\n";

            output += "\t<!-- The patcher itself -->\r\n\t<Patcher Crc=\"" + Program.ExecutableCrc + "\" />\r\n\r\n";

            output +=
                "\t<!--\r\n" +
                "\t\tAll files under patching will be checked.\r\n" +
                "\t\tIt will not update or delete anything from subdirectories.\r\n\r\n" +
                "\t\tPartial mode means that it will only keep all files that are not under patching unchanged.\r\n" +
                "\t\tIdentical means that it will replicate the server's directory.\r\n" +
                "\t-->\r\n\r\n";

            // Config
            //output += "\t<Config Mode=\"Partial\">\r\n";
            //WalkDirectories(2, "config", ref output);
            //output += "\t</Config>\r\n\r\n";

            // JarMods
            output += "\t<JarMods Mode=\"Identical\">\r\n";
            WalkDirectories(2, "jarmods", ref output);
            output += "\t</JarMods>\r\n\r\n";

            // Mods
            output += "\t<Mods Mode=\"Identical\">\r\n";
            WalkDirectories(2, "mods", ref output);
            output += "\t</Mods>\r\n\r\n";

            // Flan
            output += "\t<Flan Mode=\"Identical\">\r\n";
            WalkDirectories(2, "Flan", ref output);
            output += "\t</Flan>\r\n\r\n";

            output += "</MCPatcher>";

            return output;
        }
    }
}