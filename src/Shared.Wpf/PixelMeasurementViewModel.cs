using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

namespace RawBufferVisualizer.Presentation
{
    internal sealed class PixelMeasurementViewModel : INotifyPropertyChanged
    {
        private readonly ViewCommand _selectCommand;
        private readonly ViewCommand _clearCommand;
        private int _width, _height;
        private bool _isEnabled;
        private bool _isPreview;
        private Tuple<int, int>? _start, _end;

        public PixelMeasurementViewModel()
        {
            _selectCommand = new ViewCommand(SelectPixel, () => IsEnabled && CanMeasure);
            _clearCommand = new ViewCommand(_ => Clear(), () => _start != null);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public bool CanMeasure => _width > 0 && _height > 0 && !_isPreview;
        public string MeasurementToolTip => _isPreview ? "Measurement is unavailable for sampled previews. Wait for the full-resolution image."
            : CanMeasure ? "Click two image pixels to measure their horizontal and vertical distance"
            : "Open an available full-resolution image to measure pixel distances.";
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value || (value && !CanMeasure)) return;
                _isEnabled = value;
                Clear();
            }
        }
        public string ModeText => IsEnabled ? "Measuring" : "Measure";
        public Tuple<int, int>? Start => _start;
        public Tuple<int, int>? End => _end;
        public string Status => _start == null ? "Measure: click the first point"
            : _end == null ? string.Format(CultureInfo.InvariantCulture, "Start X {0} Y {1} · click the end point", _start.Item1, _start.Item2)
            : string.Format(CultureInfo.InvariantCulture, "Width {0} px · Height {1} px", Math.Abs(_end.Item1 - _start.Item1), Math.Abs(_end.Item2 - _start.Item2));
        public ICommand SelectCommand => _selectCommand;
        public ICommand ClearCommand => _clearCommand;

        public void SetImageSize(int width, int height, bool isPreview = false)
        {
            _width = width;
            _height = height;
            _isPreview = isPreview;
            _isEnabled = false;
            Clear();
        }

        private void SelectPixel(object? parameter)
        {
            var point = parameter as Tuple<int, int>;
            if (point == null || point.Item1 < 0 || point.Item2 < 0 || point.Item1 >= _width || point.Item2 >= _height) return;
            if (_start == null || _end != null)
            {
                _start = point;
                _end = null;
            }
            else _end = point;
            Notify();
        }

        private void Clear()
        {
            _start = null;
            _end = null;
            Notify();
        }

        private void Notify()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            _selectCommand.Refresh();
            _clearCommand.Refresh();
        }
    }
}
