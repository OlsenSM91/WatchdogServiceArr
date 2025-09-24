# ServiceWatchdogArr

<img width="409" height="179" alt="Screenshot 2025-09-24 011709" src="https://github.com/user-attachments/assets/d3008dd1-1703-49a4-8bb8-e85d8a9517ae" />

ServiceWatchdogArr is a Windows 10/11 tray application that keeps user-selected services and/or processes running. It monitors the configured applications on a schedule, restarts them when required, and exposes configuration and diagnostic tooling directly from the tray icon.

## Key features

- **Single instance** WinForms tray application targeting .NET 9. Launching a second instance focuses the existing session.
- **Emoji status indicators** (🟢/🔴/⚪) in the tray menu showing per-application health.
- **Configurable monitoring interval** (minutes, hours, or days) with global and per-application monitoring toggles.
- **Restart workflow** that stops services, terminates matching processes, and relaunches executables. Safe mode disables restarts entirely.
- **Session logging** with rolling logs (5 MB, seven-day retention) and crash reports written to `%AppData%\ServiceWatchdogArr`.
- **Crash resilience:** unhandled exceptions are captured and persisted (`crash-*.log`). The next launch logs when a crash occurred within the last five minutes.
- **Safe mode (`--safe-mode`)** for read-only monitoring (no service/process control) suitable for diagnostics.
- **HKCU Run key integration** to optionally launch at Windows startup without elevation.

## Configuration

- Configuration is stored per-user at `%AppData%\ServiceWatchdogArr\appsettings.json`.
- The schema matches the sample `appsettings.json` in the repository:
  ```json
  {
    "WatchdogConfig": {
      "Interval": { "Value": 5, "Unit": "Minutes" },
      "AutoStart": false,
      "GlobalMonitoringEnabled": true,
      "Applications": [
        {
          "Name": "Docker Desktop",
          "ServiceName": "com.docker.service",
          "ProcessNames": ["Docker Desktop", "Docker Desktop Backend"],
          "ExecutablePath": "C:\\Program Files\\Docker\\Docker\\Docker Desktop.exe",
          "MonitoringEnabled": true
        }
      ]
    }
  }
  ```
- The Settings window allows adding, editing, and removing applications. The add/edit dialog can discover currently running Windows services and processes, or you can enter details manually.

## Usage

1. Launch `ServiceWatchdogArr.exe`. The tray icon appears using `Resources/icon.ico` and immediately begins monitoring using the configured interval.
2. Right-click the tray icon to access:
   - **Services** submenu for each monitored application with `Restart` and `Enable/Disable Monitoring` actions.
   - **Global Monitoring Enabled** toggle for pausing all monitoring.
   - **Refresh Now** to force an immediate status check.
   - **Open Logs** to view `%AppData%\ServiceWatchdogArr\watchdog.log` in the shell default handler.
   - **Settings…** for interval/autostart configuration and application management.
   - **Exit** to shut down the tray application.
3. The tray tooltip reports the aggregate state (`All OK`, `Issue detected`, or `Monitoring off`). When safe mode is active the tooltip includes “Safe Mode”.
4. Run with `--safe-mode` to disable service/process restarts, termination, and launching. Menu actions remain visible but restart is disabled.

## Logging & diagnostics

- Operational log: `%AppData%\ServiceWatchdogArr\watchdog.log` (rotated at 5 MB, archives retained for seven days).
- Crash logs: `%AppData%\ServiceWatchdogArr\crash-*.log` plus `last-crash.txt` for the most recent crash timestamp.
- Use the tray **Open Logs** command to jump directly to the log file.

## Building

1. Install the .NET 9 SDK (Windows).
2. Restore and build:
   ```powershell
   dotnet build -c Release
   ```
3. Run locally:
   ```powershell
   dotnet run --project ServiceWatchdogArr.csproj
   ```
4. Create a single-file portable publish (recommended for distribution):
   ```powershell
   dotnet publish ServiceWatchdogArr.csproj -c Release -r win-x64 \
       -p:PublishSingleFile=true -p:SelfContained=true \
       -p:IncludeAllContentForSelfExtract=true -p:PublishTrimmed=false
   ```
   Package the publish output as a ZIP alongside the default `appsettings.json` template and resources.

## WinGet manifest

The repository includes a template at `packaging/winget/manifest.yaml`. Before publishing:

1. Update `Version`, `InstallerUrl`, and `InstallerSha256` with the artifacts for the current release.
2. Submit the manifest to the [winget-pkgs](https://github.com/microsoft/winget-pkgs) repository following the WinGet contribution guidelines.

## License

ServiceWatchdogArr is released under the [MIT License](LICENSE).
