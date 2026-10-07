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

        public InspectorNavigationViewModel()
        {
            CloseCommand = new ViewCommand(_ => Close(), () => IsOpen);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public string Section { get; private set; } = "Pixel";
        public string Title => Section == "Format" ? "Buffer format" : Section;
        public bool IsNarrow { get; private set; }
        public bool IsOpen { get; private set; }
        public bool IsPixelSelected { get => IsOpen && Section == "Pixel"; set => Select("Pixel", value); }
        public bool IsHistogramSelected { get => IsOpen && Section == "Histogram"; set => Select("Histogram", value); }
        public bool IsFormatSelected { get => IsOpen && Section == "Format"; set => Select("Format", value); }
        public bool IsCompareSelected { get => IsOpen && Section == "Compare"; set => Select("Compare", value); }
        public bool IsDiagnosticsSelected { get => IsOpen && Section == "Diagnostics"; set => Select("Diagnostics", value); }
        public ICommand CloseCommand { get; }

        public void SetWidth(double width)
        {
            if (width <= 0) return;
            var narrow = width < 760;
            if (_hasWidth && IsNarrow == narrow) return;
            _hasWidth = true;
            IsNarrow = narrow;
            IsOpen = narrow ? _overlayOpen : _dockedOpen;
            Notify();
        }

        public void Show(string section)
        {
            Section = section;
            SetOpen(true);
        }

        public void Close() => SetOpen(false);

        private void Select(string section, bool selected)
        {
            if (selected) Show(section);
            else if (Section == section) Close();
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            if (IsNarrow) _overlayOpen = open;
            else _dockedOpen = open;
            Notify();
        }

        private void Notify()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            ((ViewCommand)CloseCommand).Refresh();
        }
    }
}
