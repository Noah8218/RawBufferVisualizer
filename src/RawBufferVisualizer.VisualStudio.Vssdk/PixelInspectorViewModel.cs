using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    internal sealed class PixelInspectorViewModel : INotifyPropertyChanged
    {
        private readonly List<Sample> _neighborhood = new List<Sample>();
        private readonly Dictionary<(int X, int Y), Sample> _samples = new Dictionary<(int X, int Y), Sample>();
        private RawPixelStatistics? _statistics;
        private string _selectedChannel = "GV";
        private bool _isFloat;

        public event PropertyChangedEventHandler? PropertyChanged;
        public ObservableCollection<NeighborhoodCell> Cells { get; } = new ObservableCollection<NeighborhoodCell>();
        public ObservableCollection<ChannelValue> ChannelValues { get; } = new ObservableCollection<ChannelValue>();
        public Brush Swatch => Cells.Count > 12 ? Cells[12].Background : Brushes.Transparent;
        public string[] ChannelOptions { get; } = { "GV", "R", "G", "B" };
        public string Position { get; private set; } = "X —  Y —";
        public string RawBytes { get; private set; } = "—";
        public string RawByteNote { get; private set; } = string.Empty;
        public string ByteOffset { get; private set; } = "—";
        public bool IsColor { get; private set; }
        public bool IsFloat => _isFloat;
        public bool HasPixelData => ChannelValues.Count > 0;
        public string Count { get; private set; } = "—";
        public string Minimum { get; private set; } = "—";
        public string Maximum { get; private set; } = "—";
        public string Mean { get; private set; } = "—";
        public string StandardDeviation { get; private set; } = "—";
        public string ProfileCaption { get; private set; } = string.Empty;
        public string ProfileStart { get; private set; } = string.Empty;
        public string ProfileCenter { get; private set; } = string.Empty;
        public string ProfileEnd { get; private set; } = string.Empty;
        public string ProfileLowLabel { get; private set; } = string.Empty;
        public string ProfileMiddleLabel { get; private set; } = string.Empty;
        public string ProfileHighLabel { get; private set; } = string.Empty;
        public Geometry ProfileR { get; private set; } = Geometry.Empty;
        public Geometry ProfileG { get; private set; } = Geometry.Empty;
        public Geometry ProfileB { get; private set; } = Geometry.Empty;
        public Geometry ProfileValue { get; private set; } = Geometry.Empty;
        public Geometry ProfileRMarker { get; private set; } = Geometry.Empty;
        public Geometry ProfileGMarker { get; private set; } = Geometry.Empty;
        public Geometry ProfileBMarker { get; private set; } = Geometry.Empty;
        public Geometry ProfileValueMarker { get; private set; } = Geometry.Empty;
        public int ProfileSampleCount { get; private set; }
        internal RawPixelStatistics? Statistics => _statistics;
        public string SelectedChannel
        {
            get => _selectedChannel;
            set
            {
                if (value == _selectedChannel || !ChannelOptions.Contains(value)) return;
                _selectedChannel = value;
                UpdateCells();
                Notify();
            }
        }

        internal string GetDescription(int x, int y) => _samples[(x, y)].Description;

        public void Update(RawImageSource source, RawImageDescriptor descriptor, int x, int y, RawRenderOptions? options)
        {
            // Reuse only this update's bounded samples; never retain the live source.
            _samples.Clear();
            Sample ReadPixel(int column, int row)
            {
                if (!_samples.TryGetValue((column, row), out var sample))
                {
                    var text = source.DescribePixel(column, row);
                    sample = Read(text);
                    sample.Description = text;
                    _samples.Add((column, row), sample);
                }
                return sample;
            }

            _isFloat = descriptor.PixelFormat == RawPixelFormat.Float32;
            var current = ReadPixel(x, y);
            IsColor = current.IsColor;
            if (!IsColor) _selectedChannel = "GV";
            Position = string.Format(CultureInfo.InvariantCulture, "X {0}  Y {1}", x, y);
            var description = current.Description;
            var rawIndex = description.IndexOf("Raw=", StringComparison.Ordinal);
            RawBytes = rawIndex < 0 ? "—" : description.Substring(rawIndex + 4);
            var rawEnd = RawBytes.IndexOf(',');
            if (rawEnd >= 0) RawBytes = RawBytes.Substring(0, rawEnd);
            RawByteNote = descriptor.PixelFormat == RawPixelFormat.BGR24 ? " (B G R)" : _isFloat
                ? descriptor.ByteOrder == RawByteOrder.LittleEndian ? " (float32, little-endian)" : " (float32, big-endian)" : string.Empty;
            int bits = descriptor.PixelFormat == RawPixelFormat.Mono10PackedLsb ? 10 : descriptor.PixelFormat == RawPixelFormat.Mono12PackedLsb ? 12 : 0;
            long offset = (long)y * descriptor.Stride + (bits > 0 ? (long)x * bits / 8 : (long)x * descriptor.GetBytesPerPixel());
            ByteOffset = string.Format(CultureInfo.InvariantCulture, "{0:N0} (0x{0:X})", offset);
            ChannelValues.Clear();
            if (IsColor)
            {
                ChannelValues.Add(new ChannelValue("R", current.R));
                ChannelValues.Add(new ChannelValue("G", current.G));
                ChannelValues.Add(new ChannelValue("B", current.B));
                var alpha = ReadToken(description, "A=");
                if (!double.IsNaN(alpha)) ChannelValues.Add(new ChannelValue("A", alpha));
            }
            string? floatHex = null;
            if (_isFloat && RawBytes != "—")
            {
                var parts = RawBytes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 4)
                {
                    if (descriptor.ByteOrder == RawByteOrder.LittleEndian) Array.Reverse(parts);
                    floatHex = "0x" + string.Concat(parts);
                }
            }
            ChannelValues.Add(new ChannelValue(_isFloat || descriptor.PixelFormat == RawPixelFormat.Int32 ? "Value" : "GV", current.Value, _isFloat, floatHex));
            int left = Math.Max(0, x - 2), top = Math.Max(0, y - 2);
            int width = Math.Min(descriptor.Width, x + 3) - left, height = Math.Min(descriptor.Height, y + 3) - top;
            var tile = source.RenderTile(left, top, width, height, options);
            _statistics = source.GetStatistics(left, top, width, height);
            _neighborhood.Clear();
            for (int row = y - 2; row <= y + 2; row++)
            {
                for (int column = x - 2; column <= x + 2; column++)
                {
                    var sample = new Sample { IsCenter = row == y && column == x };
                    if (column >= 0 && row >= 0 && column < descriptor.Width && row < descriptor.Height)
                    {
                        sample = ReadPixel(column, row);
                        sample.IsCenter = row == y && column == x;
                        int i = (row - top) * tile.Stride + (column - left) * 4;
                        sample.Color = Color.FromRgb(tile.Bgra32[i + 2], tile.Bgra32[i + 1], tile.Bgra32[i]);
                        sample.IsAvailable = true;
                    }
                    _neighborhood.Add(sample);
                }
            }
            UpdateCells();
            var line = new List<Sample>();
            for (int column = x - 8; column <= x + 8; column++) line.Add(column < 0 || column >= descriptor.Width ? new Sample() : ReadPixel(column, y));
            ProfileSampleCount = line.Count(sample => sample.IsAvailable);
            ProfileR = IsColor ? Plot(line, sample => sample.R) : Geometry.Empty;
            ProfileG = IsColor ? Plot(line, sample => sample.G) : Geometry.Empty;
            ProfileB = IsColor ? Plot(line, sample => sample.B) : Geometry.Empty;
            var levels = options ?? source.CreateRenderOptions();
            var range = Math.Max(double.Epsilon, levels.WhiteLevel - levels.BlackLevel);
            double ProfileLevel(Sample sample)
            {
                var normalized = double.IsPositiveInfinity(range)
                    ? (sample.Value * 0.5 - levels.BlackLevel * 0.5) / (levels.WhiteLevel * 0.5 - levels.BlackLevel * 0.5)
                    : (sample.Value - levels.BlackLevel) / range;
                return normalized * 255;
            }
            ProfileValue = IsColor ? Geometry.Empty : Plot(line, ProfileLevel);
            ProfileLowLabel = _isFloat ? Format(levels.BlackLevel, 2) : "0";
            ProfileMiddleLabel = _isFloat ? Format(levels.BlackLevel * 0.5 + levels.WhiteLevel * 0.5, 2) : "128";
            ProfileHighLabel = _isFloat ? Format(levels.WhiteLevel, 2) : "255";
            ProfileRMarker = IsColor ? Marker(current.R) : Geometry.Empty;
            ProfileGMarker = IsColor ? Marker(current.G) : Geometry.Empty;
            ProfileBMarker = IsColor ? Marker(current.B) : Geometry.Empty;
            ProfileValueMarker = IsColor ? Geometry.Empty : Marker(ProfileLevel(current));
            ProfileStart = x >= 8 ? (x - 8).ToString(CultureInfo.InvariantCulture) : string.Empty;
            ProfileCenter = x.ToString(CultureInfo.InvariantCulture);
            ProfileEnd = x + 8 < descriptor.Width ? (x + 8).ToString(CultureInfo.InvariantCulture) : string.Empty;
            ProfileCaption = string.Format(CultureInfo.InvariantCulture, "Row {0} · x {1}–{2}", y, Math.Max(0, x - 8), Math.Min(descriptor.Width - 1, x + 8));
            Notify();
        }

        public void Clear()
        {
            _samples.Clear();
            _neighborhood.Clear();
            _statistics = null;
            ChannelValues.Clear();
            Cells.Clear();
            Position = "X —  Y —";
            RawBytes = ByteOffset = Count = Minimum = Maximum = Mean = StandardDeviation = "—";
            RawByteNote = string.Empty;
            ProfileCaption = ProfileStart = ProfileCenter = ProfileEnd = string.Empty;
            ProfileR = ProfileG = ProfileB = ProfileValue = Geometry.Empty;
            ProfileRMarker = ProfileGMarker = ProfileBMarker = ProfileValueMarker = Geometry.Empty;
            ProfileLowLabel = ProfileMiddleLabel = ProfileHighLabel = string.Empty;
            ProfileSampleCount = 0;
            Notify();
        }

        private void UpdateCells()
        {
            Cells.Clear();
            foreach (var sample in _neighborhood)
            {
                double value = Value(sample);
                Cells.Add(new NeighborhoodCell(sample.IsAvailable ? Format(value) : "—", sample.Color, sample.IsCenter));
            }
            if (_statistics == null) return;
            if (_selectedChannel == "GV")
            {
                Count = _statistics.Count.ToString(CultureInfo.InvariantCulture);
                Minimum = Format(_statistics.Minimum);
                Maximum = Format(_statistics.Maximum);
                Mean = FormatStatistic(_statistics.Mean, 2);
                StandardDeviation = FormatStatistic(_statistics.StandardDeviation, 3);
                return;
            }
            var values = _neighborhood.Where(sample => sample.IsAvailable).Select(Value).Where(value => !double.IsNaN(value) && !double.IsInfinity(value)).ToArray();
            Count = values.Length.ToString(CultureInfo.InvariantCulture);
            if (values.Length == 0) { Minimum = Maximum = Mean = StandardDeviation = "NaN"; return; }
            double mean = values.Average();
            Minimum = Format(values.Min());
            Maximum = Format(values.Max());
            Mean = FormatStatistic(mean, 2);
            StandardDeviation = FormatStatistic(Math.Sqrt(values.Average(value => (value - mean) * (value - mean))), 3);
        }

        private double Value(Sample sample) => _selectedChannel == "R" ? sample.R : _selectedChannel == "G" ? sample.G : _selectedChannel == "B" ? sample.B : sample.Value;
        private string Format(double value, int decimals = 3)
        {
            if (!_isFloat) return value.ToString("0.###", CultureInfo.InvariantCulture);
            return value.ToString(Math.Abs(value) >= 1e21 ? "G9" : "F" + decimals, CultureInfo.InvariantCulture);
        }
        private string FormatStatistic(double value, int decimals) => value.ToString(_isFloat && Math.Abs(value) >= 1e21 ? "G9" : "F" + (_isFloat ? 3 : decimals), CultureInfo.InvariantCulture);
        private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

        private static Geometry Marker(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return Geometry.Empty;
            var marker = new EllipseGeometry(new Point(149, 80 - Math.Max(0, Math.Min(255, value)) * 72 / 255), 3, 3);
            marker.Freeze();
            return marker;
        }

        private static Geometry Plot(IReadOnlyList<Sample> line, Func<Sample, double> value)
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                bool started = false;
                for (int i = 0; i < line.Count; i++)
                {
                    double number = value(line[i]);
                    if (!line[i].IsAvailable || double.IsNaN(number) || double.IsInfinity(number)) { started = false; continue; }
                    var point = new Point(30 + i * 238.0 / 16, 80 - Math.Max(0, Math.Min(255, number)) * 72 / 255);
                    if (!started) context.BeginFigure(point, false, false);
                    else context.LineTo(point, true, false);
                    started = true;
                }
            }
            geometry.Freeze();
            return geometry;
        }

        private static Sample Read(string description)
        {
            double r = ReadToken(description, "R="), g = ReadToken(description, "G="), b = ReadToken(description, "B=");
            double value = ReadToken(description, "GV=");
            if (double.IsNaN(value)) value = ReadToken(description, "Value=");
            bool color = !double.IsNaN(r) && !double.IsNaN(g) && !double.IsNaN(b);
            if (double.IsNaN(value) && color) value = (r + g + b) / 3;
            if (double.IsNaN(value) && !color) value = !double.IsNaN(r) ? r : !double.IsNaN(g) ? g : b;
            return new Sample { R = r, G = g, B = b, Value = value, IsColor = color, IsAvailable = true };
        }

        private static double ReadToken(string description, string key)
        {
            int start = description.IndexOf(key, StringComparison.Ordinal);
            if (start < 0) return double.NaN;
            start += key.Length;
            int end = description.IndexOfAny(new[] { ',', ' ', '\n', '\r' }, start);
            string token = end < 0 ? description.Substring(start) : description.Substring(start, end - start);
            return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
        }

        private sealed class Sample
        {
            public string Description = string.Empty;
            public double R = double.NaN, G = double.NaN, B = double.NaN, Value = double.NaN;
            public bool IsColor, IsAvailable, IsCenter;
            public Color Color = Colors.Transparent;
        }

        internal sealed class NeighborhoodCell
        {
            public string Value { get; }
            public Brush Background { get; }
            public Brush Foreground { get; }
            public bool IsCenter { get; }
            public NeighborhoodCell(string value, Color color, bool center)
            {
                Value = value;
                var background = new SolidColorBrush(color);
                background.Freeze();
                Background = background;
                Foreground = Luminance(color.R) * 0.2126 + Luminance(color.G) * 0.7152 + Luminance(color.B) * 0.0722 > 0.179 ? Brushes.Black : Brushes.White;
                IsCenter = center;
            }

            private static double Luminance(byte channel)
            {
                double value = channel / 255.0;
                return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
            }
        }

        internal sealed class ChannelValue
        {
            public string Name { get; }
            public string Decimal { get; }
            public string Hex { get; }
            public Brush Foreground { get; }
            public ChannelValue(string name, double value, bool floating = false, string? hex = null)
            {
                Name = name;
                var foreground = new SolidColorBrush(name == "R" ? Color.FromRgb(224, 108, 117) : name == "G" ? Color.FromRgb(152, 195, 121) : name == "B" ? Color.FromRgb(97, 175, 239) : Color.FromRgb(163, 167, 174));
                foreground.Freeze();
                Foreground = foreground;
                var format = floating ? "F5" : "0.###";
                if (floating && Math.Abs(value) >= 1e21) format = "G9";
                Decimal = (floating ? (double)(float)value : value).ToString(format, CultureInfo.InvariantCulture);
                Hex = hex ?? (floating || double.IsNaN(value) || double.IsInfinity(value) ? "—" : "0x" + (name == "Value" && value < 0 ? unchecked((uint)(int)value).ToString("X8", CultureInfo.InvariantCulture) : ((long)value).ToString("X2", CultureInfo.InvariantCulture)));
            }
        }
    }
}
