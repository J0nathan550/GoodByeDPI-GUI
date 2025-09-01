using AutoUpdaterDotNET;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace GoodByeDPI_GUI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AutoUpdater.Synchronous = true;
            AutoUpdater.ShowSkipButton = false;
            AutoUpdater.RunUpdateAsAdmin = false;
            AutoUpdater.OpenDownloadPage = true;
            AutoUpdater.LetUserSelectRemindLater = false;
            AutoUpdater.RemindLaterTimeSpan = RemindLaterFormat.Days;
            AutoUpdater.RemindLaterAt = 2;
            AutoUpdater.TopMost = true;
            AutoUpdater.AppTitle = "GoodByeDPI GUI | AutoUpdate";
            if (File.Exists("iconAutoUpdate.ico"))
            {
                try
                {
                    using (var icon = new System.Drawing.Icon("iconAutoUpdate.ico"))
                    {
                        AutoUpdater.Icon = icon.ToBitmap();
                    }
                }
                catch
                {
                }
            }
            AutoUpdater.Start("https://github.com/J0nathan550/GoodByeDPI-GUI/AutoUpdater.xml");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Process[] processes = Process.GetProcessesByName("goodbyedpi");
            foreach (var proc in processes)
            {
                try
                {
                    bool closed = proc.CloseMainWindow();

                    if (closed)
                    {
                        proc.WaitForExit(5000);

                        if (!proc.HasExited)
                        {
                            proc.Kill();
                        }
                    }
                    else
                    {
                        proc.Kill();
                    }
                }
                catch
                {
                }
            }
            base.OnExit(e);
        }
    }
}
