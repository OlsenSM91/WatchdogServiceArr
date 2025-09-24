using System;

namespace ServiceWatchdogArr
{
    internal static class ApplicationArguments
    {
        public static bool SafeMode { get; private set; }

        public static void Initialize(string[] args)
        {
            SafeMode = false;

            foreach (string argument in args)
            {
                if (string.Equals(argument, "--safe-mode", StringComparison.OrdinalIgnoreCase))
                {
                    SafeMode = true;
                }
            }
        }
    }
}
