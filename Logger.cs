using System;
using System.IO;

namespace ServiceWatchdogArr
{
    public static class Logger
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ServiceWatchdogArr");

        private static readonly string LogFile = Path.Combine(LogDir, "watchdog.log");

        static Logger()
        {
            if (!Directory.Exists(LogDir))
                Directory.CreateDirectory(LogDir);
        }

        public static void Write(string message)
        {
            try
            {
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

                const long maxSizeBytes = 5 * 1024 * 1024;
                var fi = new FileInfo(LogFile);

                if (fi.Exists && fi.Length > maxSizeBytes)
                {
                    string archive = Path.Combine(LogDir, $"watchdog_{DateTime.Now:yyyyMMddHHmmss}.log");
                    File.Move(LogFile, archive, true);

                    foreach (var f in Directory.GetFiles(LogDir, "watchdog_*.log"))
                    {
                        if (File.GetCreationTime(f) < DateTime.Now.AddDays(-7))
                            File.Delete(f);
                    }
                }

                File.AppendAllLines(LogFile, new[] { line });
            }
            catch { }
        }

        public static void OpenLog()
        {
            if (File.Exists(LogFile))
                System.Diagnostics.Process.Start("notepad.exe", LogFile);
            else
                Write("Log file created.");
        }
    }
}