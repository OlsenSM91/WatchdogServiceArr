using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ServiceWatchdogArr
{
    public class IntervalConfig
    {
        public int Value { get; set; } = 5;
        public string Unit { get; set; } = "Minutes"; // Minutes, Hours, Days
    }

    public class WatchedApplication
    {
        public string Name { get; set; } = string.Empty;
        public string? ProcessName { get; set; }
        public string? ExecutablePath { get; set; }
        public string? ServiceName { get; set; }

        public static List<WatchedApplication> CreateDefaults() => new()
        {
            new WatchedApplication
            {
                Name = "Plex Media Server",
                ProcessName = "Plex Media Server",
                ExecutablePath = @"C:\\Program Files\\Plex\\Plex Media Server\\Plex Media Server.exe",
                ServiceName = "PlexUpdateService"
            },
            new WatchedApplication
            {
                Name = "Radarr",
                ProcessName = "Radarr",
                ExecutablePath = @"C:\\ProgramData\\Radarr\\Radarr.exe",
                ServiceName = "Radarr"
            },
            new WatchedApplication
            {
                Name = "Sonarr",
                ProcessName = "Sonarr",
                ExecutablePath = @"C:\\ProgramData\\Sonarr\\bin\\Sonarr.exe",
                ServiceName = "Sonarr"
            },
            new WatchedApplication
            {
                Name = "Docker Desktop",
                ProcessName = "Docker Desktop",
                ExecutablePath = @"C:\\Program Files\\Docker\\Docker\\frontend\\Docker Desktop.exe",
                ServiceName = "com.docker.service"
            },
            new WatchedApplication
            {
                Name = "Corsair iCUE",
                ProcessName = "iCUE",
                ExecutablePath = @"C:\\Program Files\\Corsair\\Corsair iCUE5 Software\\iCUE.exe"
            }
        };
    }

    public class WatchdogConfig
    {
        public IntervalConfig Interval { get; set; } = new IntervalConfig();
        public bool AutoStart { get; set; } = false;
        public List<WatchedApplication> Applications { get; set; } = WatchedApplication.CreateDefaults();

        public int IntervalMinutes => Interval.Unit switch
        {
            "Hours" => Math.Max(1, Interval.Value) * 60,
            "Days"  => Math.Max(1, Interval.Value) * 24 * 60,
            _       => Math.Max(1, Interval.Value)
        };

        public static WatchdogConfig Load()
        {
            try
            {
                string path = RootConfig.GetConfigFilePath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var root = JsonSerializer.Deserialize<RootConfig>(json);
                    if (root?.WatchdogConfig != null)
                    {
                        root.WatchdogConfig.EnsureDefaults();
                        return root.WatchdogConfig;
                    }
                }
            }
            catch { }
            var fallback = new WatchdogConfig();
            fallback.EnsureDefaults();
            return fallback;
        }

        public void Save()
        {
            try
            {
                EnsureDefaults();
                var root = new RootConfig { WatchdogConfig = this };
                Directory.CreateDirectory(Path.GetDirectoryName(RootConfig.GetConfigFilePath())!);
                File.WriteAllText(RootConfig.GetConfigFilePath(),
                    JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void EnsureDefaults()
        {
            if (Applications == null || Applications.Count == 0)
                Applications = WatchedApplication.CreateDefaults();
        }
    }

    public class RootConfig
    {
        public WatchdogConfig WatchdogConfig { get; set; } = new WatchdogConfig();

        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ServiceWatchdogArr");

        private static readonly string ConfigFile = Path.Combine(ConfigDir, "appsettings.json");

        public static string GetConfigFilePath() => ConfigFile;
    }
}
