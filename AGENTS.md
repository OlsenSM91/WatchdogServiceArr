# AGENTS.MD — ServiceWatchdogArr

**Goal:** Build a production-ready Windows tray application that monitors user-selected apps (services and/or processes), restarts them when needed, and provides a clear, configurable UX. Target **.NET 9 + WinForms** on **Windows 10/11**.

We value: correctness, reliability, security, performance, and maintainability. Minimal surprises. No guesswork.

---

## 1) High-Level Requirements

* **Platform:** Windows 10/11 desktop, per-user tray app (WinForms).
* **.NET:** .NET 9. **No nullable context** (nullable disabled). **Treat warnings as errors**.
* **Single instance:** Enforce via global mutex; second launch should focus existing instance.
* **Tray UX:**

  * Static tray icon (`Resources/icon.ico` set as ApplicationIcon; tray shows `Resources/icon.png` rendered as icon).
  * Context menu includes:

    * **Services** submenu with each configured app:

      * Status indicator prefix: **🟢 running**, **🔴 not running**, **⚪ monitoring disabled**.
      * Actions: **Restart**, **Enable/Disable Monitoring** (per app).
    * **Refresh Now** (force immediate status check).
    * **Open Logs** (opens `%AppData%\ServiceWatchdogArr\watchdog.log`).
    * **Settings…** (opens settings form).
    * **Exit**.
  * Summary tooltip text reflects state: “All OK” (all monitored healthy), “Issue detected” (any monitored unhealthy), “Monitoring off” (global or per-app).
* **Monitoring semantics:**

  * An app is **green** if **service OR process** is running. Both states should be **logged** on every check.
  * If both not running/not found → **red**.
  * Per-app monitoring toggle → **gray**.
  * **Docker Desktop:** treat **process-presence as primary**; service (if any) is optional/fallback in logs only.
* **Restart semantics (per app):**

  1. If ServiceName is set → attempt **service stop** (if running) + **service start**.
  2. Regardless of service outcome, **terminate matching processes** (best-effort).
  3. If ExecutablePath is set → **start the executable** (non-elevated).

  * If service control fails due to privileges, log “admin required”; do not auto-elevate (see Security).
  * For user apps (Radarr/Sonarr/Plex/iCUE/Docker Desktop), this sequence should suffice without admin.
* **Settings:**

  * **Interval** = Value + Unit (**Minutes/Hours/Days**), min **1 minute**, max **7 days**.
  * **Run at Windows startup** (HKCU Run key).
  * **Global Monitoring Enabled** toggle (remember per session; also exposed in tray menu).
* **Add/Remove Apps UI:**

  * **UI to add monitored items**:

    * Wizard/modal allows user to **select from currently running services and processes** (list refreshed on open).
    * For each selection, user sets:

      * **Friendly Name** (default from service display name or process name).
      * **ServiceName** (optional; prefilled if adding from services list).
      * **ProcessName** (auto-detected; allow multiple candidates; drop “.exe”, store normalized).
      * **ExecutablePath** (browse to EXE used for restarts; optional but recommended).
      * **MonitoringEnabled** (default on).
    * Users can **edit** or **remove** apps later from Settings.
* **Persistence (config):**

  * File: `%AppData%\ServiceWatchdogArr\appsettings.json` (per-user).
  * Schema:

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
          // ...
        ]
      }
    }
    ```
  * **Persist per-app monitoring toggles** and **GlobalMonitoringEnabled**.
  * Maintain **backward compatibility** if older files lack new fields (sensible defaults).
* **Logging:**

  * Path: `%AppData%\ServiceWatchdogArr\watchdog.log`.
  * Rolling: rotate at **5 MB**; keep archives ≤ **7 days**.
  * Log on every check:

    * Each app’s **service status** (if ServiceName set) and **process status**.
    * Any restart attempt outcome (success/failure + reason).
    * Settings changes (interval, autostart, monitoring toggles).
  * Provide **Open Logs** menu action.
  * **No Windows toast** notifications.
* **Crash handling & Safe-Mode:**

  * CLI: `--safe-mode` → **no restarts, no service control, no process kills**; read-only monitoring/logging and UI.
  * Unhandled exception handlers:

    * `Application.ThreadException` and `AppDomain.CurrentDomain.UnhandledException` log stack traces to log and write a timestamped `crash-*.log` in the same folder.
  * On next startup, if the prior session crashed within last 5 minutes, show “Previous crash detected” in logs (no modal UI).
* **Refresh Now:** forces immediate monitoring cycle and UI refresh.
* **Packaging:**

  * **Portable ZIP** (preferred).
  * **WinGet** manifests included under `packaging/winget/` (instructions below).
  * **Single-file publish** optional profile (self-contained), mindful that WinForms may extract temporary files; handle resources appropriately.
* **Security:**

  * Run as standard user.
  * **No automatic UAC elevation.** When service control throws AccessDenied, log **“admin required”**; if the user attempts a restart again during the same session show a one-time prompt **“Administrator rights needed to control service {X}. Continue anyway with process-only restart?”**. Remember choice for the session only.

---

## 2) Architecture & Components

**Solution layout (keep simple, best practices):**

```
ServiceWatchdogArr/
  ServiceWatchdogArr.csproj
  Program.cs
  WatchdogApplicationContext.cs
  Settings/
    SettingsForm.cs
    SettingsForm.Designer.cs
    AddAppDialog.cs
    AddAppDialog.Designer.cs
    EditAppDialog.cs
    EditAppDialog.Designer.cs
  Core/
    MonitoringEngine.cs
    StatusEvaluator.cs
    ProcessManager.cs
    ServiceManager.cs
    AppModel.cs
    ConfigManager.cs
    SingleInstance.cs
    CrashReporter.cs
  Utils/
    Logger.cs
    AutoStartHelper.cs
    Paths.cs
  Resources/
    icon.png
    icon.ico
  packaging/
    winget/
      manifest.yaml (versioned per release)
  docs/
    AGENTS.MD (this file)
    README.md
    CHANGELOG.md
```

**Key responsibilities:**

* `Program.cs`

  * Parse CLI flags (`--safe-mode`), set globals, attach crash handlers, enforce single instance, start `ApplicationContext`.
* `SingleInstance.cs`

  * Global mutex `Global\ServiceWatchdogArr`; on second instance, bring existing to foreground (use a named event or remoting pipe to signal “show context menu”).
* `CrashReporter.cs`

  * Wire `Application.ThreadException` + `AppDomain.UnhandledException`, write `crash-YYYYMMDD_HHMMSS.log`.
* `Paths.cs`

  * Centralize `%AppData%\ServiceWatchdogArr` resolution; create dirs as needed.
* `ConfigManager.cs`

  * Strongly-typed config load/save w/ defaults and non-breaking upgrades; file watchers to reload on external edits.
* `AppModel.cs`

  * Defines `ApplicationEntry` (Name, ServiceName, List<string> ProcessNames, ExecutablePath, MonitoringEnabled).
  * Defines `WatchdogConfig` (Interval, AutoStart, GlobalMonitoringEnabled, List<ApplicationEntry> Applications).
* `ServiceManager.cs`

  * Service status, stop/start, timeouts, exception mapping; no elevation logic (log + return codes).
* `ProcessManager.cs`

  * Normalize process names (strip `.exe`, case-insensitive). Enumerate, kill (best-effort w/ WaitForExit). Start executable.
* `MonitoringEngine.cs`

  * Timer (System.Threading.Timer). On tick: for each app → gather **service status** (if ServiceName present) + **process status**.
  * `StatusEvaluator` decides overall **Running** (service OR process), and returns indicator.
  * Respect **GlobalMonitoringEnabled** and per-app toggle.
  * Fire events for UI update; write logs.
* `WatchdogApplicationContext.cs`

  * Owns tray icon + context menu.
  * Displays Services submenu with indicator + actions.
  * “Restart” invokes `MonitoringEngine.Restart(app)`.
  * “Refresh Now” invokes immediate check.
  * “Settings…” opens `SettingsForm`.
* `SettingsForm + Add/Edit dialogs`

  * Interval (Value+Unit) validation (1 min ≤ interval ≤ 7 days).
  * Autostart toggle.
  * App list (grid) with Add/Edit/Remove.
  * **AddAppDialog** can import from current **Services** and **Processes**; prefill fields; allow browsing to EXE (OpenFileDialog).
  * Persist to config; on OK, propagate to engine (no restart required).

---

## 3) Detailed Behaviors

### 3.1 Monitoring & Status

* **Check interval:** convert `Value + Unit` to milliseconds. Enforce min 1 min, max 7 days.
* **Safe-mode:** if enabled, the engine never calls restart, service stop/start, or kill processes. Buttons are disabled but status is still visible.
* **Indicator calculation:**

  * If global monitoring disabled OR app MonitoringEnabled == false → **⚪**.
  * Else if `ServiceRunning || ProcessRunning` → **🟢**.
  * Else → **🔴**.

### 3.2 Restart flow (non-safe mode)

1. If app has `ServiceName`, attempt `Stop()` with timeout (e.g., 30s). Log status transitions.
2. Kill any matching processes (best-effort; catch & log per process).
3. If app has `ServiceName`, attempt `Start()` with timeout (30s).
4. If app has `ExecutablePath`, `Process.Start(exe)`; log path and PID if available.
5. Re-check status after a small delay (e.g., 1–2 seconds) and log outcome.

### 3.3 Admin/privilege handling

* No UAC auto-elevation.
* On AccessDenied controlling a service: log **“admin required for {service}”**.
* If the user selects Restart again for the same app during this session, show a **single** message box:

  * “Administrator rights are required to control service '{X}'. Continue anyway with process-only restart?”
  * Remember answer until app exit (in-memory).

### 3.4 Logging

* Always include timestamps and app identifiers.
* Example lines on tick:

  ```
  [2025-09-23 20:10:54] Radarr: Service=Running, Process=Running → Overall=Running
  [2025-09-23 20:10:54] Docker Desktop: Service=Stopped (admin required?), Process=Running → Overall=Running
  ```
* On Restart:

  ```
  [..] Restart requested for 'Docker Desktop'
  [..] Stopped service com.docker.service (00:03.5)
  [..] Killed 2 processes: ["Docker Desktop" (pid 1234), "Docker Desktop Backend" (pid 5678)]
  [..] Started: "C:\Program Files\Docker\Docker\Docker Desktop.exe" (pid 91011)
  [..] Post-restart status: Service=Stopped, Process=Running → Overall=Running
  ```

---

## 4) UX Details

* **Services submenu**:

  * Build from `config.Applications` order (preserve user order; allow reordering in UI).
  * Each item text: `{dot} {Name}`. Submenu:

    * `Restart`
    * `Disable Monitoring` / `Enable Monitoring` (reflect current state)
* **Global menu:**

  * `Refresh Now`
  * `Open Logs`
  * `Settings…`
  * `Exit`
* **SettingsForm**:

  * Tab “General”: Interval (numeric up/down + unit combobox), Run at Startup checkbox, Global Monitoring checkbox.
  * Tab “Applications”: list (Name, ServiceName, ProcessNames, ExecutablePath, MonitoringEnabled), with Add/Edit/Remove.
  * `AddAppDialog`:

    * Two pickers: **Running Services** (DisplayName + ServiceName), **Running Processes** (ProcessName), with search.
    * Text inputs: Friendly Name, ExecutablePath (Browse…), checkboxes for MonitoringEnabled.
    * Multi-process support: allow adding multiple process names for one app (e.g., Docker spawns multiple).
* **Validation**:

  * Interval bounds enforced.
  * ExecutablePath should be an absolute path to `.exe` if provided; if not provided, we still allow monitoring/restart (service only or process-kill-only).
  * ServiceName/process names are optional but at least one must exist to be useful.

---

## 5) Config & Compatibility

* On load, if `Applications` missing, initialize empty list.
* If `GlobalMonitoringEnabled` missing, default true.
* For older flat config formats, load and map to the new schema (graceful fallback).

---

## 6) Build, Packaging, & Publishing

* **Project settings (`.csproj`):**

  * `<TargetFramework>net9.0-windows</TargetFramework>`
  * `<UseWindowsForms>true</UseWindowsForms>`
  * `<Nullable>disable</Nullable>` (**explicitly disable nullable**)
  * `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
  * Add **Microsoft.Windows.Compatibility** 9.0.0
  * `ApplicationIcon` → `Resources/icon.ico`
* **Single-file publish profile (optional):**

  * `/p:PublishSingleFile=true /p:SelfContained=true /p:IncludeAllContentForSelfExtract=true /p:PublishTrimmed=false`
  * Rationale: WinForms apps often rely on reflection; avoid trimming errors.
* **Portable ZIP:**

  * Output folder contains the single EXE + `appsettings.json` template + `Resources` folder (if not embedded).
* **WinGet:**

  * Provide `packaging/winget/manifest.yaml` with:

    * `Id: YourPublisher.ServiceWatchdogArr`
    * `Publisher`, `Name`, `Version`
    * `License: MIT`
    * `Installers`: portable ZIP URL + SHA256
    * Instructions in README for updating manifest and submitting to winget-pkgs repo.

---

## 7) Testing & Validation

**Unit tests** (if added later):

* `StatusEvaluatorTests`: service-only, process-only, both, none → correct dot.
* `IntervalConversionTests`: minute/hour/day bounds and clamping.
* `ProcessNameNormalizerTests`: `.exe` stripping, case-insensitivity.
* `ConfigManagerTests`: defaulting, upgrade from older schema, persistence.

**Manual test checklist:**

* First run with no config → settings defaults; add an app via UI.
* Verify **gray** state when per-app monitoring disabled and when global monitoring disabled.
* Verify **green** when service or process is up (e.g., Docker running without service).
* Kill app → **red**; click **Restart** → app comes back if ExecutablePath is set/service runs.
* **Refresh Now** forces immediate update.
* Change interval → observe that checks occur at new cadence without restart.
* Toggle **Run at Startup** → confirm HKCU Run entry created/removed.
* Crash test: throw an unhandled exception (debug build) → `crash-*.log` created; next start logs prior crash.
* `--safe-mode` → Restart and kill operations do nothing; menu items disabled or no-ops; status still visible.

---

## 8) Performance & Reliability

* Use **System.Threading.Timer** for background checks.
* Marshal back to UI thread using `SynchronizationContext.Post` for menu updates.
* Debounce menu rebuilds; update only labels for items rather than rebuilding the entire menu each tick.
* Avoid blocking the UI thread; service/process checks on a worker thread.
* Timeouts: service stop/start 30s, process kill wait 10s.

---

## 9) Security & Privacy

* No network activity. No telemetry.
* Log files contain only local operational data (service/process names, paths if user provided).
* Respect user session boundaries; no writing outside `%AppData%\ServiceWatchdogArr`.

---

## 10) Coding Standards

* **Nullable disabled**.
* **Treat warnings as errors**.
* Use analyzers (CA\* rules) at recommended level; fix violations.
* Small, focused classes; no god objects.
* Comprehensive XML doc comments on public types/methods; meaningful summaries on complex methods.
* Clear exception handling and logging; no silent failures except where explicitly documented (e.g., best-effort process kill).

---

## 11) Implementation Tasks (ordered)

1. **Scaffold solution** and folders per structure above; wire `.csproj` with settings & package references.
2. Implement `Paths`, `Logger` (rolling), `CrashReporter`, `SingleInstance`.
3. Implement `AppModel` & `ConfigManager` with schema + migration defaults.
4. Implement `ServiceManager` and `ProcessManager` (status, stop/kill/start helpers).
5. Implement `StatusEvaluator` and `MonitoringEngine` (timer + events).
6. Implement `WatchdogApplicationContext`:

   * Load config, create tray icon, build menu, wire **Refresh Now**, **Open Logs**, **Settings**, **Exit**.
   * Subscribe to engine events; update menu labels (🟢/🔴/⚪).
7. Implement **SettingsForm** (+ **AddAppDialog**, **EditAppDialog**):

   * General tab: interval bounds, autostart, global monitoring.
   * Applications tab: grid + add/edit/remove; add uses running services/processes.
8. Wire **Restart** action with proper sequence + safe-mode guard + privilege handling.
9. Final pass: global UX polish, tooltips, error messages, log clarity.
10. Build **portable zip**; create **winget manifest** template and docs.

---

## 12) Definition of Done

* Builds on .NET 9 with **warnings as errors**, **nullable disabled**.
* Runs single instance; tray menu functional; settings persist; restart flow proven against at least two real apps (e.g., Radarr + Docker).
* Logging verifies detection logic (service+process).
* Interval limits enforced; safe-mode works; crash logs produced on forced crash in debug.
* Portable zip produced; winget manifests drafted.
* README documents usage, flags, config, and troubleshooting.
* License = **MIT** included.

---

## 13) Nice-to-Have (deferred unless requested)

* Export/import monitored apps set.
* Optional passwordless “run as admin” bridge (task scheduler trick).
* Integration tests with a fake “test service” + mock processes.

---

## 14) Known Edge Cases & Policies

* Some apps spawn transient helper processes; allow multiple ProcessNames and consider RUNNING if **any** is present.
* If both ServiceName and ExecutablePath are unset, app can still be monitored via ProcessNames only.
* Service control failures due to permissions are **non-fatal**; we prefer process-only restarts (after explicit user consent during the session).

---
