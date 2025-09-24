using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceWatchdogArr
{
    internal sealed class MonitoringEngine : IDisposable
    {
        private readonly ServiceManager _serviceManager = new ServiceManager();
        private readonly ProcessManager _processManager = new ProcessManager();
        private readonly object _syncRoot = new object();
        private readonly SemaphoreSlim _cycleLock = new SemaphoreSlim(1, 1);
        private Timer _timer;
        private WatchdogConfig _config;
        private List<ApplicationStatusSnapshot> _latest = new List<ApplicationStatusSnapshot>();
        private bool _disposed;

        public MonitoringEngine(WatchdogConfig config)
        {
            _config = config.Clone();
            _timer = new Timer(OnTimerTick, null, TimeSpan.Zero, _config.MonitoringInterval);
        }

        public event EventHandler<MonitoringCycleEventArgs> MonitoringCycleCompleted;

        public IReadOnlyList<ApplicationStatusSnapshot> LatestSnapshot
        {
            get
            {
                lock (_syncRoot)
                {
                    return _latest.Select(static status => status.Clone()).ToList();
                }
            }
        }

        public void ApplyConfiguration(WatchdogConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            lock (_syncRoot)
            {
                _config = config.Clone();
                _timer?.Change(TimeSpan.Zero, _config.MonitoringInterval);
            }
        }

        public Task RefreshNowAsync()
        {
            return RunCycleAsync();
        }

        private void OnTimerTick(object state)
        {
            _ = RunCycleAsync();
        }

        private async Task RunCycleAsync()
        {
            if (_disposed)
            {
                return;
            }

            if (!await _cycleLock.WaitAsync(0).ConfigureAwait(false))
            {
                return;
            }

            try
            {
                WatchdogConfig configSnapshot;
                lock (_syncRoot)
                {
                    configSnapshot = _config.Clone();
                }

                var statuses = new List<ApplicationStatusSnapshot>();
                foreach (MonitoredApplication application in configSnapshot.Applications)
                {
                    ServiceQueryResult serviceStatus = _serviceManager.QueryStatus(application.ServiceName);
                    ProcessQueryResult processStatus = _processManager.QueryProcesses(application.ProcessNames);
                    var snapshot = new ApplicationStatusSnapshot(application, configSnapshot.GlobalMonitoringEnabled, serviceStatus, processStatus);
                    statuses.Add(snapshot);
                    LogStatus(snapshot);
                }

                lock (_syncRoot)
                {
                    _latest = statuses.Select(static status => status.Clone()).ToList();
                }

                MonitoringCycleCompleted?.Invoke(this, new MonitoringCycleEventArgs(DateTime.Now, statuses));
            }
            catch (Exception ex)
            {
                Logger.Write(ex, "Monitoring cycle failure");
            }
            finally
            {
                _cycleLock.Release();
            }
        }

        private static void LogStatus(ApplicationStatusSnapshot snapshot)
        {
            string servicePart;
            if (!snapshot.Service.Exists)
            {
                servicePart = "service not configured";
            }
            else if (snapshot.Service.AccessDenied)
            {
                servicePart = "service access denied";
            }
            else if (snapshot.Service.HasError)
            {
                servicePart = $"service error: {snapshot.Service.Error}";
            }
            else
            {
                servicePart = snapshot.ServiceRunning ? "service running" : "service stopped";
            }

            string processPart;
            if (snapshot.ProcessNames.Count == 0)
            {
                processPart = "process not configured";
            }
            else if (snapshot.ProcessRunning)
            {
                processPart = $"process running ({string.Join(", ", snapshot.Process.RunningProcesses)})";
            }
            else
            {
                processPart = "process stopped";
            }

            string monitoringInfo = snapshot.EffectiveMonitoringEnabled ? "monitoring active" : "monitoring disabled";
            Logger.Write($"[{snapshot.Application.Name}] {servicePart}; {processPart}; {monitoringInfo}");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer?.Dispose();
            _cycleLock.Dispose();
        }
    }

    internal sealed class MonitoringCycleEventArgs : EventArgs
    {
        public MonitoringCycleEventArgs(DateTime timestamp, IReadOnlyList<ApplicationStatusSnapshot> statuses)
        {
            Timestamp = timestamp;
            Statuses = statuses;
        }

        public DateTime Timestamp { get; }

        public IReadOnlyList<ApplicationStatusSnapshot> Statuses { get; }
    }

    internal enum ApplicationHealth
    {
        MonitoringDisabled,
        Healthy,
        Unhealthy
    }

    internal sealed class ApplicationStatusSnapshot
    {
        public ApplicationStatusSnapshot(MonitoredApplication application, bool globalMonitoringEnabled, ServiceQueryResult service, ProcessQueryResult process)
        {
            Application = application.Clone();
            GlobalMonitoringEnabled = globalMonitoringEnabled;
            Service = service;
            Process = process;
        }

        public MonitoredApplication Application { get; }

        public bool GlobalMonitoringEnabled { get; }

        public ServiceQueryResult Service { get; }

        public ProcessQueryResult Process { get; }

        public bool ServiceRunning => Service.IsRunning;

        public bool ProcessRunning => Process.AnyRunning;

        public bool EffectiveMonitoringEnabled => GlobalMonitoringEnabled && Application.MonitoringEnabled;

        public ApplicationHealth Health
        {
            get
            {
                if (!EffectiveMonitoringEnabled)
                {
                    return ApplicationHealth.MonitoringDisabled;
                }

                return ServiceRunning || ProcessRunning ? ApplicationHealth.Healthy : ApplicationHealth.Unhealthy;
            }
        }

        public IReadOnlyList<string> ProcessNames => Application.ProcessNames;

        public ApplicationStatusSnapshot Clone()
        {
            return new ApplicationStatusSnapshot(Application, GlobalMonitoringEnabled, Service, Process);
        }
    }
}
