using System;
using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher
{
    public abstract class DisposableBase : IDisposable
    {
        private static readonly ContextLogger _logger = Logger.GetLogger(nameof(DisposableBase));
        private bool _disposed = false;
        protected bool IsDisposed => _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _logger.Debug($"Disposing managed resources for {GetType().Name}");
                    ReleaseManagedResources();
                }
                _logger.Debug($"Disposing unmanaged resources for {GetType().Name}");
                ReleaseUnmanagedResources();
                _disposed = true;
            }
        }

        /// <summary>
        /// Override this method to release managed resources.
        /// </summary>
        protected virtual void ReleaseManagedResources() { }

        /// <summary>
        /// Override this method to release unmanaged resources.
        /// </summary>
        protected virtual void ReleaseUnmanagedResources() { }

        ~DisposableBase()
        {
            _logger.Debug($"Finalizer called for {GetType().Name}");
            Dispose(false);
        }

        protected void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);
        }
    }
}

