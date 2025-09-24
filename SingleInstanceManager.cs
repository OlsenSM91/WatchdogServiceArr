using System;
using System.Threading;

namespace ServiceWatchdogArr
{
    internal sealed class SingleInstanceManager : IDisposable
    {
        private const string MutexName = "Local/ServiceWatchdogArr.SingleInstance";
        private const string EventName = "Local/ServiceWatchdogArr.Show";

        private readonly Mutex _mutex;
        private readonly EventWaitHandle _showHandle;
        private readonly RegisteredWaitHandle _registeredWait;
        private readonly Action _showCallback;
        private bool _disposed;

        private SingleInstanceManager(Mutex mutex, EventWaitHandle showHandle, RegisteredWaitHandle registeredWait, Action showCallback)
        {
            _mutex = mutex;
            _showHandle = showHandle;
            _registeredWait = registeredWait;
            _showCallback = showCallback;
        }

        public static bool TryAcquire(Action showCallback, out SingleInstanceManager manager)
        {
            manager = null;
            if (showCallback == null)
            {
                throw new ArgumentNullException(nameof(showCallback));
            }

            bool createdNew;
            Mutex mutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out createdNew);
            if (!createdNew)
            {
                mutex.Dispose();
                return false;
            }

            EventWaitHandle showHandle = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            RegisteredWaitHandle registeredWait = ThreadPool.RegisterWaitForSingleObject(
                showHandle,
                (_, _) => showCallback(),
                null,
                Timeout.Infinite,
                executeOnlyOnce: false);

            manager = new SingleInstanceManager(mutex, showHandle, registeredWait, showCallback);
            return true;
        }

        public static void SignalExistingInstance()
        {
            try
            {
                using EventWaitHandle showHandle = EventWaitHandle.OpenExisting(EventName);
                showHandle.Set();
            }
            catch
            {
                // Nothing to do if signaling fails.
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _registeredWait?.Unregister(null);
            _showHandle?.Dispose();
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
