using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ServiceWatchdogArr
{
    internal enum IntervalUnit
    {
        Minutes,
        Hours,
        Days
    }

    internal sealed class IntervalConfig
    {
        public int Value { get; set; } = 5;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public IntervalUnit Unit { get; set; } = IntervalUnit.Minutes;

        public TimeSpan ToTimeSpan()
        {
            int clampedValue = Math.Clamp(Value <= 0 ? 1 : Value, 1, 7 * 24 * 60);
            Value = clampedValue;

            return Unit switch
            {
                IntervalUnit.Hours => TimeSpan.FromMinutes(Math.Clamp(clampedValue * 60, 1, 7 * 24 * 60)),
                IntervalUnit.Days => TimeSpan.FromMinutes(Math.Clamp(clampedValue * 24 * 60, 1, 7 * 24 * 60)),
                _ => TimeSpan.FromMinutes(clampedValue)
            };
        }

        public IntervalConfig Clone()
        {
            return new IntervalConfig
            {
                Value = Value,
                Unit = Unit
            };
        }
    }

    internal sealed class MonitoredApplication
    {
        public string Name { get; set; } = string.Empty;

        public string ServiceName { get; set; } = string.Empty;

        public List<string> ProcessNames { get; set; } = new List<string>();

        public string ExecutablePath { get; set; } = string.Empty;

        public bool MonitoringEnabled { get; set; } = true;

        public MonitoredApplication Clone()
        {
            return new MonitoredApplication
            {
                Name = Name,
                ServiceName = ServiceName,
                ExecutablePath = ExecutablePath,
                MonitoringEnabled = MonitoringEnabled,
                ProcessNames = new List<string>(ProcessNames)
            };
        }

        public void Normalize()
        {
            Name = Name?.Trim() ?? string.Empty;
            ServiceName = ServiceName?.Trim() ?? string.Empty;
            ExecutablePath = ExecutablePath?.Trim() ?? string.Empty;
            ProcessNames = ProcessNames
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Select(static name => ProcessNameHelper.Normalize(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    internal sealed class WatchdogConfig
    {
        public IntervalConfig Interval { get; set; } = new IntervalConfig();

        public bool AutoStart { get; set; }

        public bool GlobalMonitoringEnabled { get; set; } = true;

        public List<MonitoredApplication> Applications { get; set; } = new List<MonitoredApplication>();

        [JsonIgnore]
        public TimeSpan MonitoringInterval => Interval.ToTimeSpan();

        public WatchdogConfig Clone()
        {
            return new WatchdogConfig
            {
                Interval = Interval.Clone(),
                AutoStart = AutoStart,
                GlobalMonitoringEnabled = GlobalMonitoringEnabled,
                Applications = Applications.Select(app => app.Clone()).ToList()
            };
        }

        public void Normalize()
        {
            Interval.ToTimeSpan();
            foreach (MonitoredApplication application in Applications)
            {
                application.Normalize();
            }
        }
    }

    internal sealed class ConfigManager
    {
        private readonly object _syncRoot = new object();
        private readonly JsonSerializerOptions _serializerOptions;
        private WatchdogConfig _config;

        public ConfigManager()
        {
            _serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            _serializerOptions.Converters.Add(new JsonStringEnumConverter());

            _config = LoadFromDisk();
        }

        public WatchdogConfig GetSnapshot()
        {
            lock (_syncRoot)
            {
                return _config.Clone();
            }
        }

        public WatchdogConfig Update(Func<WatchdogConfig, WatchdogConfig> updater)
        {
            ArgumentNullException.ThrowIfNull(updater);

            lock (_syncRoot)
            {
                WatchdogConfig snapshot = _config.Clone();
                WatchdogConfig updated = updater(snapshot) ?? snapshot;
                updated.Normalize();
                _config = updated.Clone();
                SaveToDisk(updated);
                return _config.Clone();
            }
        }

        public WatchdogConfig Update(Action<WatchdogConfig> updater)
        {
            return Update(config =>
            {
                updater(config);
                return config;
            });
        }

        private WatchdogConfig LoadFromDisk()
        {
            try
            {
                if (!File.Exists(Paths.ConfigFilePath))
                {
                    return CreateDefaultConfig();
                }

                string json = File.ReadAllText(Paths.ConfigFilePath);
                JsonNode rootNode = JsonNode.Parse(json) ?? new JsonObject();

                JsonObject configNode = EnsureConfigNode(rootNode);
                var config = configNode.Deserialize<WatchdogConfig>(_serializerOptions) ?? CreateDefaultConfig();
                EnsureApplicationDefaults(config);
                config.Normalize();
                return config;
            }
            catch (Exception ex)
            {
                Logger.Write(ex, "Failed to load configuration; reverting to defaults");
                return CreateDefaultConfig();
            }
        }

        private void SaveToDisk(WatchdogConfig config)
        {
            try
            {
                Directory.CreateDirectory(Paths.AppDataDirectory);
                var root = new JsonObject
                {
                    ["WatchdogConfig"] = JsonNode.Parse(JsonSerializer.Serialize(config, _serializerOptions))
                };
                File.WriteAllText(Paths.ConfigFilePath, root.ToJsonString(_serializerOptions));
            }
            catch (Exception ex)
            {
                Logger.Write(ex, "Failed to persist configuration");
            }
        }

        private static WatchdogConfig CreateDefaultConfig()
        {
            var config = new WatchdogConfig
            {
                Interval = new IntervalConfig { Value = 5, Unit = IntervalUnit.Minutes },
                AutoStart = false,
                GlobalMonitoringEnabled = true,
                Applications = new List<MonitoredApplication>
                {
                    new MonitoredApplication
                    {
                        Name = "Docker Desktop",
                        ServiceName = "com.docker.service",
                        ExecutablePath = @"C:\\Program Files\\Docker\\Docker\\Docker Desktop.exe",
                        MonitoringEnabled = true,
                        ProcessNames = new List<string> { "Docker Desktop", "Docker Desktop Backend" }
                    }
                }
            };
            config.Normalize();
            return config;
        }

        private static void EnsureApplicationDefaults(WatchdogConfig config)
        {
            foreach (MonitoredApplication application in config.Applications)
            {
                if (application.ProcessNames == null)
                {
                    application.ProcessNames = new List<string>();
                }

                if (application.ProcessNames.Count == 0 && !string.IsNullOrWhiteSpace(application.ServiceName))
                {
                    application.ProcessNames.Add(application.ServiceName);
                }

                application.MonitoringEnabled = application.MonitoringEnabled;
            }
        }

        private static JsonObject EnsureConfigNode(JsonNode rootNode)
        {
            JsonObject rootObject;
            if (rootNode is JsonObject jsonObject)
            {
                rootObject = jsonObject;
            }
            else
            {
                rootObject = new JsonObject();
            }

            if (!rootObject.TryGetPropertyValue("WatchdogConfig", out JsonNode configNode) || configNode is not JsonObject configObject)
            {
                configObject = new JsonObject();
                rootObject["WatchdogConfig"] = configObject;
            }

            EnsureIntervalDefaults(configObject);
            EnsureGlobalMonitoring(configObject);
            EnsureApplicationsArray(configObject);
            return configObject;
        }

        private static void EnsureIntervalDefaults(JsonObject configObject)
        {
            if (!configObject.TryGetPropertyValue("Interval", out JsonNode intervalNode) || intervalNode is not JsonObject intervalObject)
            {
                intervalObject = new JsonObject();
                configObject["Interval"] = intervalObject;
            }

            if (!intervalObject.TryGetPropertyValue("Value", out JsonNode valueNode) || valueNode == null)
            {
                intervalObject["Value"] = 5;
            }

            if (!intervalObject.TryGetPropertyValue("Unit", out JsonNode unitNode) || unitNode == null)
            {
                intervalObject["Unit"] = IntervalUnit.Minutes.ToString();
            }
        }

        private static void EnsureGlobalMonitoring(JsonObject configObject)
        {
            if (!configObject.TryGetPropertyValue("GlobalMonitoringEnabled", out JsonNode globalNode) || globalNode == null)
            {
                configObject["GlobalMonitoringEnabled"] = true;
            }
        }

        private static void EnsureApplicationsArray(JsonObject configObject)
        {
            if (!configObject.TryGetPropertyValue("Applications", out JsonNode appsNode) || appsNode is not JsonArray appsArray)
            {
                appsArray = new JsonArray();
                configObject["Applications"] = appsArray;
            }

            foreach (JsonNode node in appsArray)
            {
                if (node is not JsonObject appObject)
                {
                    continue;
                }

                if (!appObject.TryGetPropertyValue("MonitoringEnabled", out JsonNode enabledNode) || enabledNode == null)
                {
                    appObject["MonitoringEnabled"] = true;
                }

                if (appObject.TryGetPropertyValue("ProcessNames", out JsonNode processNode))
                {
                    if (processNode is JsonValue value && value.TryGetValue<string>(out string singleProcess))
                    {
                        appObject["ProcessNames"] = new JsonArray(singleProcess);
                    }
                    else if (processNode == null)
                    {
                        appObject["ProcessNames"] = new JsonArray();
                    }
                }
                else if (appObject.TryGetPropertyValue("ProcessName", out JsonNode legacyProcessNode) && legacyProcessNode is JsonValue legacyValue && legacyValue.TryGetValue<string>(out string legacyProcess))
                {
                    appObject["ProcessNames"] = new JsonArray(legacyProcess);
                }

                if (!appObject.TryGetPropertyValue("ProcessNames", out JsonNode ensuredNode) || ensuredNode is not JsonArray)
                {
                    appObject["ProcessNames"] = new JsonArray();
                }
            }
        }
    }

    internal static class ProcessNameHelper
    {
        public static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            string trimmed = name.Trim();
            if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[..^4];
            }

            return trimmed;
        }
    }
}
