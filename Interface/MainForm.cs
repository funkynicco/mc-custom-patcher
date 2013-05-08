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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace MC_Custom_Updater
{
    public partial class MainForm : Form
    {
        public const string PatchUrl = "http://mcvoltz.nprog.com/patch";
        private PatchList _list = null;

        public MainForm(PatchList list)
        {
            _list = list;
            InitializeComponent();

            UpdateSize();

            listView1.BeginUpdate();
            listView1.Items.Clear();
            foreach (FileAction action in list.FinalActions)
            {
                var item = listView1.Items.Add(action.File);
                item.SubItems.Add(Enum.GetName(typeof(FileActionResult), action.Action));
                item.Checked = true;
                item.Tag = action;
            }
            listView1.EndUpdate();
        }

        void UpdateSize()
        {
            listView1.Columns[0].Width = listView1.Width - listView1.Columns[1].Width - 22;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // Generate a list
            LinkedList<PatchFile> files = new LinkedList<PatchFile>();
            foreach (ListViewItem lvi in listView1.Items)
            {
                if (lvi.Checked)
                {
                    FileAction action = (FileAction)lvi.Tag;
                    if (action.Action == FileActionResult.Remove)
                    {
                        try
                        {
                            if (File.Exists(action.File))
                                File.Delete(action.File);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                "Could not delete the following file.\n" + action.File + "\n\nError:" + ex.Message, "MC Patcher",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    }
                    else
                    {
                        var file = _list.FindFile(action.File);
                        if (file != null)
                            files.AddLast(file);
                    }
                }
            }

            Hide();
            using (var dlg = new Updater(files))
            {
                dlg.ShowDialog();
                Close();
            }
        }
    }
}