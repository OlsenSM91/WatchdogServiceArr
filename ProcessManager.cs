using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ServiceWatchdogArr
{
    internal sealed class ProcessManager
    {
        private const int KillWaitMilliseconds = 10000;

        public ProcessQueryResult QueryProcesses(IEnumerable<string> processNames)
        {
            List<string> targets = NormalizeTargets(processNames);
            if (targets.Count == 0)
            {
                return ProcessQueryResult.Empty;
            }

            var running = new List<string>();
            Process[] processes;
            try
            {
                processes = Process.GetProcesses();
            }
            catch
            {
                return ProcessQueryResult.Empty;
            }

            foreach (Process process in processes)
            {
                try
                {
                    string normalized = ProcessNameHelper.Normalize(process.ProcessName);
                    if (targets.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    {
                        running.Add(process.ProcessName);
                    }
                }
                catch
                {
                    // Ignore observation errors.
                }
                finally
                {
                    process.Dispose();
                }
            }

            return new ProcessQueryResult(running);
        }

        public bool KillProcesses(IEnumerable<string> processNames, string applicationName)
        {
            List<string> targets = NormalizeTargets(processNames);
            if (targets.Count == 0)
            {
                return false;
            }

            bool terminatedAny = false;
            Process[] processes;
            try
            {
                processes = Process.GetProcesses();
            }
            catch (Exception ex)
            {
                Logger.Write(ex, $"Failed to enumerate processes for {applicationName}");
                return false;
            }

            foreach (Process process in processes)
            {
                try
                {
                    string normalized = ProcessNameHelper.Normalize(process.ProcessName);
                    if (!targets.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Logger.Write($"Terminating process {process.ProcessName} (PID {process.Id}) for {applicationName}");
                    process.Kill(true);
                    terminatedAny = true;
                    process.WaitForExit(KillWaitMilliseconds);
                }
                catch (Exception ex)
                {
                    Logger.Write(ex, $"Failed to terminate process {process.ProcessName} for {applicationName}");
                }
                finally
                {
                    process.Dispose();
                }
            }

            return terminatedAny;
        }

        public bool StartProcess(string executablePath, string applicationName)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
                Logger.Write($"Launched executable for {applicationName}: {executablePath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Write(ex, $"Failed to start executable for {applicationName}");
                return false;
            }
        }

        private static List<string> NormalizeTargets(IEnumerable<string> processNames)
        {
            return processNames
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Select(static name => ProcessNameHelper.Normalize(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    internal readonly struct ProcessQueryResult
    {
        public static ProcessQueryResult Empty => new ProcessQueryResult(Array.Empty<string>());

        public ProcessQueryResult(IEnumerable<string> runningProcesses)
        {
            RunningProcesses = runningProcesses.ToArray();
        }

        public IReadOnlyList<string> RunningProcesses { get; }

        public bool AnyRunning => RunningProcesses.Count > 0;
    }
}
