using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace RawBufferVisualizer.VisualStudio
{
    public sealed class VisualStudioSnapshotDirectoryLease : IDisposable
    {
        private readonly string _leasePath;
        private FileStream? _stream;

        internal VisualStudioSnapshotDirectoryLease(string directoryPath)
        {
            DirectoryPath = directoryPath;
            _leasePath = Path.Combine(
                directoryPath,
                ".rbuf-active-" + Guid.NewGuid().ToString("N") + ".lease");
            _stream = new FileStream(
                _leasePath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.Read);
            var payload = Encoding.UTF8.GetBytes(
                Process.GetCurrentProcess().Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "\n"
                + DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            _stream.Write(payload, 0, payload.Length);
            _stream.Flush(true);
        }

        public string DirectoryPath { get; private set; }

        public void Dispose()
        {
            var stream = _stream;
            if (stream == null)
            {
                return;
            }

            _stream = null;
            stream.Dispose();
            try
            {
                File.Delete(_leasePath);
            }
            catch
            {
                // An orphaned marker is safe. A later stale sweep can remove it
                // after confirming that no process still holds the file open.
            }
        }
    }
}
