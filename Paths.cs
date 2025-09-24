using System;
using System.IO;

namespace ServiceWatchdogArr
{
    internal static class Paths
    {
        static Paths()
        {
            Directory.CreateDirectory(AppDataDirectory);
        }

        public static string AppDataDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ServiceWatchdogArr");

        public static string ConfigFilePath => Path.Combine(AppDataDirectory, "appsettings.json");

        public static string LogFilePath => Path.Combine(AppDataDirectory, "watchdog.log");

        public static string CrashLogFilePrefix => Path.Combine(AppDataDirectory, "crash-");

        public static string LastCrashMarkerPath => Path.Combine(AppDataDirectory, "last-crash.txt");
    }
}
