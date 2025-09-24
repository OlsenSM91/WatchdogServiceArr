using System;
using System.IO;
using System.Text.Json;

namespace ServiceWatchdogArr
{
    public class IntervalConfig
    {
        public int Value { get; set; } = 5;
        public string Unit { get; set; } = "Minutes"; // Minutes, Hours, Days
    }

    public class WatchdogConfig
    {
        public IntervalConfig Interval { get; set; } = new IntervalConfig();
        public bool AutoStart { get; set; } = false;

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
                    if (root != null && root.WatchdogConfig != null) return root.WatchdogConfig;
                }
            }
            catch { }
            return new WatchdogConfig();
        }

        public void Save()
        {
            try
            {
                var root = new RootConfig { WatchdogConfig = this };
                Directory.CreateDirectory(Path.GetDirectoryName(RootConfig.GetConfigFilePath())!);
                File.WriteAllText(RootConfig.GetConfigFilePath(),
                    JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
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