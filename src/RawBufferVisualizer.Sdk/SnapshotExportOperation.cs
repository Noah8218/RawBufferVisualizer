using System;
using System.Threading;
using System.Threading.Tasks;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.Sdk
{
    // One owner per viewer. The caller retains the source/temporary-directory lease until the returned task ends.
    public sealed class SnapshotExportOperation : IDisposable
    {
        private readonly object _sync = new object();
        private CancellationTokenSource? _cancellation;
        private bool _isLive;
        private bool _disposed;

        public bool IsRunning { get { lock (_sync) return _cancellation != null; } }

        public Task<RawBufferSnapshotReference> SaveAsync(string metadataPath, RawImageSource source, RawImageDescriptor descriptor,
            IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            lock (_sync)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(SnapshotExportOperation));
                if (_cancellation != null) throw new InvalidOperationException("A snapshot export is already in progress.");
                var snapshotDescriptor = descriptor.Clone();
                var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _cancellation = cancellation;
                _isLive = source.IsLiveProcessBacked;
                // Do not pass the token to Task.Run: the finally block must execute even when cancelled before dispatch.
                return Task.Run(() =>
                {
                    try { return RawBufferSnapshot.SaveSource(metadataPath, source, snapshotDescriptor, cancellation.Token, progress); }
                    finally
                    {
                        lock (_sync)
                        {
                            _cancellation = null;
                            cancellation.Dispose();
                        }
                    }
                });
            }
        }

        public void Cancel()
        {
            lock (_sync) _cancellation?.Cancel();
        }

        public void CancelLiveExport()
        {
            lock (_sync) if (_isLive) _cancellation?.Cancel();
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _disposed = true;
                _cancellation?.Cancel();
            }
        }
    }
}
