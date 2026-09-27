using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Presentation;

namespace RawBufferVisualizer.Wpf
{
    internal sealed class RawImportViewModel : INotifyPropertyChanged
    {
        private readonly ViewCommand _confirm;
        private string _width = string.Empty, _height = string.Empty, _stride = string.Empty, _validBits = "8";
        private RawPixelFormat _format;
        private RawByteOrder _byteOrder;

        public RawImportViewModel(string path, long length)
        {
            Path = path;
            Length = length;
            _confirm = new ViewCommand(_ => { if (Descriptor != null) CloseRequested?.Invoke(true); }, () => Descriptor != null);
            CancelCommand = new ViewCommand(_ => CloseRequested?.Invoke(false), () => true);
            Validate();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action<bool>? CloseRequested;
        public string Path { get; }
        public long Length { get; }
        public Array Formats { get; } = Enum.GetValues(typeof(RawPixelFormat));
        public Array ByteOrders { get; } = Enum.GetValues(typeof(RawByteOrder));
        public string Width { get => _width; set { _width = value; Validate(); } }
        public string Height { get => _height; set { _height = value; Validate(); } }
        public string Stride { get => _stride; set { _stride = value; Validate(); } }
        public string ValidBits { get => _validBits; set { _validBits = value; Validate(); } }
        public RawByteOrder ByteOrder { get => _byteOrder; set { _byteOrder = value; Validate(); } }
        public RawPixelFormat Format
        {
            get => _format;
            set
            {
                if (_format == value) return;
                _format = value;
                _validBits = value == RawPixelFormat.Binary ? "1" : value == RawPixelFormat.Mono16 ? "16" : value == RawPixelFormat.Mono10PackedLsb ? "10"
                    : value == RawPixelFormat.Mono12PackedLsb ? "12" : value == RawPixelFormat.Float32 || value == RawPixelFormat.Int32 ? "32" : "8";
                Validate();
            }
        }
        public RawImageDescriptor? Descriptor { get; private set; }
        public string ValidationMessage { get; private set; } = string.Empty;
        public string ByteSummary { get; private set; } = string.Empty;
        public bool HasError => Descriptor == null;
        public ICommand ConfirmCommand => _confirm;
        public ICommand CancelCommand { get; }

        private void Validate()
        {
            Descriptor = null;
            ByteSummary = string.Format(CultureInfo.CurrentCulture, "Available: {0:N0} bytes", Length);
            if (!Positive(_width, out var width) || !Positive(_height, out var height) || !Positive(_validBits, out var validBits))
                ValidationMessage = "Enter positive whole numbers for width, height and valid bits. RAW files do not contain these settings.";
            else
            {
                var descriptor = new RawImageDescriptor { Width = width, Height = height, PixelFormat = _format, ValidBits = validBits, ByteOrder = _byteOrder };
                var automaticStride = string.IsNullOrWhiteSpace(_stride);
                if (automaticStride) descriptor.Stride = descriptor.GetMinimumStride();
                else if (Positive(_stride, out var stride)) descriptor.Stride = stride;
                var diagnostics = RawBufferDiagnostics.AnalyzeLength(Length, descriptor);
                var errors = diagnostics.Where(item => item.Severity == RawDiagnosticSeverity.Error).Select(item => item.Message).ToArray();
                var fixedBits = _format == RawPixelFormat.Float32 ? 32 : _format == RawPixelFormat.Binary ? 1 : 8;
                if (_format != RawPixelFormat.Mono16 && _format != RawPixelFormat.Mono10PackedLsb && _format != RawPixelFormat.Mono12PackedLsb && _format != RawPixelFormat.Int32 && validBits != fixedBits)
                    errors = new[] { _format + " requires " + fixedBits + " valid bits per channel." };
                if (!automaticStride && descriptor.Stride == 0) errors = new[] { "Stride must be a positive whole number of bytes, or blank for tightly packed rows." };
                if (errors.Length > 0) ValidationMessage = string.Join(Environment.NewLine, errors);
                else
                {
                    Descriptor = descriptor;
                    var extra = Length - descriptor.GetRequiredByteCount();
                    ValidationMessage = extra > 0
                        ? string.Format(CultureInfo.CurrentCulture, "Check settings: {0:N0} trailing bytes will not be displayed. Matching file size alone does not confirm the pixel format.", extra)
                        : "Size is valid. Confirm these settings against the source that produced the RAW file.";
                }
                ByteSummary += string.Format(CultureInfo.CurrentCulture, " · Required: {0:N0} bytes · Row stride: {1:N0} bytes", descriptor.GetRequiredByteCount(), descriptor.Stride);
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            _confirm.Refresh();
        }

        private static bool Positive(string value, out int number) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) && number > 0;
    }
}
