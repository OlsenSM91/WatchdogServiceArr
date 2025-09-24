using System;
using System.Windows.Forms;

namespace ServiceWatchdogArr
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ApplicationArguments.Initialize(args);
            Logger.EnsureLogExists();
            CrashReporter.Initialize();
            CrashReporter.CheckForRecentCrash();

            if (!SingleInstanceManager.TryAcquire(WatchdogApplicationContext.RequestBringToForeground, out SingleInstanceManager instanceManager))
            {
                SingleInstanceManager.SignalExistingInstance();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Logger.Write(ApplicationArguments.SafeMode ? "Starting in safe mode" : "Starting ServiceWatchdogArr");

            using (instanceManager)
            using (var context = new WatchdogApplicationContext())
            {
                Application.Run(context);
            }
        }
    }
}