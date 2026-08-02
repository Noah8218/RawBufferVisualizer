using System;

namespace RawBufferVisualizer.VisualStudio
{
    internal sealed class VisualStudioSnapshotLeaseOwner : IDisposable
    {
        private VisualStudioSnapshotDirectoryLease? _lease;
        private bool _disposed;

        public bool HasLease
        {
            get { return _lease != null; }
        }

        public string DirectoryPath
        {
            get { return _lease == null ? string.Empty : _lease.DirectoryPath; }
        }

        public string Replace(string metadataPath, bool ownSnapshotDirectory)
        {
            ThrowIfDisposed();
            var nextDirectory = string.Empty;
            var ownsNextDirectory = ownSnapshotDirectory
                && VisualStudioTempStore.TryGetOwnedSnapshotDirectory(
                    metadataPath,
                    out nextDirectory);
            if (!ownsNextDirectory)
            {
                nextDirectory = string.Empty;
            }

            if (_lease != null
                && string.Equals(
                    _lease.DirectoryPath,
                    nextDirectory,
                    StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            VisualStudioSnapshotDirectoryLease? nextLease = ownsNextDirectory
                ? VisualStudioTempStore.CreateSnapshotDirectoryLease(metadataPath)
                : null;
            var previousLease = _lease;
            _lease = nextLease;
            if (previousLease != null)
            {
                var previousDirectory = previousLease.DirectoryPath;
                previousLease.Dispose();
                VisualStudioTempStore.TryDeleteDirectory(previousDirectory);
                return previousDirectory;
            }

            return string.Empty;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_lease == null)
            {
                return;
            }

            var directory = _lease.DirectoryPath;
            _lease.Dispose();
            _lease = null;
            VisualStudioTempStore.TryDeleteDirectory(directory);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }
}
