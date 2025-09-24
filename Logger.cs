using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ServiceWatchdogArr
{
    internal static class Logger
    {
        private const long MaxLogSizeBytes = 5L * 1024L * 1024L;
        private static readonly object SyncRoot = new object();

        static Logger()
        {
            Directory.CreateDirectory(Paths.AppDataDirectory);
        }

        public static void Write(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            try
            {
                lock (SyncRoot)
                {
                    RotateIfNeeded();
                    var line = FormatLine(message);
                    File.AppendAllText(Paths.LogFilePath, line, Encoding.UTF8);
                }
            }
            catch
            {
                // Intentionally swallow logging failures. Avoid throwing from logger.
            }
        }

        public static void Write(Exception exception, string context)
        {
            if (exception == null)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.Append(context);
            builder.AppendLine();
            builder.AppendLine(exception.ToString());
            Write(builder.ToString());
        }

        public static void EnsureLogExists()
        {
            try
            {
                if (!File.Exists(Paths.LogFilePath))
                {
                    lock (SyncRoot)
                    {
                        if (!File.Exists(Paths.LogFilePath))
                        {
                            File.AppendAllText(Paths.LogFilePath, FormatLine("Log created."), Encoding.UTF8);
                        }
                    }
                }
            }
            catch
            {
                // Ignore creation errors.
            }
        }

        public static void OpenLog()
        {
            try
            {
                EnsureLogExists();
                var startInfo = new ProcessStartInfo
                {
                    FileName = Paths.LogFilePath,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                Write(ex, "Failed to open log file");
            }
        }

        private static void RotateIfNeeded()
        {
            try
            {
                if (!File.Exists(Paths.LogFilePath))
                {
                    return;
                }

                var fileInfo = new FileInfo(Paths.LogFilePath);
                if (fileInfo.Length <= MaxLogSizeBytes)
                {
                    return;
                }

                string archiveName = string.Format(
                    CultureInfo.InvariantCulture,
                    "watchdog_{0:yyyyMMddHHmmss}.log",
                    DateTime.Now);
                string archivePath = Path.Combine(Paths.AppDataDirectory, archiveName);
                File.Move(Paths.LogFilePath, archivePath, overwrite: true);

                PurgeOldArchives();
            }
            catch
            {
                // Ignore rotation errors. We prefer stale logs over failed writes.
            }
        }

        private static void PurgeOldArchives()
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-7);
                IEnumerable<string> archives = Directory.EnumerateFiles(Paths.AppDataDirectory, "watchdog_*.log");
                foreach (string archive in archives)
                {
                    try
                    {
                        if (File.GetCreationTime(archive) < cutoff)
                        {
                            File.Delete(archive);
                        }
                    }
                    catch
                    {
                        // Ignore deletion errors.
                    }
                }
            }
            catch
            {
                // Ignore purge errors.
            }
        }

        private static string FormatLine(string message)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "[{0:yyyy-MM-dd HH:mm:ss}] {1}{2}",
                DateTime.Now,
                message.TrimEnd('\r', '\n'),
                Environment.NewLine);
        }
    }
}
