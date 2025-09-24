using Microsoft.Win32;

namespace ServiceWatchdogArr
{
    public static class AutoStartHelper
    {
        private const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "ServiceWatchdogArr";

        public static void Enable(string exePath)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY, writable: true);
            key?.SetValue(APP_NAME, $"\"{exePath}\"");
        }

        public static void Disable()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY, writable: true);
            key?.DeleteValue(APP_NAME, false);
        }

        public static bool IsEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY, writable: false);
            return key?.GetValue(APP_NAME) != null;
        }
    }
}