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