using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using RawBufferVisualizer.OpenGlCanvas;
using RawBufferVisualizer.Sdk;
using Line = System.Windows.Shapes.Line;

namespace RawBufferVisualizer.Wpf
{
    public partial class MainWindow : Window
    {
        private const long MaxCpuPreviewBytes = 512L * 1024L * 1024L;
        private const long MaxInMemorySourceBytes = 512L * 1024L * 1024L;

        private RawImageSource? _imageSource;
        private RawImageDescriptor _descriptor;
        private RenderedImage? _rendered;
        private string? _currentPath;
        private bool _syncingZoomSlider;

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
            OpenGlImageView.ClearImage();
            _imageSource?.Dispose();
            _imageSource = null;
            base.OnClosed(e);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormatBox.ItemsSource = Enum.GetValues(typeof(RawPixelFormat)).Cast<RawPixelFormat>();
            ByteOrderBox.ItemsSource = Enum.GetValues(typeof(RawByteOrder)).Cast<RawByteOrder>();
            OpenGlImageView.PixelHovered += OpenGlImageView_PixelHovered;
            OpenGlImageView.ViewChanged += OpenGlImageView_ViewChanged;
            ApplyDescriptorToFields();
            UpdateStatus();
            UpdateZoomStatus();
        }

        public void OpenPath(string path)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (fullPath.EndsWith(".rbuf.json", StringComparison.OrdinalIgnoreCase))
                {
                    var reference = RawBufferSnapshot.LoadReference(fullPath);
                    var source = CreateImageSource(reference.RawPath, reference.Descriptor, reference.RawByteLength);
                    _descriptor = reference.Descriptor;
                    _currentPath = fullPath;
                    ReplaceImageSource(source);
                    ApplyDescriptorToFields();
                }
                else
                {
                    _descriptor = ReadDescriptorFromFields();
                    var source = CreateImageSource(fullPath, _descriptor, new FileInfo(fullPath).Length);
                    _currentPath = fullPath;
                    ReplaceImageSource(source);
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

        private void OpenSample_Click(object sender, RoutedEventArgs e)
        {
            var sampleDirectory = FindSampleDirectory();
            var samplePath = sampleDirectory == null ? null : FindDefaultSamplePath(sampleDirectory);
            if (samplePath == null)
            {
                MessageBox.Show(this, "Sample files were not found.", "Open Sample", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenPath(samplePath);
        }

        private void OpenSampleFolder_Click(object sender, RoutedEventArgs e)
        {
            var sampleDirectory = FindSampleDirectory();
            if (sampleDirectory == null)
            {
                MessageBox.Show(this, "Sample files were not found.", "Sample Folder", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = sampleDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Sample Folder failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string? FindSampleDirectory()
        {
            var roots = new List<string>();
            AddSearchRoots(roots, AppContext.BaseDirectory);
            AddSearchRoots(roots, Environment.CurrentDirectory);

            foreach (var root in roots)
            {
                var candidates = new[]
                {
                    Path.Combine(root, "samples"),
                    Path.Combine(root, "artifacts", "samples")
                };

                foreach (var candidate in candidates)
                {
                    if (Directory.Exists(candidate) && Directory.EnumerateFiles(candidate, "*.rbuf.json").Any())
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static void AddSearchRoots(List<string> roots, string startPath)
        {
            var directory = new DirectoryInfo(Path.GetFullPath(startPath));
            for (var depth = 0; directory != null && depth < 8; depth++, directory = directory.Parent)
            {
                if (!roots.Contains(directory.FullName, StringComparer.OrdinalIgnoreCase))
                {
                    roots.Add(directory.FullName);
                }
            }
        }

        private static string? FindDefaultSamplePath(string sampleDirectory)
        {
            var preferred = Path.Combine(sampleDirectory, "rgb24-color.rbuf.json");
            if (File.Exists(preferred))
            {
                return preferred;
            }

            return Directory
                .EnumerateFiles(sampleDirectory, "*.rbuf.json")
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
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
            if (_imageSource == null)
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

                var diagnostics = RawBufferDiagnostics.AnalyzeLength(_imageSource.Length, _descriptor);
                if (RawBufferDiagnostics.HasErrors(diagnostics))
                {
                    throw new InvalidOperationException("Snapshot descriptor is invalid.");
                }

                var rawPath = RawBufferSnapshot.SaveMetadata(dialog.FileName, _descriptor);
                _imageSource.CopyRawTo(rawPath);
                _currentPath = Path.GetFullPath(dialog.FileName);
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
                if (_imageSource != null)
                {
                    ReplaceImageSource(_imageSource.WithDescriptor(_descriptor));
                }

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
            if (_imageSource == null)
            {
                StatusText.Text = "No buffer loaded";
                return;
            }

            var diagnostics = _imageSource.Analyze();
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
                OpenGlImageView.LoadRawImageSource(_imageSource);
            }
            catch (Exception ex)
            {
                ClearPreview();
                DiagnosticsList.Items.Add("Error: image display failed. " + ex.Message);
                UpdateStatus();
                return;
            }

            var estimatedPreviewBytes = RawImageTilePlanner.EstimateBgraByteCount(_descriptor);
            if (_imageSource.IsFileBacked || estimatedPreviewBytes > MaxCpuPreviewBytes)
            {
                _rendered = null;
                HistogramCanvas.Children.Clear();
                DiagnosticsList.Items.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    _imageSource.IsFileBacked
                        ? "Info: CPU histogram and PNG cache skipped because the source is file-backed. Display uses {1:N0} tiles."
                        : "Info: CPU histogram and PNG cache skipped because BGRA preview would require {0:N0} bytes. Display uses {1:N0} tiles.",
                    estimatedPreviewBytes,
                    OpenGlImageView.TileCount));
                FileText.Text = _currentPath ?? string.Empty;
                UpdateStatus();
                return;
            }

            _rendered = _imageSource.RenderTile(0, 0, _descriptor.Width, _descriptor.Height, null);
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
            if (_imageSource == null || e.X < 0 || e.Y < 0)
            {
                PixelText.Text = string.Empty;
                return;
            }

            PixelText.Text = _imageSource.DescribePixel(e.X, e.Y);
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

            if (_syncingZoomSlider)
            {
                return;
            }

            OpenGlImageView.SetZoomScale(e.NewValue);
            UpdateZoomStatus();
        }

        private void Fit_Click(object sender, RoutedEventArgs e)
        {
            OpenGlImageView.FitToImage();
            UpdateZoomStatus();
        }

        private void ActualSize_Click(object sender, RoutedEventArgs e)
        {
            OpenGlImageView.SetZoomScale(1);
            UpdateZoomStatus();
        }

        private void OpenGlImageView_ViewChanged(object? sender, EventArgs e)
        {
            UpdateZoomStatus();
        }

        private void UpdateZoomStatus()
        {
            if (ZoomText == null || ZoomSlider == null || OpenGlImageView == null)
            {
                return;
            }

            var zoom = OpenGlImageView.ZoomScale;
            var clampedZoom = Math.Max(ZoomSlider.Minimum, Math.Min(ZoomSlider.Maximum, zoom));
            _syncingZoomSlider = true;
            try
            {
                ZoomSlider.Value = clampedZoom;
            }
            finally
            {
                _syncingZoomSlider = false;
            }

            ZoomText.Text = string.Format(CultureInfo.InvariantCulture, "{0:0.#}%", zoom * 100);
        }

        private void UpdateStatus()
        {
            if (StatusText == null)
            {
                return;
            }

            if (_imageSource == null)
            {
                StatusText.Text = "Ready";
                return;
            }

            StatusText.Text = string.Format(
                CultureInfo.InvariantCulture,
                "{0} x {1}, {2}, {3:N0} bytes, tiles {4:N0}",
                _descriptor.Width,
                _descriptor.Height,
                _descriptor.PixelFormat,
                _imageSource.Length,
                OpenGlImageView.TileCount);
        }

        private void ReplaceImageSource(RawImageSource? source)
        {
            OpenGlImageView.ClearImage();
            var previous = _imageSource;
            _imageSource = source;
            if (!ReferenceEquals(previous, source))
            {
                previous?.Dispose();
            }
        }

        private static RawImageSource CreateImageSource(string rawPath, RawImageDescriptor descriptor, long rawByteLength)
        {
            if (rawByteLength > MaxInMemorySourceBytes)
            {
                if (!RawImageSource.CanStreamFormat(descriptor.PixelFormat))
                {
                    throw new NotSupportedException("This packed format is too large for in-memory loading and is not supported by file-backed tiled display yet.");
                }

                return RawImageSource.FromFile(rawPath, descriptor);
            }

            if (rawByteLength > int.MaxValue)
            {
                throw new InvalidOperationException("The raw payload is too large to load into a single byte array.");
            }

            return RawImageSource.FromMemory(File.ReadAllBytes(rawPath), descriptor);
        }
    }
}
