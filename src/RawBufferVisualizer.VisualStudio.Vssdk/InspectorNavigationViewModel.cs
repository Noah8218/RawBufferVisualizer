using System;
using System.ComponentModel;
using System.Windows.Input;
using RawBufferVisualizer.Presentation;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    internal sealed class InspectorNavigationViewModel : INotifyPropertyChanged
    {
        private bool _hasWidth;
        private bool _dockedOpen = true;
        private bool _overlayOpen;
        private bool? _imageStripOverride;

        public InspectorNavigationViewModel()
        {
            CloseCommand = new ViewCommand(_ => Close(), () => IsOpen);
            ToggleSectionCommand = new ViewCommand(parameter =>
            {
                if (parameter is string section)
                {
                    if (IsOpen && Section == section) Close();
                    else Show(section);
                }
            }, parameter => parameter is string);
            ToggleImageListCommand = new ViewCommand(_ => { _imageStripOverride = !IsImageStrip; Notify(); }, () => true);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? OpenStateChanged;
        public string Section { get; private set; } = "Pixel";
        public string Title => Section == "Format" ? "Buffer format" : Section;
        public bool IsNarrow { get; private set; }
        public bool IsOpen { get; private set; }
        public bool DockedOpen => _dockedOpen;
        public bool OverlayOpen => _overlayOpen;
        public bool IsPixelSelected => IsOpen && Section == "Pixel";
        public bool IsHistogramSelected => IsOpen && Section == "Histogram";
        public bool IsFormatSelected => IsOpen && Section == "Format";
        public bool IsCompareSelected => IsOpen && Section == "Compare";
        public bool IsDiagnosticsSelected => IsOpen && Section == "Diagnostics";
        public ICommand CloseCommand { get; }
        public ICommand ToggleSectionCommand { get; }
        public bool IsImageStrip => _imageStripOverride ?? IsNarrow;
        public ICommand ToggleImageListCommand { get; }

        public void RestoreOpenState(bool? dockedOpen, bool? overlayOpen)
        {
            _dockedOpen = dockedOpen ?? true;
            _overlayOpen = overlayOpen ?? false;
            if (_hasWidth) IsOpen = IsNarrow ? _overlayOpen : _dockedOpen;
            Notify();
        }

        public void SetWidth(double width)
        {
            if (width <= 0) return;
            var narrow = width < 760;
            if (_hasWidth && IsNarrow == narrow) return;
            _hasWidth = true;
            IsNarrow = narrow;
            _imageStripOverride = null;
            IsOpen = narrow ? _overlayOpen : _dockedOpen;
            Notify();
        }

        public void Show(string section)
        {
            Section = section;
            SetOpen(true);
        }

        public void Close() => SetOpen(false);

        private void SetOpen(bool open)
        {
            var changed = (IsNarrow ? _overlayOpen : _dockedOpen) != open;
            IsOpen = open;
            if (IsNarrow) _overlayOpen = open;
            else _dockedOpen = open;
            Notify();
            if (changed) OpenStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Notify()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            ((ViewCommand)CloseCommand).Refresh();
        }
    }
}
