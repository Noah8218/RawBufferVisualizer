using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Presentation;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    internal sealed class BufferLayoutViewModel : INotifyPropertyChanged
    {
        private RawImageDescriptor? _current;
        private RawImageDescriptor? _declared;
        private long _bufferLength;
        private RawPixelFormat? _pixelFormat;
        private RawByteOrder? _byteOrder;
        private string _width = string.Empty;
        private string _height = string.Empty;
        private string _stride = string.Empty;
        private string _validBits = string.Empty;

        public BufferLayoutViewModel()
        {
            ApplyCommand = new ViewCommand(_ => Apply(), () => HasSource);
            ResetCommand = new ViewCommand(_ => Reset(), () => HasSource);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action<RawImageDescriptor>? Applied;
        public event Action<string>? Failed;
        public bool HasSource => _current != null;
        public RawPixelFormat? PixelFormat { get => _pixelFormat; set { _pixelFormat = value; Notify(); } }
        public RawByteOrder? ByteOrder { get => _byteOrder; set { _byteOrder = value; Notify(); } }
        public string Width { get => _width; set { _width = value; Notify(); } }
        public string Height { get => _height; set { _height = value; Notify(); } }
        public string Stride { get => _stride; set { _stride = value; Notify(); } }
        public string ValidBits { get => _validBits; set { _validBits = value; Notify(); } }
        public string Error { get; private set; } = string.Empty;
        public string Derived
        {
            get
            {
                if (!HasSource || PixelFormat == null || !int.TryParse(Width, out var width) || !int.TryParse(Stride, out var stride)) return string.Empty;
                var descriptor = new RawImageDescriptor { Width = width, PixelFormat = PixelFormat.Value };
                if (!descriptor.TryGetMinimumStride(out var rowBytes)) return string.Empty;
                return string.Format(CultureInfo.InvariantCulture, "Row bytes {0:N0} · padding {1:N0} B/row · buffer {2:N0} B", rowBytes, (long)stride - rowBytes, _bufferLength);
            }
        }
        public ICommand ApplyCommand { get; }
        public ICommand ResetCommand { get; }

        public void SetSource(RawImageDescriptor current, RawImageDescriptor declared, long bufferLength)
        {
            _current = current.Clone();
            _declared = declared.Clone();
            _bufferLength = bufferLength;
            Fill(current);
        }

        public void Clear()
        {
            _current = _declared = null;
            _pixelFormat = null;
            _byteOrder = null;
            _width = _height = _stride = _validBits = Error = string.Empty;
            Notify();
        }

        private void Fill(RawImageDescriptor descriptor)
        {
            _pixelFormat = descriptor.PixelFormat;
            _byteOrder = descriptor.ByteOrder;
            _width = descriptor.Width.ToString(CultureInfo.InvariantCulture);
            _height = descriptor.Height.ToString(CultureInfo.InvariantCulture);
            _stride = descriptor.Stride.ToString(CultureInfo.InvariantCulture);
            _validBits = descriptor.ValidBits.ToString(CultureInfo.InvariantCulture);
            Error = string.Empty;
            Notify();
        }

        private void Apply()
        {
            try
            {
                var descriptor = _current!.Clone();
                descriptor.PixelFormat = PixelFormat!.Value;
                descriptor.ByteOrder = ByteOrder!.Value;
                descriptor.Width = Positive(Width, "Width");
                descriptor.Height = Positive(Height, "Height");
                descriptor.Stride = Positive(Stride, "Stride");
                descriptor.ValidBits = Positive(ValidBits, "Valid bits");
                var maximumValidBits = RawImageDescriptor.GetDefaultValidBits(descriptor.PixelFormat);
                // Preserve the exact declaration from older color snapshots that stored total pixel bits.
                var isDeclaredColorStorageDepth = _declared != null
                    && descriptor.PixelFormat == _declared.PixelFormat
                    && descriptor.ValidBits == _declared.ValidBits
                    && (descriptor.PixelFormat == RawPixelFormat.RGB24 || descriptor.PixelFormat == RawPixelFormat.BGR24 || descriptor.PixelFormat == RawPixelFormat.BGRA32)
                    && descriptor.ValidBits == descriptor.GetBytesPerPixel() * 8;
                if (descriptor.ValidBits > maximumValidBits && !isDeclaredColorStorageDepth)
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                        "Valid bits must be between 1 and {0} for {1}.", maximumValidBits, descriptor.PixelFormat));
                Applied?.Invoke(descriptor);
                _current = descriptor.Clone();
                Fill(descriptor);
            }
            catch (Exception ex) { ReportError(ex.Message); }
        }

        private void Reset()
        {
            var descriptor = _declared!.Clone();
            var current = _current!;
            Fill(descriptor);
            if (current.Width == descriptor.Width && current.Height == descriptor.Height && current.Stride == descriptor.Stride && current.PixelFormat == descriptor.PixelFormat && current.ValidBits == descriptor.ValidBits && current.ByteOrder == descriptor.ByteOrder) return;
            try { Applied?.Invoke(descriptor); _current = descriptor.Clone(); }
            catch (Exception ex) { ReportError(ex.Message); }
        }

        private void ReportError(string message)
        {
            Error = message;
            Failed?.Invoke(message);
            Notify();
        }

        private static int Positive(string text, string name)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0)
                throw new InvalidOperationException(name + " must be a positive integer.");
            return value;
        }

        private void Notify()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            ((ViewCommand)ApplyCommand).Refresh();
            ((ViewCommand)ResetCommand).Refresh();
        }
    }
}
