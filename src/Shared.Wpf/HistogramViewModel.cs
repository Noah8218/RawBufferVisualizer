using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.Presentation
{
    internal sealed class HistogramViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ViewCommand _actionCommand;
        private RawImageSource? _source;
        private Func<IDisposable?>? _acquireReadLease;
        private CancellationTokenSource? _cancellation;
        private Task _completion = Task.CompletedTask;
        private int _generation;
        private int _selectedChannelIndex;
        private bool _pending;
        private bool _running;
        private bool _cancelRequested;
        private bool _visible;
        private bool _disposed;

        public HistogramViewModel()
        {
            _actionCommand = new ViewCommand(_ => { if (_running) Cancel(); else Request(); }, () => !_disposed && _source != null && (!_running || !_cancelRequested));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public RawPixelHistogram? Result { get; private set; }
        public string Status { get; private set; } = "Select an image.";
        public string? Error { get; private set; }
        public string[] Channels { get; } = new[] { "RGB mean", "R", "G", "B" };
        public bool HasChannels => _source != null && RawPixelHistogram.HasColorChannels(_source.Descriptor.PixelFormat);
        public bool HasSource => _source != null;
        public bool IsRunning => _running;
        public Task Completion => _completion;
        public ICommand ActionCommand => _actionCommand;
        public string ActionText => _running ? "Cancel" : "Refresh";
        public string LowerLabel => Result?.FiniteCount > 0 ? Number(Result.LowerBound) : string.Empty;
        public string UpperLabel => Result?.FiniteCount > 0 ? Number(Result.UpperBound) : string.Empty;
        public string MiddleLabel
        {
            get
            {
                if (Result == null || Result.FiniteCount == 0) return string.Empty;
                var middle = (Result.LowerBound + Result.UpperBound) / 2;
                return Number(_source?.Descriptor.PixelFormat == RawPixelFormat.Float32 ? middle : Math.Round(middle));
            }
        }
        public string Coverage => Result == null ? string.Empty : string.Format(CultureInfo.CurrentCulture,
            Result.IsSampled ? "Sampled: {0:N0} / {1:N0} pixels" : "All pixels: {0:N0}", Result.SampleCount, Result.TotalPixels);
        public string ValueRange => Result?.FiniteCount > 0 ? (Result.IsSampled ? "Sample range: " : "Range: ") + Number(Result.Minimum) + " to " + Number(Result.Maximum) : string.Empty;
        public string Exclusions => Result?.ExcludedCount > 0 ? string.Format(CultureInfo.CurrentCulture, "Excluded: {0:N0} NaN or infinite values", Result.ExcludedCount) : string.Empty;
        public string FrequencyScale => Result == null ? string.Empty : string.Format(CultureInfo.CurrentCulture, "Y: pixels per bin · 0 to {0:N0}", Result.Bins.Max());
        public string DockedFrequencyScale => Result == null ? string.Empty : string.Format(CultureInfo.CurrentCulture, "Count · linear · max {0:N0}", Result.Bins.Max());
        public string DockedCoverage => Result == null ? string.Empty : string.Format(CultureInfo.CurrentCulture,
            Result.IsSampled ? "Coverage: sampled · {0:N0} / {1:N0} px" : "Coverage: full image · {0:N0} px", Result.SampleCount, Result.TotalPixels);
        public string DockedValueRange => Result?.FiniteCount > 0 ? "min " + Number(Result.Minimum, true) + " · max " + Number(Result.Maximum, true) + " · mean " + Statistic(Result.Mean) + " · std " + Statistic(Result.StandardDeviation) : string.Empty;
        public string DockedLowerLabel => Result?.FiniteCount > 0 ? Number(Result.LowerBound, true) : string.Empty;
        public string DockedUpperLabel => Result?.FiniteCount > 0 ? Number(Result.UpperBound, true) : string.Empty;
        public string DockedMiddleLabel => Result?.FiniteCount > 0 ? Number(_source?.Descriptor.PixelFormat == RawPixelFormat.Float32 ? (Result.LowerBound + Result.UpperBound) / 2 : Math.Round((Result.LowerBound + Result.UpperBound) / 2), true) : string.Empty;
        public string AxisDescription => Result == null ? string.Empty : "X: pixel value · 256 equal-width bins";
        public string SamplingDescription => Result?.IsSampled == true ? "A spatial sample keeps inspection responsive. Counts and range describe sampled pixels; rare values may be missed." : string.Empty;
        public string ChannelDescription => HasChannels && _selectedChannelIndex == 0 ? "RGB mean = (R + G + B) / 3 per pixel; alpha is excluded. This is not luminance." : string.Empty;
        public string SourceDescription
        {
            get
            {
                if (_source == null) return string.Empty;
                var descriptor = _source.Descriptor;
                var basis = Result == null ? string.Empty : Result.UsesRawValues ? "Raw values / " : "Display values / ";
                var bits = descriptor.PixelFormat == RawPixelFormat.Mono16 ? " (" + (descriptor.ValidBits > 0 ? descriptor.ValidBits : 16) + " valid bits)" : string.Empty;
                return basis + descriptor.PixelFormat + bits;
            }
        }

        public int SelectedChannelIndex
        {
            get => _selectedChannelIndex;
            set
            {
                if (value < 0 || value > 3 || value == _selectedChannelIndex) return;
                _selectedChannelIndex = HasChannels ? value : 0;
                Request();
            }
        }

        // The host supplies input and a borrowed lifetime; histogram policy stays here.
        public void SetSource(RawImageSource? source, Func<IDisposable?>? acquireReadLease = null, string? unavailableReason = null)
        {
            if (_disposed) return;
            if (ReferenceEquals(source, _source) && unavailableReason == null) return;
            Invalidate();
            _source = source;
            _acquireReadLease = acquireReadLease;
            _selectedChannelIndex = 0;
            Status = source == null ? unavailableReason ?? "Select an image." : "Ready.";
            if (source != null && _visible) Request();
            else Notify();
        }

        public void SetVisible(bool visible)
        {
            if (_disposed || _visible == visible) return;
            _visible = visible;
            if (visible && _source != null && Result == null && Status != "Cancelled.") Request();
            else if (!visible && _running) { Invalidate(); Status = "Ready."; Notify(); }
        }

        public void OnDebuggerResumed()
        {
            if (_source?.IsLiveProcessBacked == true) SetSource(null, unavailableReason: "Live image unavailable while the debuggee is running.");
            else if (_running) Cancel();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Invalidate();
            _source = null;
            _acquireReadLease = null;
        }

        private void Request()
        {
            if (_disposed || _source == null) return;
            Invalidate();
            if (!_visible) { Status = "Ready."; Notify(); return; }
            _pending = true;
            _cancelRequested = false;
            Status = "Calculating...";
            if (!_running) _completion = DrainAsync();
            Notify();
        }

        private async Task DrainAsync()
        {
            _running = true;
            while (_pending && !_disposed)
            {
                _pending = false;
                var source = _source;
                var generation = _generation;
                var channel = (RawHistogramChannel)_selectedChannelIndex;
                var cancellation = new CancellationTokenSource();
                _cancellation = cancellation;
                try
                {
                    using (_acquireReadLease?.Invoke())
                    {
                        var result = await Task.Run(() => source!.GetHistogram(channel, cancellation.Token));
                        if (generation == _generation && !_disposed && !cancellation.IsCancellationRequested)
                        {
                            Result = result;
                            Status = result.FiniteCount == 0 ? "No finite values in the inspected pixels." : string.Empty;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    if (generation == _generation && !_disposed) { Result = null; Status = "Cancelled."; }
                }
                catch (Exception ex)
                {
                    if (generation == _generation && !_disposed) { Result = null; Error = ex.Message; Status = "Histogram unavailable: " + ex.Message; }
                }
                finally
                {
                    _cancellation = null;
                    cancellation.Dispose();
                }
            }
            _running = false;
            _cancelRequested = false;
            if (!_disposed) Notify();
        }

        private void Cancel()
        {
            Invalidate();
            _cancelRequested = true;
            Status = "Cancelled.";
            Notify();
        }

        private void Invalidate()
        {
            _generation++;
            _pending = false;
            Result = null;
            Error = null;
            _cancellation?.Cancel();
        }

        private void Notify()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            _actionCommand.Refresh();
        }

        private string Statistic(double value) => value.ToString(Math.Abs(value) >= 1e21 ? "G9" : _source?.Descriptor.PixelFormat == RawPixelFormat.Float32 ? "F3" : "F1", CultureInfo.CurrentCulture);

        private string Number(double value, bool docked = false) => value.ToString(_source?.Descriptor.PixelFormat == RawPixelFormat.Int32 ? "0" : docked && _source?.Descriptor.PixelFormat == RawPixelFormat.Float32 && Math.Abs(value) < 1e21 ? "F3" : "G9", CultureInfo.CurrentCulture);
    }
}
