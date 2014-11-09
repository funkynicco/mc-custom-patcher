using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MC_Custom_Updater
{
    public partial class ShowListDlg : Form
    {
        private readonly SyntaxTagger _tagger = new SyntaxTagger();
        private readonly Font _regularFont = new Font("Calibri", 10.0f);
        private readonly Font _tagFont = new Font("Calibri", 10.0f, FontStyle.Bold);

        /// <summary>
        /// Gets or sets the list xml.
        /// </summary>
        public string ListXml
        {
            get
            {
                return richTextBox1.Text;
            }
            set
            {
                richTextBox1.Text = value;
                foreach (var tag in _tagger.GetSyntaxTags(richTextBox1.Text.Replace("\r\n", "\n")))
                {
                    Debug.WriteLine("Select " + tag.Index + " (+" + tag.Length + ")");

                    richTextBox1.Select(tag.Index, tag.Length);
                    richTextBox1.SelectionColor = Color.Blue;
                    richTextBox1.SelectionFont = _tagFont;
                }

                richTextBox1.Select(0, 0);
            }
        }

        public ShowListDlg()
        {
            InitializeComponent();
            richTextBox1.Font = _regularFont;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // save ...
            File.WriteAllText("list.xml", richTextBox1.Text);

            DialogResult = DialogResult.OK;
        }
    }
}
