using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Connecting Remote Desktop")]
[assembly: AssemblyDescription("Connecting - Portable & Secure Remote Control Platform")]
[assembly: AssemblyCompany("Connecting")]
[assembly: AssemblyProduct("Connecting Remote Desktop Enterprise")]
[assembly: AssemblyCopyright("Copyright © 2026 Connecting")]
[assembly: AssemblyFileVersion("1.0.2.0")]
[assembly: AssemblyVersion("1.0.2.0")]

namespace Conecting
{
    /// <summary>
    /// Application Entry Point.
    /// Initializes Visual Styles, DPI Awareness, and main Application Loop.
    /// </summary>
    public static class Program
    {
        private static Mutex appMutex = null;

        [STAThread]
        public static void Main(string[] args)
        {
            bool createdNew;
            appMutex = new Mutex(true, "Global\\Connecting_RemoteDesktop_SingleInstance_Mutex", out createdNew);

            if (!createdNew)
            {
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                MainForm mainForm = new MainForm();
                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Application Error: " + ex.Message, "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (appMutex != null)
                {
                    try { appMutex.ReleaseMutex(); } catch { }
                    appMutex.Dispose();
                }
            }
        }
    }
}
