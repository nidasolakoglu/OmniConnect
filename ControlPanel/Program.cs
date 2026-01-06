using System;
using System.Windows.Forms;
using System.Net.Sockets;


namespace ControlPanel
{
    internal static class Program
    {

        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}
