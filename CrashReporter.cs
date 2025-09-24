using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ServiceWatchdogArr
{
    internal static class CrashReporter
    {
        private static int _initialized;

        public static void Initialize()
        {
            if (Interlocked.Exchange(ref _initialized, 1) == 1)
            {
                return;
            }

            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        public static void CheckForRecentCrash()
        {
            try
            {
                if (!File.Exists(Paths.LastCrashMarkerPath))
                {
                    return;
                }

                string content = File.ReadAllText(Paths.LastCrashMarkerPath);
                if (DateTime.TryParseExact(content, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime crashUtc))
                {
                    if (DateTime.UtcNow - crashUtc <= TimeSpan.FromMinutes(5))
                    {
                        Logger.Write("Previous crash detected within the last five minutes.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write(ex, "Failed to evaluate crash marker");
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            HandleException(e.Exception, "UI thread exception");
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                HandleException(exception, "Unhandled exception");
            }
        }

        private static void HandleException(Exception exception, string context)
        {
            try
            {
                Logger.Write(exception, context);
                WriteCrashFile(exception, context);
            }
            catch
            {
                // Ignore nested failures.
            }
        }

        private static void WriteCrashFile(Exception exception, string context)
        {
            try
            {
                Directory.CreateDirectory(Paths.AppDataDirectory);
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                string crashFile = string.Concat(Paths.CrashLogFilePrefix, timestamp, ".log");
                var builder = new StringBuilder();
                builder.AppendLine(context);
                builder.AppendLine(DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
                builder.AppendLine(exception.ToString());
                File.WriteAllText(crashFile, builder.ToString());
                File.WriteAllText(Paths.LastCrashMarkerPath, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            }
            catch
            {
                // Ignore crash file failures.
            }
        }
    }
}
