using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;
using Line = System.Windows.Shapes.Line;

namespace RawBufferVisualizer.Wpf
{
    public partial class MainWindow : Window
    {
        private byte[]? _buffer;
        private RawImageDescriptor _descriptor;
        private RenderedImage? _rendered;
        private string? _currentPath;

        public MainWindow()
        {
            InitializeComponent();
            _descriptor = new RawImageDescriptor
            {
                Width = 640,
                Height = 480,
                Stride = 640,
                PixelFormat = RawPixelFormat.Mono8,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormatBox.ItemsSource = Enum.GetValues(typeof(RawPixelFormat)).Cast<RawPixelFormat>();
            ByteOrderBox.ItemsSource = Enum.GetValues(typeof(RawByteOrder)).Cast<RawByteOrder>();
            ApplyDescriptorToFields();
            UpdateStatus();
        }

        public void OpenPath(string path)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (fullPath.EndsWith(".rbuf.json", StringComparison.OrdinalIgnoreCase))
                {
                    var snapshot = RawBufferSnapshot.Load(fullPath);
                    _buffer = snapshot.Buffer;
                    _descriptor = snapshot.Descriptor;
                    _currentPath = fullPath;
                    ApplyDescriptorToFields();
                }
                else
                {
                    _buffer = File.ReadAllBytes(fullPath);
                    _descriptor = ReadDescriptorFromFields();
                    _currentPath = fullPath;
                }

                RenderCurrentBuffer();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Open failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Raw Buffer Metadata (*.rbuf.json)|*.rbuf.json|Raw Buffer (*.raw;*.bin)|*.raw;*.bin|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) == true)
            {
                OpenPath(dialog.FileName);
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _descriptor = ReadDescriptorFromFields();
                RenderCurrentBuffer();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Apply failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private RawImageDescriptor ReadDescriptorFromFields()
        {
            var format = FormatBox.SelectedItem is RawPixelFormat selectedFormat ? selectedFormat : RawPixelFormat.Mono8;
            var byteOrder = ByteOrderBox.SelectedItem is RawByteOrder selectedByteOrder ? selectedByteOrder : RawByteOrder.LittleEndian;

            return new RawImageDescriptor
            {
                Width = ParseInt(WidthBox.Text, "Width"),
                Height = ParseInt(HeightBox.Text, "Height"),
                Stride = ParseInt(StrideBox.Text, "Stride"),
                PixelFormat = format,
                ValidBits = ParseInt(ValidBitsBox.Text, "Valid Bits"),
                ByteOrder = byteOrder
            };
        }

        private static int ParseInt(string value, string name)
        {
            int result;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                throw new InvalidOperationException(name + " must be an integer.");
            }

            return result;
        }

        private void ApplyDescriptorToFields()
        {
            WidthBox.Text = _descriptor.Width.ToString(CultureInfo.InvariantCulture);
            HeightBox.Text = _descriptor.Height.ToString(CultureInfo.InvariantCulture);
            StrideBox.Text = _descriptor.Stride.ToString(CultureInfo.InvariantCulture);
            ValidBitsBox.Text = _descriptor.ValidBits.ToString(CultureInfo.InvariantCulture);
            FormatBox.SelectedItem = _descriptor.PixelFormat;
            ByteOrderBox.SelectedItem = _descriptor.ByteOrder;
        }

        private void RenderCurrentBuffer()
        {
            DiagnosticsList.Items.Clear();
            if (_buffer == null)
            {
                StatusText.Text = "No buffer loaded";
                return;
            }

            var diagnostics = RawBufferDiagnostics.Analyze(_buffer, _descriptor);
            foreach (var diagnostic in diagnostics)
            {
                DiagnosticsList.Items.Add(diagnostic.ToString());
            }

            if (RawBufferDiagnostics.HasErrors(diagnostics))
            {
                ImageView.Source = null;
                _rendered = null;
                UpdateStatus();
                return;
            }

            _rendered = RawBufferRenderer.Render(_buffer, _descriptor);
            var bitmap = BitmapSource.Create(
                _rendered.Width,
                _rendered.Height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                _rendered.Bgra32,
                _rendered.Stride);
            bitmap.Freeze();
            ImageView.Source = bitmap;
            FileText.Text = _currentPath ?? string.Empty;
            DrawHistogram();
            UpdateStatus();
        }

        private void DrawHistogram()
        {
            HistogramCanvas.Children.Clear();
            if (_rendered == null)
            {
                return;
            }

            var bins = new int[256];
            for (var i = 0; i < _rendered.Bgra32.Length; i += 4)
            {
                var b = _rendered.Bgra32[i];
                var g = _rendered.Bgra32[i + 1];
                var r = _rendered.Bgra32[i + 2];
                bins[(r + g + b) / 3]++;
            }

            var max = bins.Max();
            if (max <= 0)
            {
                return;
            }

            var width = HistogramCanvas.ActualWidth > 0 ? HistogramCanvas.ActualWidth : 260;
            var height = HistogramCanvas.ActualHeight > 0 ? HistogramCanvas.ActualHeight : 130;
            for (var i = 0; i < bins.Length; i++)
            {
                var x = (i / 255.0) * width;
                var lineHeight = (bins[i] / (double)max) * height;
                HistogramCanvas.Children.Add(new Line
                {
                    X1 = x,
                    X2 = x,
                    Y1 = height,
                    Y2 = height - lineHeight,
                    Stroke = Brushes.SteelBlue,
                    StrokeThickness = 1
                });
            }
        }

        private void ImageView_MouseMove(object sender, MouseEventArgs e)
        {
            if (_buffer == null || _rendered == null || ImageView.Source == null)
            {
                return;
            }

            var position = e.GetPosition(ImageView);
            var zoom = Math.Max(ZoomSlider.Value, 0.1);
            var x = (int)(position.X / zoom);
            var y = (int)(position.Y / zoom);
            PixelText.Text = RawPixelInspector.Describe(_buffer, _descriptor, x, y);
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ImageScale == null)
            {
                return;
            }

            ImageScale.ScaleX = e.NewValue;
            ImageScale.ScaleY = e.NewValue;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (StatusText == null)
            {
                return;
            }

            if (_buffer == null)
            {
                StatusText.Text = "Ready";
                return;
            }

            StatusText.Text = string.Format(
                CultureInfo.InvariantCulture,
                "{0} x {1}, {2}, {3:N0} bytes",
                _descriptor.Width,
                _descriptor.Height,
                _descriptor.PixelFormat,
                _buffer.Length);
        }
    }
}
