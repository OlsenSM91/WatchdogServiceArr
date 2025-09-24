using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Windows.Forms;

namespace ServiceWatchdogArr
{
    public class WatchdogApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon trayIcon;
        private readonly System.Windows.Forms.Timer checkTimer;
        private WatchdogConfig config;
        private List<WatchedApplication> applications = new();
        private readonly Dictionary<string, bool> monitoringEnabled = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AppStatusSnapshot> lastStatuses = new(StringComparer.OrdinalIgnoreCase);
        private readonly Icon baseIcon;
        private readonly Dictionary<TrayStatus, Icon> trayStatusIcons = new();
        private TrayStatus currentTrayStatus = TrayStatus.Unknown;

        public WatchdogApplicationContext()
        {
            LoadConfiguration();

            baseIcon = LoadBaseIcon();

            trayIcon = new NotifyIcon
            {
                Icon = baseIcon,
                Visible = true,
                Text = "ServiceWatchdogArr: Initializing"
            };

            checkTimer = new System.Windows.Forms.Timer
            {
                Interval = GetIntervalMilliseconds(config.IntervalMinutes)
            };
            checkTimer.Tick += (_, _) => UpdateStatus();
            checkTimer.Start();

            BuildContextMenu();
            UpdateStatus();
        }

        private static Icon LoadBaseIcon()
        {
            string pngPath = Path.Combine(AppContext.BaseDirectory, "Resources", "icon.png");
            if (File.Exists(pngPath))
            {
                using var bmp = new Bitmap(pngPath);
                return CreateIconFromBitmap(bmp);
            }

            string icoPath = Path.Combine(AppContext.BaseDirectory, "Resources", "icon.ico");
            if (File.Exists(icoPath))
            {
                return new Icon(icoPath);
            }

            using var fallback = new Bitmap(32, 32);
            using var g = Graphics.FromImage(fallback);
            g.Clear(Color.Gray);
            using var pen = new Pen(Color.Black, 2);
            g.DrawEllipse(pen, new Rectangle(4, 4, 24, 24));
            return CreateIconFromBitmap(fallback);
        }

        private static int GetIntervalMilliseconds(int minutes)
        {
            long ms = Math.Max(1, minutes) * 60000L;
            return (int)Math.Min(int.MaxValue, ms);
        }

        private void LoadConfiguration()
        {
            config = WatchdogConfig.Load();
            applications = config.Applications ?? new List<WatchedApplication>();

            var names = new HashSet<string>(applications.Select(a => a.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var key in monitoringEnabled.Keys.Where(k => !names.Contains(k)).ToList())
            {
                monitoringEnabled.Remove(key);
            }

            foreach (var key in lastStatuses.Keys.Where(k => !names.Contains(k)).ToList())
            {
                lastStatuses.Remove(key);
            }

            foreach (var app in applications)
            {
                if (!monitoringEnabled.ContainsKey(app.Name))
                    monitoringEnabled[app.Name] = true;
            }
        }

        private void BuildContextMenu()
        {
            trayIcon.ContextMenuStrip?.Dispose();

            ContextMenuStrip menu = new ContextMenuStrip();

            if (applications.Count > 0)
            {
                ToolStripMenuItem servicesMenu = new ToolStripMenuItem("Services") { Tag = "servicesMenu" };
                foreach (var app in applications)
                {
                    ToolStripMenuItem svcMenu = new ToolStripMenuItem(app.Name) { Tag = app };

                    var restartItem = new ToolStripMenuItem("Restart");
                    restartItem.Click += (s, e) => RestartApplication(app);
                    svcMenu.DropDownItems.Add(restartItem);

                    var toggleItem = new ToolStripMenuItem("Disable Monitoring") { Tag = "toggle" };
                    toggleItem.Click += (s, e) => ToggleMonitoring(app);
                    svcMenu.DropDownItems.Add(toggleItem);

                    servicesMenu.DropDownItems.Add(svcMenu);
                }
                menu.Items.Add(servicesMenu);
                menu.Items.Add(new ToolStripSeparator());
            }

            menu.Items.Add(new ToolStripMenuItem("Open Logs", null, (s, e) => Logger.OpenLog()));

            ToolStripMenuItem settingsItem = new ToolStripMenuItem("Settings");
            settingsItem.Click += (s, e) =>
            {
                using var settings = new SettingsForm();
                if (settings.ShowDialog() == DialogResult.OK)
                {
                    LoadConfiguration();
                    checkTimer.Interval = GetIntervalMilliseconds(config.IntervalMinutes);
                    Logger.Write($"Interval updated to {config.Interval.Value} {config.Interval.Unit}");
                    BuildContextMenu();
                    UpdateStatus();
                }
            };
            menu.Items.Add(settingsItem);

            menu.Items.Add(new ToolStripMenuItem("Exit", null, (s, e) => ExitThread()));
            menu.Opening += (s, e) => UpdateStatus();

            trayIcon.ContextMenuStrip = menu;
        }

        private void UpdateStatus()
        {
            ContextMenuStrip? menu = trayIcon.ContextMenuStrip;

            if (menu != null && menu.InvokeRequired)
            {
                menu.BeginInvoke(new MethodInvoker(UpdateStatus));
                return;
            }

            bool anyMonitoring = false;
            bool anyRunning = false;
            bool allRunning = true;

            foreach (var app in applications)
            {
                bool enabled = monitoringEnabled.GetValueOrDefault(app.Name, true);
                var status = GetApplicationStatus(app);

                UpdateStatusLog(app, enabled, status);

                if (menu != null)
                {
                    UpdateMenuItem(menu, app, enabled, status);
                }

                if (enabled)
                {
                    anyMonitoring = true;
                    if (status.IsRunning)
                        anyRunning = true;
                    else
                        allRunning = false;
                }
            }

            UpdateTraySummary(anyMonitoring, allRunning, anyRunning);
        }

        private void UpdateMenuItem(ContextMenuStrip menu, WatchedApplication app, bool enabled, ApplicationStatus status)
        {
            foreach (ToolStripMenuItem item in menu.Items.OfType<ToolStripMenuItem>())
            {
                if (!string.Equals(item.Tag as string, "servicesMenu", StringComparison.Ordinal))
                    continue;

                foreach (ToolStripMenuItem svcItem in item.DropDownItems.OfType<ToolStripMenuItem>())
                {
                    if (!ReferenceEquals(svcItem.Tag, app))
                        continue;

                    string indicator = enabled ? (status.IsRunning ? "🟢" : "🔴") : "⚪";
                    svcItem.Text = $"{indicator} {app.Name}";

                    var toggleItem = svcItem.DropDownItems.OfType<ToolStripMenuItem>()
                        .FirstOrDefault(i => string.Equals(i.Tag as string, "toggle", StringComparison.Ordinal));
                    if (toggleItem != null)
                        toggleItem.Text = enabled ? "Disable Monitoring" : "Enable Monitoring";

                    return;
                }
            }
        }

        private void UpdateStatusLog(WatchedApplication app, bool enabled, ApplicationStatus status)
        {
            if (!lastStatuses.TryGetValue(app.Name, out var previous) ||
                previous.MonitoringEnabled != enabled ||
                previous.ServiceRunning != status.ServiceRunning ||
                previous.ProcessRunning != status.ProcessRunning ||
                !string.Equals(previous.ServiceError, status.ServiceError, StringComparison.Ordinal))
            {
                if (enabled)
                {
                    Logger.Write($"{app.Name} service status: {(status.ServiceRunning ? "Running" : "Stopped")}");
                    Logger.Write($"{app.Name} process status: {(status.ProcessRunning ? "Running" : "Stopped")}");

                    if (!string.IsNullOrWhiteSpace(app.ServiceName))
                    {
                        if (status.ServiceError != null)
                        {
                            Logger.Write($"{app.Name} service query error ({app.ServiceName}): {status.ServiceError}");
                        }
                        else if (!string.IsNullOrEmpty(previous?.ServiceError))
                        {
                            Logger.Write($"{app.Name} service query restored ({app.ServiceName})");
                        }
                    }
                }

                lastStatuses[app.Name] = new AppStatusSnapshot
                {
                    MonitoringEnabled = enabled,
                    ServiceRunning = status.ServiceRunning,
                    ProcessRunning = status.ProcessRunning,
                    ServiceError = status.ServiceError
                };
            }
        }

        private void UpdateTraySummary(bool anyMonitoring, bool allRunning, bool anyRunning)
        {
            TrayStatus status;
            string tooltip;

            if (!anyMonitoring)
            {
                status = TrayStatus.MonitoringDisabled;
                tooltip = "ServiceWatchdogArr: Monitoring paused";
            }
            else if (allRunning && anyRunning)
            {
                status = TrayStatus.AllRunning;
                tooltip = "ServiceWatchdogArr: All services OK";
            }
            else
            {
                status = TrayStatus.IssueDetected;
                tooltip = "ServiceWatchdogArr: Attention required";
            }

            SetTrayStatus(status, tooltip);
        }

        private void SetTrayStatus(TrayStatus status, string tooltip)
        {
            if (status != currentTrayStatus)
            {
                trayIcon.Icon = GetStatusIcon(status);
                currentTrayStatus = status;
            }

            if (!string.Equals(trayIcon.Text, tooltip, StringComparison.Ordinal))
            {
                trayIcon.Text = tooltip;
            }
        }

        private Icon GetStatusIcon(TrayStatus status)
        {
            if (status == TrayStatus.Unknown)
                return baseIcon;

            if (!trayStatusIcons.TryGetValue(status, out var icon))
            {
                icon = status switch
                {
                    TrayStatus.AllRunning => CreateStatusIcon(Color.LimeGreen, Color.SeaGreen),
                    TrayStatus.IssueDetected => CreateStatusIcon(Color.OrangeRed, Color.Maroon),
                    TrayStatus.MonitoringDisabled => CreateStatusIcon(Color.LightGray, Color.DimGray),
                    _ => baseIcon
                };
                trayStatusIcons[status] = icon;
            }

            return icon;
        }

        private static Icon CreateStatusIcon(Color fill, Color border)
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var rect = new Rectangle(4, 4, 24, 24);
            using (var brush = new SolidBrush(fill))
            {
                g.FillEllipse(brush, rect);
            }

            using (var pen = new Pen(border, 3))
            {
                g.DrawEllipse(pen, rect);
            }

            return CreateIconFromBitmap(bmp);
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

        private void ToggleMonitoring(WatchedApplication app)
        {
            bool current = monitoringEnabled.GetValueOrDefault(app.Name, true);
            monitoringEnabled[app.Name] = !current;
            Logger.Write($"{(monitoringEnabled[app.Name] ? "Enabled" : "Disabled")} monitoring for {app.Name}");
            UpdateStatus();
        }

        private void RestartApplication(WatchedApplication app)
        {
            try
            {
                bool serviceRestarted = TryRestartService(app);
                bool processesTerminated = serviceRestarted;

                if (!serviceRestarted)
                {
                    processesTerminated = TerminateProcesses(app.ProcessName, app.Name);
                }

                var afterStopStatus = GetApplicationStatus(app);

                bool processStarted = false;
                if (!afterStopStatus.ProcessRunning && !string.IsNullOrWhiteSpace(app.ExecutablePath))
                {
                    processStarted = TryStartExecutable(app);
                }

                var finalStatus = GetApplicationStatus(app);

                if (serviceRestarted)
                {
                    Logger.Write($"Restarted service for {app.Name}");
                }

                if (processStarted)
                {
                    if (finalStatus.ProcessRunning)
                    {
                        Logger.Write($"Restarted process for {app.Name}");
                    }
                    else
                    {
                        Logger.Write($"Process restart requested for {app.Name}, but it is not running yet");
                    }
                }
                else if (!serviceRestarted && !processesTerminated && finalStatus.IsRunning)
                {
                    Logger.Write($"{app.Name} is already running");
                }

                if (!finalStatus.IsRunning)
                {
                    throw new InvalidOperationException($"Unable to restart {app.Name}");
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Failed to restart {app.Name}: {ex.Message}");
                MessageBox.Show($"Failed to restart {app.Name}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UpdateStatus();
            }
        }

        private bool TryRestartService(WatchedApplication app)
        {
            if (string.IsNullOrWhiteSpace(app.ServiceName))
                return false;

            try
            {
                using var sc = new ServiceController(app.ServiceName);
                sc.Refresh();

                if (sc.Status == ServiceControllerStatus.StopPending)
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));

                if (sc.Status != ServiceControllerStatus.Stopped)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }

                TerminateProcesses(app.ProcessName, app.Name);

                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                return true;
            }
            catch (Exception ex)
            {
                Logger.Write($"Service restart failed for {app.Name} ({app.ServiceName}): {ex.Message}");
                return false;
            }
        }

        private static bool TerminateProcesses(string? processName, string appName)
        {
            bool terminatedAny = false;

            foreach (var proc in FindProcesses(processName))
            {
                try
                {
                    Logger.Write($"Stopping process {proc.ProcessName} for {appName}");
                    proc.Kill(true);
                    terminatedAny = true;
                    proc.WaitForExit(10000);
                }
                catch (Exception ex)
                {
                    Logger.Write($"Failed to stop process {proc.ProcessName} for {appName}: {ex.Message}");
                }
                finally
                {
                    proc.Dispose();
                }
            }

            return terminatedAny;
        }

        private static bool TryStartExecutable(WatchedApplication app)
        {
            if (string.IsNullOrWhiteSpace(app.ExecutablePath))
                return false;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = app.ExecutablePath,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Write($"Failed to start executable for {app.Name}: {ex.Message}");
                return false;
            }
        }

        private ApplicationStatus GetApplicationStatus(WatchedApplication app)
        {
            bool serviceRunning = false;
            string? serviceError = null;

            if (!string.IsNullOrWhiteSpace(app.ServiceName))
            {
                try
                {
                    using var sc = new ServiceController(app.ServiceName);
                    sc.Refresh();
                    serviceRunning = sc.Status == ServiceControllerStatus.Running ||
                                     sc.Status == ServiceControllerStatus.StartPending;
                }
                catch (Exception ex)
                {
                    serviceError = ex.Message;
                    serviceRunning = false;
                }
            }

            bool processRunning = IsProcessRunning(app.ProcessName);
            return new ApplicationStatus(serviceRunning, processRunning, serviceError);
        }

        private static bool IsProcessRunning(string? processName)
        {
            var processes = FindProcesses(processName);
            try
            {
                return processes.Count > 0;
            }
            finally
            {
                foreach (var proc in processes)
                    proc.Dispose();
            }
        }

        private static List<Process> FindProcesses(string? processName)
        {
            var matches = new List<Process>();
            if (string.IsNullOrWhiteSpace(processName))
                return matches;

            string trimmed = processName.Trim();
            string withoutExtension = Path.GetFileNameWithoutExtension(trimmed);
            string normalizedTarget = NormalizeProcessName(withoutExtension);

            Process[] processes;
            try
            {
                processes = Process.GetProcesses();
            }
            catch
            {
                return matches;
            }

            foreach (var proc in processes)
            {
                try
                {
                    string candidate = NormalizeProcessName(proc.ProcessName);
                    if (string.Equals(proc.ProcessName, withoutExtension, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(candidate, normalizedTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        matches.Add(proc);
                    }
                    else
                    {
                        proc.Dispose();
                    }
                }
                catch
                {
                    proc.Dispose();
                }
            }

            return matches;
        }

        private static string NormalizeProcessName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            string trimmed = name.Trim();

            if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[..^4];

            return string.Concat(trimmed.Where(c => !char.IsWhiteSpace(c)));
        }

        protected override void ExitThreadCore()
        {
            checkTimer.Stop();
            checkTimer.Dispose();

            trayIcon.Visible = false;
            trayIcon.Dispose();

            foreach (var icon in trayStatusIcons.Values)
            {
                icon.Dispose();
            }
            trayStatusIcons.Clear();
            baseIcon.Dispose();

            base.ExitThreadCore();
        }

        private sealed class AppStatusSnapshot
        {
            public bool MonitoringEnabled { get; init; }
            public bool ServiceRunning { get; init; }
            public bool ProcessRunning { get; init; }
            public string? ServiceError { get; init; }
        }

        private readonly struct ApplicationStatus
        {
            public ApplicationStatus(bool serviceRunning, bool processRunning, string? serviceError)
            {
                ServiceRunning = serviceRunning;
                ProcessRunning = processRunning;
                ServiceError = serviceError;
            }

            public bool ServiceRunning { get; }
            public bool ProcessRunning { get; }
            public string? ServiceError { get; }
            public bool IsRunning => ServiceRunning || ProcessRunning;
        }

        private enum TrayStatus
        {
            Unknown,
            MonitoringDisabled,
            AllRunning,
            IssueDetected
        }

        private static class NativeMethods
        {
            [DllImport("user32.dll")]
            public static extern bool DestroyIcon(IntPtr hIcon);
        }
    }
}
