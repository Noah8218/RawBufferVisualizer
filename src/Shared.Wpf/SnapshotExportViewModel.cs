using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.Presentation
{
    // The viewer owns the operation and retains the source until RunAsync has settled.
    internal sealed class SnapshotExportViewModel : INotifyPropertyChanged, IProgress<long>
    {
        private readonly SnapshotExportOperation _operation;
        private readonly RawImageSource _source;
        private readonly RawImageDescriptor _descriptor;
        private readonly IProgress<long> _progress;
        private readonly Stopwatch _reportClock = Stopwatch.StartNew();
        private readonly ViewCommand _cancelCommand;
        private Task<RawBufferSnapshotReference?>? _completion;
        private long _lastReportTime;
        private long _copiedBytes;
        private bool _cancelRequested;

        public SnapshotExportViewModel(SnapshotExportOperation operation, string path, RawImageSource source, RawImageDescriptor descriptor)
        {
            _operation = operation;
            Path = path;
            _source = source;
            _descriptor = descriptor.Clone();
            TotalBytes = source.Length;
            _progress = new Progress<long>(UpdateProgress);
            _cancelCommand = new ViewCommand(_ => CancelOrClose(), () => IsFinished || !_cancelRequested);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? Completed;
        public event EventHandler? CloseRequested;
        public string Path { get; }
        public long TotalBytes { get; }
        public bool IsFinished { get; private set; }
        public string? Error { get; private set; }
        public string Status { get; private set; } = "Saving snapshot...";
        public double Percent => TotalBytes == 0 ? 0 : 100.0 * _copiedBytes / TotalBytes;
        public string ByteCount => string.Format(CultureInfo.CurrentCulture, "{0:N0} / {1:N0} bytes ({2:0}%)", _copiedBytes, TotalBytes, Percent);
        public string CancelText => IsFinished ? "Close" : "Cancel";
        public ICommand CancelCommand => _cancelCommand;

        public Task<RawBufferSnapshotReference?> RunAsync() => _completion ?? (_completion = SaveAsync());

        public void Report(long value)
        {
            // Bound dispatcher traffic as well as copy buffers during fast, large exports.
            var now = _reportClock.ElapsedMilliseconds;
            if (value != TotalBytes && now - _lastReportTime < 100) return;
            _lastReportTime = now;
            _progress.Report(value);
        }

        private async Task<RawBufferSnapshotReference?> SaveAsync()
        {
            RawBufferSnapshotReference? result = null;
            try
            {
                result = await _operation.SaveAsync(Path, _source, _descriptor, this);
                UpdateProgress(TotalBytes);
                Status = "Snapshot saved.";
            }
            catch (OperationCanceledException) { Status = "Snapshot export cancelled."; }
            catch (Exception ex)
            {
                Error = ex.Message;
                Status = "Snapshot export failed: " + ex.Message;
            }
            finally
            {
                IsFinished = true;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
                _cancelCommand.Refresh();
                Completed?.Invoke(this, EventArgs.Empty);
            }
            return result;
        }

        private void UpdateProgress(long value)
        {
            if (IsFinished) return;
            _copiedBytes = Math.Min(TotalBytes, Math.Max(_copiedBytes, value));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Percent)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ByteCount)));
        }

        private void CancelOrClose()
        {
            if (IsFinished) { CloseRequested?.Invoke(this, EventArgs.Empty); return; }
            _cancelRequested = true;
            Status = "Cancelling snapshot export...";
            _operation.Cancel();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            _cancelCommand.Refresh();
        }
    }
}
