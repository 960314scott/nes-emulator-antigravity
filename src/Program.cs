using System;
using System.IO;
using System.Windows.Forms;
using NesEmulator.UI;

namespace NesEmulator
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            MainForm form = new MainForm();
            if (args != null && args.Length > 0 && File.Exists(args[0]))
            {
                form.LoadRom(args[0]);
            }

            Application.Run(form);
        }
    }
}
