using System;
using System.ComponentModel;
using System.ServiceProcess;

namespace ServiceWatchdogArr
{
    internal sealed class ServiceManager
    {
        private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

        public ServiceQueryResult QueryStatus(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                return ServiceQueryResult.NotConfigured;
            }

            try
            {
                using ServiceController controller = new ServiceController(serviceName);
                controller.Refresh();
                bool running = controller.Status == ServiceControllerStatus.Running ||
                               controller.Status == ServiceControllerStatus.StartPending;
                return new ServiceQueryResult(true, running, false, null);
            }
            catch (InvalidOperationException ex) when (IsAccessDenied(ex))
            {
                return new ServiceQueryResult(true, false, true, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return new ServiceQueryResult(false, false, false, ex.Message);
            }
            catch (Exception ex)
            {
                return new ServiceQueryResult(true, false, false, ex.Message);
            }
        }

        public ServiceRestartResult RestartService(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                return ServiceRestartResult.Skipped;
            }

            try
            {
                using ServiceController controller = new ServiceController(serviceName);
                controller.Refresh();

                if (controller.Status == ServiceControllerStatus.StopPending)
                {
                    controller.WaitForStatus(ServiceControllerStatus.Stopped, StopTimeout);
                }

                if (controller.Status != ServiceControllerStatus.Stopped &&
                    controller.Status != ServiceControllerStatus.StopPending)
                {
                    controller.Stop();
                    controller.WaitForStatus(ServiceControllerStatus.Stopped, StopTimeout);
                }

                controller.Start();
                controller.WaitForStatus(ServiceControllerStatus.Running, StartTimeout);
                return ServiceRestartResult.Success;
            }
            catch (InvalidOperationException ex) when (IsAccessDenied(ex))
            {
                return ServiceRestartResult.RequiresElevation(ex.Message);
            }
            catch (Exception ex)
            {
                return ServiceRestartResult.Failure(ex.Message);
            }
        }

        private static bool IsAccessDenied(Exception ex)
        {
            if (ex is InvalidOperationException invalidOperation && invalidOperation.InnerException is Win32Exception win32)
            {
                return win32.NativeErrorCode == 5;
            }

            return false;
        }
    }

    internal readonly struct ServiceQueryResult
    {
        public static ServiceQueryResult NotConfigured => new ServiceQueryResult(false, false, false, null);

        public ServiceQueryResult(bool exists, bool running, bool accessDenied, string error)
        {
            Exists = exists;
            IsRunning = running;
            AccessDenied = accessDenied;
            Error = error;
        }

        public bool Exists { get; }

        public bool IsRunning { get; }

        public bool AccessDenied { get; }

        public string Error { get; }

        public bool HasError => !string.IsNullOrWhiteSpace(Error);
    }

    internal readonly struct ServiceRestartResult
    {
        private ServiceRestartResult(bool succeeded, bool requiresElevation, string message, bool skipped)
        {
            Succeeded = succeeded;
            RequiresElevation = requiresElevation;
            Message = message;
            Skipped = skipped;
        }

        public bool Succeeded { get; }

        public bool RequiresElevation { get; }

        public string Message { get; }

        public bool Skipped { get; }

        public static ServiceRestartResult Success => new ServiceRestartResult(true, false, string.Empty, false);

        public static ServiceRestartResult RequiresElevation(string message) => new ServiceRestartResult(false, true, message, false);

        public static ServiceRestartResult Failure(string message) => new ServiceRestartResult(false, false, message, false);

        public static ServiceRestartResult Skipped => new ServiceRestartResult(true, false, string.Empty, true);
    }
}
