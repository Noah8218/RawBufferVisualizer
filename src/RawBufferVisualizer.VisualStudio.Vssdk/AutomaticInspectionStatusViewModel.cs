using System.ComponentModel;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    internal enum InspectionDebuggerMode { Stopped, Running, Paused }

    internal sealed class AutomaticInspectionStatusViewModel : INotifyPropertyChanged
    {
        private InspectionDebuggerMode _mode;
        private string _frame = "No paused stack frame";
        private string _imageStatus = "0 images";
        private string? _alert;
        private string? _notice;
        private bool _imageError, _busy, _clearDetailsOnImageUpdate;

        public event PropertyChangedEventHandler? PropertyChanged;
        public string Frame { get => _frame; set { _frame = value; Notify(); } }
        public string Details { get; private set; } = "Pause the debugger to inspect the current frame.";
        public bool IsBusy { get => _busy; set { _busy = value; Notify(); } }
        public bool CanScan => _mode == InspectionDebuggerMode.Paused && !_busy;
        public string Status => _notice ?? (_imageError ? _imageStatus : _alert ?? (_busy ? "Scanning…" : ModeStatus));
        public string StatusDetails => ModeStatus + "\n" + _imageStatus + "\n" + Details + (_notice == null ? string.Empty : "\n" + _notice);
        private string ModeStatus => _mode == InspectionDebuggerMode.Paused
            ? (string.IsNullOrWhiteSpace(_frame) || _frame == "Current paused stack frame" ? "Debugger paused" : "Paused · " + _frame)
            : _mode == InspectionDebuggerMode.Running ? "Debugger running" : "Debugger stopped";

        private string ModeDetails => _mode == InspectionDebuggerMode.Paused ? "Scan Now is available. Auto Inspect controls refresh on the next Break."
            : _mode == InspectionDebuggerMode.Running ? "Pause the debugger to inspect the current frame. Captured images remain available."
            : "No active debug session. Captured images remain available.";

        public void SetMode(InspectionDebuggerMode mode)
        {
            _mode = mode;
            _alert = null;
            _notice = null;
            _frame = mode == InspectionDebuggerMode.Paused ? "Current paused stack frame" : "No paused stack frame";
            Details = ModeDetails;
            _clearDetailsOnImageUpdate = false;
            Notify();
        }

        public void SetDetails(string text, string? alert = null, bool clearOnImageUpdate = false)
        {
            Details = text;
            _alert = alert;
            _notice = null;
            _clearDetailsOnImageUpdate = clearOnImageUpdate;
            Notify();
        }

        public void SetImageStatus(string text, bool isError = false)
        {
            if (_clearDetailsOnImageUpdate)
            {
                Details = ModeDetails;
                _clearDetailsOnImageUpdate = false;
            }
            _imageStatus = text;
            _imageError = isError;
            _notice = null;
            Notify();
        }

        public void SetTransientStatus(string text)
        {
            _notice = text;
            Notify();
        }

        private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
