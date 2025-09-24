using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Windows.Forms;

namespace ServiceWatchdogArr
{
    public class WatchdogApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon trayIcon;
        private readonly System.Timers.Timer checkTimer;
        private WatchdogConfig config;
        private List<WatchedApplication> applications = new();
        private readonly Dictionary<string, bool> monitoringEnabled = new(StringComparer.OrdinalIgnoreCase);

        public WatchdogApplicationContext()
        {
            LoadConfiguration();

            // Use PNG for tray (your request); fall back to ICO if PNG missing.
            Icon trayIco;
            try
            {
                using var bmp = new Bitmap(Path.Combine(AppContext.BaseDirectory, "Resources", "icon.png"));
                trayIco = Icon.FromHandle(bmp.GetHicon());
            }
            catch
            {
                trayIco = new Icon(Path.Combine(AppContext.BaseDirectory, "Resources", "icon.ico"));
            }

            trayIcon = new NotifyIcon()
            {
                Icon = trayIco,
                Visible = true,
                Text = "ServiceWatchdogArr"
            };

            checkTimer = new System.Timers.Timer(config.IntervalMinutes * 60000);
            checkTimer.Elapsed += (s, e) => UpdateStatus();
            checkTimer.Start();

            BuildContextMenu();
            UpdateStatus();
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
                    checkTimer.Interval = config.IntervalMinutes * 60000;
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
            if (trayIcon.ContextMenuStrip == null)
                return;

            if (trayIcon.ContextMenuStrip.InvokeRequired)
            {
                trayIcon.ContextMenuStrip.BeginInvoke(new MethodInvoker(UpdateStatus));
                return;
            }

            foreach (ToolStripMenuItem item in trayIcon.ContextMenuStrip.Items.OfType<ToolStripMenuItem>())
            {
                if (!string.Equals(item.Tag as string, "servicesMenu", StringComparison.Ordinal))
                    continue;

                foreach (ToolStripMenuItem svcItem in item.DropDownItems.OfType<ToolStripMenuItem>())
                {
                    if (svcItem.Tag is not WatchedApplication app)
                        continue;

                    bool enabled = monitoringEnabled.GetValueOrDefault(app.Name, true);
                    string indicator;
                    if (!enabled)
                    {
                        indicator = "⚪";
                    }
                    else
                    {
                        bool running = IsApplicationRunning(app);
                        indicator = running ? "🟢" : "🔴";
                    }

                    svcItem.Text = $"{indicator} {app.Name}";

                    var toggleItem = svcItem.DropDownItems.OfType<ToolStripMenuItem>()
                        .FirstOrDefault(i => string.Equals(i.Tag as string, "toggle", StringComparison.Ordinal));
                    if (toggleItem != null)
                        toggleItem.Text = enabled ? "Disable Monitoring" : "Enable Monitoring";
                }
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
                if (!serviceRestarted)
                {
                    TerminateProcesses(app.ProcessName, app.Name);
                }

                var status = GetApplicationStatus(app);
                bool success = false;

                if (serviceRestarted)
                {
                    Logger.Write($"Restarted service for {app.Name}");
                    success = true;
                }

                if (!status.ProcessRunning && !string.IsNullOrWhiteSpace(app.ExecutablePath))
                {
                    if (TryStartExecutable(app))
                    {
                        status = GetApplicationStatus(app);
                        Logger.Write($"Restarted process for {app.Name}");
                        success = true;
                    }
                }
                else if (status.ProcessRunning)
                {
                    Logger.Write($"Restarted process for {app.Name}");
                    success = true;
                }

                if (!success && (status.ServiceRunning || status.ProcessRunning))
                {
                    success = true;
                }

                if (!success)
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

        private static void TerminateProcesses(string? processName, string appName)
        {
            foreach (var proc in FindProcesses(processName))
            {
                try
                {
                    Logger.Write($"Stopping process {proc.ProcessName} for {appName}");
                    proc.Kill(true);
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

        private bool IsApplicationRunning(WatchedApplication app)
        {
            var status = GetApplicationStatus(app);
            return status.ServiceRunning || status.ProcessRunning;
        }

        private (bool ServiceRunning, bool ProcessRunning) GetApplicationStatus(WatchedApplication app)
        {
            bool serviceRunning = false;
            if (!string.IsNullOrWhiteSpace(app.ServiceName))
            {
                try
                {
                    using var sc = new ServiceController(app.ServiceName);
                    sc.Refresh();
                    serviceRunning = sc.Status == ServiceControllerStatus.Running ||
                                     sc.Status == ServiceControllerStatus.StartPending;
                }
                catch
                {
                    serviceRunning = false;
                }
            }

            bool processRunning = IsProcessRunning(app.ProcessName);
            return (serviceRunning, processRunning);
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
            trayIcon.Visible = false;
            trayIcon.Dispose();
            base.ExitThreadCore();
        }
    }
}

