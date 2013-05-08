using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MC_Custom_Updater
{
    public class SyntaxTagger
    {
        private HashSet<string> _words = new HashSet<string>();

        private void AddRange(string[] words)
        {
            foreach (string word in words)
                _words.Add(word);
        }

        public SyntaxTagger()
        {
            AddRange(new string[]
            { 
                "?xml",
                "mcpatcher",
                "patcher",
                "config",
                "coremods",
                "mods",
                "directory",
                "file"
            });
        }

        public IEnumerable<SyntaxTag> GetSyntaxTags(string code)
        {
            // Rich text box treats \r\n as one character.
            // All new lines must be \n to get the correct offset in the rich text box.
            code = code.Replace("\r\n", "\n");

            List<SyntaxTag> tags = new List<SyntaxTag>();

            for (int i = 0; i < code.Length;)
            {
                char cb = code[i];
                if (cb == '<')
                {
                    while (char.IsWhiteSpace(cb = code[++i]) && cb != '>' && cb != 0) ;
                    if (cb == 0)
                        break;

                    if (cb == '>')
                        continue;

                    if (cb == '/') // ignore ending tag notification
                        cb = code[++i];

                    int begin = i;
                    while (!char.IsWhiteSpace(cb = code[++i]) && cb != '>' && cb != 0) ;
                    if (cb == 0)
                        break;

                    string word = code.Substring(begin, i - begin).ToLower();
                    Debug.WriteLine(word);
                    if (_words.Contains(word))
                        tags.Add(new SyntaxTag(begin, i - begin));
                }

                ++i;
            }

            return tags;
        }
    }

    public class SyntaxTag
    {
        public int Index { get; private set; }
        public int Length { get; private set; }

        public SyntaxTag(int index, int length)
        {
            Index = index;
            Length = length;
        }
    }
}