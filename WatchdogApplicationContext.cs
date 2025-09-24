using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ServiceWatchdogArr
{
    internal sealed class WatchdogApplicationContext : ApplicationContext
    {
        private static WatchdogApplicationContext s_current;

        private readonly SynchronizationContext _syncContext;
        private readonly NotifyIcon _trayIcon;
        private readonly Icon _baseIcon;
        private readonly Dictionary<TrayStatus, Icon> _trayIcons = new Dictionary<TrayStatus, Icon>();
        private readonly Dictionary<ApplicationHealth, Image> _statusImages = new Dictionary<ApplicationHealth, Image>();
        private readonly ConfigManager _configManager;
        private readonly MonitoringEngine _monitoringEngine;
        private readonly ServiceManager _serviceManager = new ServiceManager();
        private readonly ProcessManager _processManager = new ProcessManager();
        private readonly Dictionary<string, ApplicationStatusSnapshot> _latestStatuses = new Dictionary<string, ApplicationStatusSnapshot>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ToolStripMenuItem> _applicationMenuItems = new Dictionary<string, ToolStripMenuItem>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ToolStripMenuItem> _toggleMenuItems = new Dictionary<string, ToolStripMenuItem>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _serviceRequiresElevation = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _processOnlyConsent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _promptShown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly ToolStripMenuItem _globalMonitoringItem;
        private readonly ToolStripMenuItem _refreshItem;
        private readonly ToolStripMenuItem _servicesRootItem;
        private readonly ToolStripMenuItem _settingsItem;
        private readonly ToolStripMenuItem _openLogsItem;
        private readonly ToolStripMenuItem _exitItem;
        private readonly ContextMenuStrip _contextMenu;

        private WatchdogConfig _currentConfig;
        private TrayStatus _currentTrayStatus = TrayStatus.Unknown;
        private bool _disposed;

        public WatchdogApplicationContext()
        {
            s_current = this;
            _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();

            _configManager = new ConfigManager();
            _currentConfig = _configManager.GetSnapshot();
            _monitoringEngine = new MonitoringEngine(_currentConfig);
            _monitoringEngine.MonitoringCycleCompleted += OnMonitoringCycleCompleted;

            _baseIcon = LoadBaseIcon();
            _trayIcon = new NotifyIcon
            {
                Icon = _baseIcon,
                Visible = true,
                Text = "ServiceWatchdogArr"
            };

            _contextMenu = new ContextMenuStrip
            {
                ShowImageMargin = true
            };

            _servicesRootItem = new ToolStripMenuItem("Services");
            _contextMenu.Items.Add(_servicesRootItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            _globalMonitoringItem = new ToolStripMenuItem("Global Monitoring Enabled")
            {
                Checked = _currentConfig.GlobalMonitoringEnabled,
                CheckOnClick = false
            };
            _globalMonitoringItem.Click += (_, _) => ToggleGlobalMonitoring();
            _contextMenu.Items.Add(_globalMonitoringItem);

            _refreshItem = new ToolStripMenuItem("Refresh Now");
            _refreshItem.Click += async (_, _) => await _monitoringEngine.RefreshNowAsync().ConfigureAwait(false);
            _contextMenu.Items.Add(_refreshItem);

            _openLogsItem = new ToolStripMenuItem("Open Logs");
            _openLogsItem.Click += (_, _) => Logger.OpenLog();
            _contextMenu.Items.Add(_openLogsItem);

            _settingsItem = new ToolStripMenuItem("Settings…");
            _settingsItem.Click += (_, _) => ShowSettings();
            _contextMenu.Items.Add(_settingsItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            _exitItem = new ToolStripMenuItem("Exit");
            _exitItem.Click += (_, _) => ExitApplication();
            _contextMenu.Items.Add(_exitItem);

            _contextMenu.Opening += (_, _) => UpdateMenuFromStatuses();
            _trayIcon.ContextMenuStrip = _contextMenu;

            BuildServiceMenu();
            UpdateMenuFromStatuses();
            UpdateTraySummary();

            Logger.Write("Watchdog tray initialized");

            if (ApplicationArguments.SafeMode)
            {
                Logger.Write("Safe mode is active. Restart operations are disabled.");
            }

            _ = _monitoringEngine.RefreshNowAsync();
        }

        public static void RequestBringToForeground()
        {
            WatchdogApplicationContext current = s_current;
            if (current == null)
            {
                return;
            }

            current._syncContext.Post(_ => current.ShowSettings(), null);
        }

        private void OnMonitoringCycleCompleted(object sender, MonitoringCycleEventArgs e)
        {
            _syncContext.Post(_ =>
            {
                _latestStatuses.Clear();
                foreach (ApplicationStatusSnapshot snapshot in e.Statuses)
                {
                    _latestStatuses[snapshot.Application.Name] = snapshot;
                }

                UpdateMenuFromStatuses();
                UpdateTraySummary();
            }, null);
        }

        private void BuildServiceMenu()
        {
            _servicesRootItem.DropDownItems.Clear();
            _applicationMenuItems.Clear();
            _toggleMenuItems.Clear();
            foreach (MonitoredApplication application in _currentConfig.Applications)
            {
                var appItem = new ToolStripMenuItem(application.Name)
                {
                    Tag = application.Name,
                    ImageScaling = ToolStripItemImageScaling.None
                };

                var restartItem = new ToolStripMenuItem("Restart")
                {
                    Enabled = !ApplicationArguments.SafeMode
                };
                restartItem.Click += (_, _) => RestartApplication(application.Name);

                var toggleItem = new ToolStripMenuItem(application.MonitoringEnabled ? "Disable Monitoring" : "Enable Monitoring");
                toggleItem.Click += (_, _) => ToggleApplicationMonitoring(application.Name);
                _toggleMenuItems[application.Name] = toggleItem;

                appItem.DropDownItems.Add(restartItem);
                appItem.DropDownItems.Add(toggleItem);

                _servicesRootItem.DropDownItems.Add(appItem);
                _applicationMenuItems[application.Name] = appItem;
            }
        }

        private void UpdateMenuFromStatuses()
        {
            foreach (var kvp in _applicationMenuItems)
            {
                string appName = kvp.Key;
                ToolStripMenuItem menuItem = kvp.Value;
                if (_latestStatuses.TryGetValue(appName, out ApplicationStatusSnapshot snapshot))
                {
                    menuItem.Text = string.Concat(GetStatusEmoji(snapshot.Health), " ", snapshot.Application.Name);
                    menuItem.Image = GetStatusImage(snapshot.Health);
                    menuItem.ToolTipText = BuildTooltip(snapshot);

                    if (_toggleMenuItems.TryGetValue(appName, out ToolStripMenuItem toggleItem))
                    {
                        toggleItem.Text = snapshot.Application.MonitoringEnabled ? "Disable Monitoring" : "Enable Monitoring";
                    }
                }
                else
                {
                    menuItem.Text = string.Concat(GetStatusEmoji(ApplicationHealth.MonitoringDisabled), " ", menuItem.Tag);
                    menuItem.Image = GetStatusImage(ApplicationHealth.MonitoringDisabled);
                }
            }

            _globalMonitoringItem.Checked = _currentConfig.GlobalMonitoringEnabled;
        }

        private void UpdateTraySummary()
        {
            if (_latestStatuses.Count == 0)
            {
                SetTrayStatus(TrayStatus.Unknown, TooltipWithSafeMode("ServiceWatchdogArr: Monitoring"));
                return;
            }

            bool anyMonitoring = _latestStatuses.Values.Any(static status => status.EffectiveMonitoringEnabled);
            bool anyUnhealthy = _latestStatuses.Values.Any(static status => status.Health == ApplicationHealth.Unhealthy);

            TrayStatus trayStatus;
            string tooltip;

            if (!anyMonitoring)
            {
                trayStatus = TrayStatus.MonitoringDisabled;
                tooltip = "ServiceWatchdogArr: Monitoring off";
            }
            else if (anyUnhealthy)
            {
                trayStatus = TrayStatus.IssueDetected;
                tooltip = "ServiceWatchdogArr: Issue detected";
            }
            else
            {
                trayStatus = TrayStatus.AllHealthy;
                tooltip = "ServiceWatchdogArr: All OK";
            }

            SetTrayStatus(trayStatus, TooltipWithSafeMode(tooltip));
        }

        private string TooltipWithSafeMode(string tooltip)
        {
            if (ApplicationArguments.SafeMode)
            {
                return tooltip + " (Safe Mode)";
            }

            return tooltip;
        }

        private void SetTrayStatus(TrayStatus status, string tooltip)
        {
            if (_currentTrayStatus != status)
            {
                _trayIcon.Icon = GetTrayIcon(status);
                _currentTrayStatus = status;
            }

            if (!string.Equals(_trayIcon.Text, tooltip, StringComparison.Ordinal))
            {
                _trayIcon.Text = tooltip;
            }
        }

        private Icon GetTrayIcon(TrayStatus status)
        {
            if (status == TrayStatus.Unknown)
            {
                return _baseIcon;
            }

            if (_trayIcons.TryGetValue(status, out Icon icon))
            {
                return icon;
            }

            Color fill;
            Color border;
            switch (status)
            {
                case TrayStatus.AllHealthy:
                    fill = Color.LimeGreen;
                    border = Color.SeaGreen;
                    break;
                case TrayStatus.IssueDetected:
                    fill = Color.OrangeRed;
                    border = Color.Maroon;
                    break;
                default:
                    fill = Color.LightGray;
                    border = Color.DimGray;
                    break;
            }

            Icon generated = CreateStatusIcon(fill, border);
            _trayIcons[status] = generated;
            return generated;
        }

        private Icon CreateStatusIcon(Color fill, Color border)
        {
            using Bitmap baseBitmap = _baseIcon.ToBitmap();
            using Bitmap canvas = new Bitmap(baseBitmap.Width, baseBitmap.Height);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawImage(baseBitmap, Point.Empty);

                int diameter = Math.Max(6, canvas.Width / 3);
                int padding = Math.Max(2, canvas.Width / 10);
                var rect = new Rectangle(canvas.Width - diameter - padding, canvas.Height - diameter - padding, diameter, diameter);

                using (var brush = new SolidBrush(fill))
                {
                    g.FillEllipse(brush, rect);
                }

                using (var pen = new Pen(border, Math.Max(2f, diameter / 5f)))
                {
                    g.DrawEllipse(pen, rect);
                }
            }

            Icon icon = CreateIconFromBitmap(canvas);
            return icon;
        }

        private Image GetStatusImage(ApplicationHealth health)
        {
            if (_statusImages.TryGetValue(health, out Image image))
            {
                return image;
            }

            Color fill;
            Color border;
            switch (health)
            {
                case ApplicationHealth.Healthy:
                    fill = Color.LimeGreen;
                    border = Color.SeaGreen;
                    break;
                case ApplicationHealth.Unhealthy:
                    fill = Color.OrangeRed;
                    border = Color.Maroon;
                    break;
                default:
                    fill = Color.LightGray;
                    border = Color.DimGray;
                    break;
            }

            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                Rectangle rect = new Rectangle(3, 3, 10, 10);
                using (var brush = new SolidBrush(fill))
                {
                    g.FillEllipse(brush, rect);
                }

                using (var pen = new Pen(border, 2f))
                {
                    g.DrawEllipse(pen, rect);
                }
            }

            _statusImages[health] = bmp;
            return bmp;
        }

        private static string GetStatusEmoji(ApplicationHealth health)
        {
            return health switch
            {
                ApplicationHealth.Healthy => "🟢",
                ApplicationHealth.Unhealthy => "🔴",
                _ => "⚪"
            };
        }

        private static string BuildTooltip(ApplicationStatusSnapshot snapshot)
        {
            string serviceState;
            if (!snapshot.Service.Exists)
            {
                serviceState = "Service: not configured";
            }
            else if (snapshot.Service.AccessDenied)
            {
                serviceState = "Service: access denied";
            }
            else if (snapshot.Service.HasError)
            {
                serviceState = "Service: error";
            }
            else
            {
                serviceState = snapshot.ServiceRunning ? "Service: running" : "Service: stopped";
            }

            string processState;
            if (snapshot.ProcessNames.Count == 0)
            {
                processState = "Process: not configured";
            }
            else if (snapshot.ProcessRunning)
            {
                processState = "Process: running";
            }
            else
            {
                processState = "Process: stopped";
            }

            return string.Concat(serviceState, Environment.NewLine, processState);
        }

        private void ToggleGlobalMonitoring()
        {
            bool newValue = !_currentConfig.GlobalMonitoringEnabled;
            Logger.Write(newValue ? "Enabled global monitoring" : "Disabled global monitoring");
            _currentConfig = _configManager.Update(config =>
            {
                config.GlobalMonitoringEnabled = newValue;
                return config;
            });
            _monitoringEngine.ApplyConfiguration(_currentConfig);
            BuildServiceMenu();
            UpdateMenuFromStatuses();
            _ = _monitoringEngine.RefreshNowAsync();
        }

        private void ToggleApplicationMonitoring(string applicationName)
        {
            _currentConfig = _configManager.Update(config =>
            {
                MonitoredApplication application = config.Applications.FirstOrDefault(app => string.Equals(app.Name, applicationName, StringComparison.OrdinalIgnoreCase));
                if (application != null)
                {
                    application.MonitoringEnabled = !application.MonitoringEnabled;
                }

                return config;
            });

            _monitoringEngine.ApplyConfiguration(_currentConfig);
            BuildServiceMenu();
            UpdateMenuFromStatuses();
            _ = _monitoringEngine.RefreshNowAsync();
        }

        private void RestartApplication(string applicationName)
        {
            if (ApplicationArguments.SafeMode)
            {
                Logger.Write($"Safe mode prevents restart operations for {applicationName}.");
                return;
            }

            MonitoredApplication application = _currentConfig.Applications.FirstOrDefault(app => string.Equals(app.Name, applicationName, StringComparison.OrdinalIgnoreCase));
            if (application == null)
            {
                Logger.Write($"Restart requested for unknown application {applicationName}.");
                return;
            }

            application = application.Clone();

            bool skipService = false;
            if (!string.IsNullOrWhiteSpace(application.ServiceName))
            {
                if (_processOnlyConsent.Contains(application.ServiceName))
                {
                    skipService = true;
                }
                else if (_serviceRequiresElevation.TryGetValue(application.ServiceName, out bool requiresElevation) && requiresElevation && !_promptShown.Contains(application.ServiceName))
                {
                    DialogResult result = MessageBox.Show(
                        $"Administrator rights needed to control service {application.ServiceName}. Continue anyway with process-only restart?",
                        "Administrator rights required",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    _promptShown.Add(application.ServiceName);
                    if (result == DialogResult.Yes)
                    {
                        _processOnlyConsent.Add(application.ServiceName);
                        skipService = true;
                    }
                    else
                    {
                        Logger.Write($"User cancelled restart for {application.Name} due to insufficient privileges.");
                        return;
                    }
                }
            }

            ServiceRestartResult serviceResult = ServiceRestartResult.Skipped;
            if (!skipService)
            {
                serviceResult = _serviceManager.RestartService(application.ServiceName);
                if (serviceResult.Succeeded)
                {
                    Logger.Write($"Service restart succeeded for {application.Name}.");
                    _serviceRequiresElevation[application.ServiceName] = false;
                    _promptShown.Remove(application.ServiceName);
                    _processOnlyConsent.Remove(application.ServiceName);
                }
                else if (serviceResult.RequiresElevation)
                {
                    Logger.Write($"Service restart failed for {application.Name}: administrator rights required.");
                    _serviceRequiresElevation[application.ServiceName] = true;
                }
                else if (!serviceResult.Skipped)
                {
                    Logger.Write($"Service restart failed for {application.Name}: {serviceResult.Message}");
                }
            }

            bool killed = _processManager.KillProcesses(application.ProcessNames, application.Name);
            if (!string.IsNullOrWhiteSpace(application.ExecutablePath))
            {
                bool started = _processManager.StartProcess(application.ExecutablePath, application.Name);
                if (!started)
                {
                    Logger.Write($"Executable launch failed for {application.Name}.");
                }
            }

            if (!serviceResult.Succeeded && !killed)
            {
                Logger.Write($"Restart completed with warnings for {application.Name}.");
            }

            _ = _monitoringEngine.RefreshNowAsync();
        }

        private void ShowSettings()
        {
            SettingsForm existing = Application.OpenForms.OfType<SettingsForm>().FirstOrDefault();
            if (existing != null)
            {
                existing.BringToFront();
                existing.Focus();
                return;
            }

            var form = new SettingsForm(_configManager.GetSnapshot());
            form.SettingsSaved += ApplyUpdatedConfig;
            form.Show();
            form.Focus();
        }

        private void ApplyUpdatedConfig(WatchdogConfig updatedConfig)
        {
            if (updatedConfig == null)
            {
                return;
            }

            updatedConfig.Normalize();
            WatchdogConfig previous = _currentConfig.Clone();
            _currentConfig = _configManager.Update(_ => updatedConfig);
            LogConfigurationChanges(previous, _currentConfig);
            _monitoringEngine.ApplyConfiguration(_currentConfig);
            BuildServiceMenu();
            UpdateMenuFromStatuses();
            _ = _monitoringEngine.RefreshNowAsync();
        }

        private static void LogConfigurationChanges(WatchdogConfig previous, WatchdogConfig updated)
        {
            if (previous.Interval.Value != updated.Interval.Value || previous.Interval.Unit != updated.Interval.Unit)
            {
                Logger.Write($"Interval updated to {updated.Interval.Value} {updated.Interval.Unit}");
            }

            if (previous.AutoStart != updated.AutoStart)
            {
                Logger.Write(updated.AutoStart ? "Enabled run at startup" : "Disabled run at startup");
            }

            if (previous.GlobalMonitoringEnabled != updated.GlobalMonitoringEnabled)
            {
                Logger.Write(updated.GlobalMonitoringEnabled ? "Global monitoring enabled" : "Global monitoring disabled");
            }

            var previousApps = new Dictionary<string, MonitoredApplication>(StringComparer.OrdinalIgnoreCase);
            foreach (MonitoredApplication app in previous.Applications)
            {
                previousApps[app.Name] = app;
            }

            foreach (MonitoredApplication app in updated.Applications)
            {
                if (!previousApps.ContainsKey(app.Name))
                {
                    Logger.Write($"Added application {app.Name}");
                }
            }

            foreach (MonitoredApplication app in previous.Applications)
            {
                if (!updated.Applications.Any(a => string.Equals(a.Name, app.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    Logger.Write($"Removed application {app.Name}");
                }
            }

            foreach (MonitoredApplication app in updated.Applications)
            {
                if (previousApps.TryGetValue(app.Name, out MonitoredApplication previousApp) && previousApp.MonitoringEnabled != app.MonitoringEnabled)
                {
                    Logger.Write(app.MonitoringEnabled ? $"Enabled monitoring for {app.Name}" : $"Disabled monitoring for {app.Name}");
                }
            }
        }

        private void ExitApplication()
        {
            ExitThread();
        }

        protected override void ExitThreadCore()
        {
            if (_disposed)
            {
                base.ExitThreadCore();
                return;
            }

            _disposed = true;
            _monitoringEngine.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _baseIcon.Dispose();

            foreach (Icon icon in _trayIcons.Values)
            {
                icon.Dispose();
            }
            _trayIcons.Clear();

            foreach (Image image in _statusImages.Values)
            {
                image.Dispose();
            }
            _statusImages.Clear();

            if (ReferenceEquals(s_current, this))
            {
                s_current = null;
            }

            base.ExitThreadCore();
        }

        private static Icon LoadBaseIcon()
        {
            string icoPath = Path.Combine(AppContext.BaseDirectory, "Resources", "icon.ico");
            if (File.Exists(icoPath))
            {
                return new Icon(icoPath);
            }

            string pngPath = Path.Combine(AppContext.BaseDirectory, "Resources", "icon.png");
            if (File.Exists(pngPath))
            {
                using Bitmap bitmap = new Bitmap(pngPath);
                return CreateIconFromBitmap(bitmap);
            }

            using Bitmap fallback = new Bitmap(32, 32);
            using (Graphics graphics = Graphics.FromImage(fallback))
            {
                graphics.Clear(Color.Gray);
            }

            return CreateIconFromBitmap(fallback);
        }

        private static Icon CreateIconFromBitmap(Bitmap bitmap)
        {
            IntPtr hIcon = bitmap.GetHicon();
            try
            {
                using Icon temp = Icon.FromHandle(hIcon);
                return (Icon)temp.Clone();
            }
            finally
            {
                NativeMethods.DestroyIcon(hIcon);
            }
        }

        private enum TrayStatus
        {
            Unknown,
            AllHealthy,
            IssueDetected,
            MonitoringDisabled
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool DestroyIcon(IntPtr hIcon);
        }
    }
}
