using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.OpenGlCanvas;
using RawBufferVisualizer.Sdk;
using Line = System.Windows.Shapes.Line;

namespace RawBufferVisualizer.Wpf
{
    public partial class MainWindow : Window
    {
        private const long MaxCpuPreviewBytes = 512L * 1024L * 1024L;

        private byte[]? _buffer;
        private RawImageDescriptor _descriptor;
        private RenderedImage? _rendered;
        private string? _currentPath;
        private string? _vrecTempDirectory;

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

        protected override void OnClosed(EventArgs e)
        {
            ClearVrecTempDirectory();
            base.OnClosed(e);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormatBox.ItemsSource = Enum.GetValues(typeof(RawPixelFormat)).Cast<RawPixelFormat>();
            ByteOrderBox.ItemsSource = Enum.GetValues(typeof(RawByteOrder)).Cast<RawByteOrder>();
            OpenGlImageView.PixelHovered += OpenGlImageView_PixelHovered;
            ApplyDescriptorToFields();
            UpdateStatus();
        }

        public void OpenPath(string path)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (fullPath.EndsWith(".vrec", StringComparison.OrdinalIgnoreCase))
                {
                    var snapshot = LoadFirstVrecImage(fullPath);
                    _buffer = snapshot.Buffer;
                    _descriptor = snapshot.Descriptor;
                    _currentPath = fullPath;
                    ApplyDescriptorToFields();
                }
                else if (fullPath.EndsWith(".rbuf.json", StringComparison.OrdinalIgnoreCase))
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
                Filter = "Vision Recording (*.vrec)|*.vrec|Raw Buffer Metadata (*.rbuf.json)|*.rbuf.json|Raw Buffer (*.raw;*.bin)|*.raw;*.bin|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) == true)
            {
                OpenPath(dialog.FileName);
            }
        }

        private void SavePng_Click(object sender, RoutedEventArgs e)
        {
            if (_rendered == null)
            {
                MessageBox.Show(this, "PNG export is available only when the CPU preview cache is enabled.", "Save PNG", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png",
                FileName = GetDefaultExportName(".png")
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            using (var stream = File.Create(dialog.FileName))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(CreateBitmapSource(_rendered)));
                encoder.Save(stream);
            }
        }

        private void SaveSnapshot_Click(object sender, RoutedEventArgs e)
        {
            if (_buffer == null)
            {
                MessageBox.Show(this, "No buffer to save.", "Save Snapshot", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                _descriptor = ReadDescriptorFromFields();
                var dialog = new SaveFileDialog
                {
                    Filter = "Raw Buffer Metadata (*.rbuf.json)|*.rbuf.json",
                    FileName = GetDefaultExportName(".rbuf.json")
                };

                if (dialog.ShowDialog(this) != true)
                {
                    return;
                }

                var snapshot = new RawBufferSnapshot(_buffer, _descriptor);
                snapshot.Save(dialog.FileName);
                _currentPath = snapshot.MetadataPath;
                FileText.Text = _currentPath ?? string.Empty;
                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Save Snapshot failed", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private string GetDefaultExportName(string extension)
        {
            var name = string.IsNullOrWhiteSpace(_currentPath)
                ? "raw-buffer"
                : Path.GetFileNameWithoutExtension(_currentPath);

            if (name.EndsWith(".rbuf", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - ".rbuf".Length);
            }

            return name + extension;
        }

        private RawBufferSnapshot LoadFirstVrecImage(string vrecPath)
        {
            ClearVrecTempDirectory();
            var tempRoot = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerVrec");
            _vrecTempDirectory = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_vrecTempDirectory);

            string? firstDescriptorPath = null;
            using (var stream = File.OpenRead(vrecPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries.OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(entry.Name) || !entry.FullName.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var extractedPath = ExtractVrecEntry(entry, _vrecTempDirectory);
                    if (firstDescriptorPath == null && entry.FullName.EndsWith(".rbuf.json", StringComparison.OrdinalIgnoreCase))
                    {
                        firstDescriptorPath = extractedPath;
                    }
                }
            }

            if (firstDescriptorPath == null)
            {
                throw new InvalidDataException("The VREC package does not contain an image descriptor.");
            }

            return RawBufferSnapshot.Load(firstDescriptorPath);
        }

        private static string ExtractVrecEntry(ZipArchiveEntry entry, string destinationRoot)
        {
            var relativePath = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, relativePath));
            var fullRoot = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!destinationPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Invalid VREC entry path: " + entry.FullName);
            }

            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var input = entry.Open())
            using (var output = File.Create(destinationPath))
            {
                input.CopyTo(output);
            }

            return destinationPath;
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
                ClearPreview();
                UpdateStatus();
                return;
            }

            try
            {
                OpenGlImageView.LoadRawBuffer(_buffer, _descriptor);
            }
            catch (Exception ex)
            {
                ClearPreview();
                DiagnosticsList.Items.Add("Error: OpenGL upload failed. " + ex.Message);
                UpdateStatus();
                return;
            }

            var estimatedPreviewBytes = RawImageTilePlanner.EstimateBgraByteCount(_descriptor);
            if (estimatedPreviewBytes > MaxCpuPreviewBytes)
            {
                _rendered = null;
                HistogramCanvas.Children.Clear();
                DiagnosticsList.Items.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Info: CPU histogram and PNG cache skipped because BGRA preview would require {0:N0} bytes. OpenGL uploaded {1:N0} tiles.",
                    estimatedPreviewBytes,
                    OpenGlImageView.TileCount));
                FileText.Text = _currentPath ?? string.Empty;
                UpdateStatus();
                return;
            }

            _rendered = RawBufferRenderer.Render(_buffer, _descriptor);
            FileText.Text = _currentPath ?? string.Empty;
            DrawHistogram();
            UpdateStatus();
        }

        private static BitmapSource CreateBitmapSource(RenderedImage image)
        {
            var bitmap = BitmapSource.Create(
                image.Width,
                image.Height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                image.Bgra32,
                image.Stride);
            bitmap.Freeze();
            return bitmap;
        }

        private void ClearPreview()
        {
            _rendered = null;
            OpenGlImageView.ClearImage();
            HistogramCanvas.Children.Clear();
        }

        private void ClearVrecTempDirectory()
        {
            if (string.IsNullOrEmpty(_vrecTempDirectory) || !Directory.Exists(_vrecTempDirectory))
            {
                return;
            }

            try
            {
                Directory.Delete(_vrecTempDirectory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            _vrecTempDirectory = null;
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

        private void OpenGlImageView_PixelHovered(object? sender, RawOpenGlPixelEventArgs e)
        {
            if (_buffer == null || e.X < 0 || e.Y < 0)
            {
                PixelText.Text = string.Empty;
                return;
            }

            PixelText.Text = RawPixelInspector.Describe(_buffer, _descriptor, e.X, e.Y);
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                OpenPath(files[0]);
            }
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OpenGlImageView == null)
            {
                return;
            }

            OpenGlImageView.SetZoomScale(e.NewValue);
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
                "{0} x {1}, {2}, {3:N0} bytes, GL tiles {4:N0}",
                _descriptor.Width,
                _descriptor.Height,
                _descriptor.PixelFormat,
                _buffer.Length,
                OpenGlImageView.TileCount);
        }
    }
}
