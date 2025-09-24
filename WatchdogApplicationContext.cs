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

        private readonly Dictionary<string, string> serviceNames = new()
        {
            { "Plex Media Server", "PlexUpdateService" },
            { "Radarr", "Radarr" },
            { "Sonarr", "Sonarr" }
        };

        private readonly Dictionary<string, string> processNames = new()
        {
            { "Docker Desktop", "Docker Desktop" },
            { "Corsair iCUE", "iCUE" }
        };

        private readonly Dictionary<string, bool> monitoringEnabled = new();

        public WatchdogApplicationContext()
        {
            config = WatchdogConfig.Load();

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

            foreach (var svc in serviceNames.Keys.Concat(processNames.Keys))
                monitoringEnabled[svc] = true;

            checkTimer = new System.Timers.Timer(config.IntervalMinutes * 60000);
            checkTimer.Elapsed += (s, e) => UpdateStatus();
            checkTimer.Start();

            BuildContextMenu();
            UpdateStatus();
        }

        private void BuildContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            // Services submenu
            ToolStripMenuItem servicesMenu = new ToolStripMenuItem("Services");
            foreach (var svc in serviceNames.Keys.Concat(processNames.Keys))
            {
                ToolStripMenuItem svcMenu = new ToolStripMenuItem(svc) { Tag = svc };
                svcMenu.DropDownItems.Add(new ToolStripMenuItem("Restart", null, (s, e) => RestartServiceOrProcess(svc)));
                svcMenu.DropDownItems.Add(new ToolStripMenuItem("Disable Monitoring", null, (s, e) => ToggleMonitoring(svc)));
                servicesMenu.DropDownItems.Add(svcMenu);
            }
            menu.Items.Add(servicesMenu);
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(new ToolStripMenuItem("Open Logs", null, (s, e) => Logger.OpenLog()));
            menu.Items.Add(new ToolStripMenuItem("Settings", null, (s, e) =>
            {
                using var settings = new SettingsForm();
                if (settings.ShowDialog() == DialogResult.OK)
                {
                    config = WatchdogConfig.Load();
                    checkTimer.Interval = config.IntervalMinutes * 60000;
                    Logger.Write($"Interval updated to {config.Interval.Value} {config.Interval.Unit}");
                }
            }));
            menu.Items.Add(new ToolStripMenuItem("Exit", null, (s, e) => ExitThread()));

            trayIcon.ContextMenuStrip = menu;
        }

        private void UpdateStatus()
        {
            foreach (ToolStripMenuItem item in trayIcon.ContextMenuStrip.Items.OfType<ToolStripMenuItem>())
            {
                if (item.Text == "Services")
                {
                    foreach (ToolStripMenuItem svcItem in item.DropDownItems.OfType<ToolStripMenuItem>())
                    {
                        string svcName = svcItem.Tag?.ToString() ?? "";
                        if (string.IsNullOrEmpty(svcName)) continue;

                        if (svcItem.DropDownItems.Count > 0)
                        {
                            bool enabled = monitoringEnabled.GetValueOrDefault(svcName, true);
                            string indicator;

                            if (!enabled)
                            {
                                indicator = "⚪";
                            }
                            else
                            {
                                bool running = false;

                                if (serviceNames.ContainsKey(svcName))
                                {
                                    try
                                    {
                                        using var sc = new ServiceController(serviceNames[svcName]);
                                        running = sc.Status == ServiceControllerStatus.Running;
                                    }
                                    catch { running = false; }
                                }
                                else if (processNames.ContainsKey(svcName))
                                {
                                    running = Process.GetProcessesByName(processNames[svcName]).Any();
                                }

                                indicator = running ? "🟢" : "🔴";
                            }

                            svcItem.Text = $"{indicator} {svcName}";

                            var toggleItem = svcItem.DropDownItems.OfType<ToolStripMenuItem>()
                                .FirstOrDefault(i => i.Text.StartsWith("Enable") || i.Text.StartsWith("Disable"));
                            if (toggleItem != null)
                                toggleItem.Text = enabled ? "Disable Monitoring" : "Enable Monitoring";
                        }
                    }
                }
            }
        }

        private void ToggleMonitoring(string svcName)
        {
            bool current = monitoringEnabled.GetValueOrDefault(svcName, true);
            monitoringEnabled[svcName] = !current;
            Logger.Write($"{(monitoringEnabled[svcName] ? "Enabled" : "Disabled")} monitoring for {svcName}");
            UpdateStatus();
        }

        private void RestartServiceOrProcess(string svcName)
        {
            try
            {
                if (serviceNames.ContainsKey(svcName))
                {
                    using var sc = new ServiceController(serviceNames[svcName]);
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    Logger.Write($"Restarted service: {svcName}");
                }
                else if (processNames.ContainsKey(svcName))
                {
                    foreach (var proc in Process.GetProcessesByName(processNames[svcName]))
                    {
                        try { proc.Kill(true); proc.WaitForExit(); } catch {}
                    }

                    string exePath = svcName == "Docker Desktop"
                        ? @"C:\Program Files\Docker\Docker\Docker Desktop.exe"
                        : @"C:\Program Files\Corsair\Corsair iCUE5 Software\iCUE.exe";

                    try { Process.Start(exePath); } catch {}
                    Logger.Write($"Restarted process: {svcName}");
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Failed to restart {svcName}: {ex.Message}");
                MessageBox.Show($"Failed to restart {svcName}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateStatus();
        }

        protected override void ExitThreadCore()
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
            base.ExitThreadCore();
        }
    }
}