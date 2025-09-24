# ServiceWatchdogArr

Windows tray watchdog for Plex, Radarr, Sonarr, Docker Desktop, iCUE.

- Tray status: green, red, gray.
- Right-click menu: status, enable/disable monitoring, settings, per-app restart, exit.
- Settings: interval minutes/hours/days, run at startup, toggle monitoring.
- Config is stored in `appsettings.json` with root object containing `WatchdogConfig`.

## Build
- Requires .NET 8 SDK on Windows.
- Open `ServiceWatchdogArr.csproj` in Visual Studio, or run:
  ```
  dotnet build -c Release
  dotnet run
  ```

## Notes
- Icons are generated dynamically in memory so no external `.ico` files are required.
- If an application has a `ServiceName`, the app uses ServiceController to start/stop.
- If only a process path is present, the app launches the executable.