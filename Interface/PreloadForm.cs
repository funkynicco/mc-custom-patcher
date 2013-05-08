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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MC_Custom_Updater
{
    public partial class PreloadForm : Form
    {
        public string PatchState
        {
            get { return label2.Text; }
            set { label2.Text = value; }
        }

        public PreloadForm()
        {
            InitializeComponent();
            label3.Text = Program.ExecutableCrc.ToString();
        }
    }
}