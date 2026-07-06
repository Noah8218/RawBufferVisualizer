using System;
using System.Collections.ObjectModel;
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

        private readonly ObservableCollection<ImageDocument> _documents = new ObservableCollection<ImageDocument>();
        private RawImageSource? _imageSource;
        private RawImageDescriptor _descriptor;
        private RenderedImage? _rendered;
        private string? _currentPath;
        private ImageDocument? _activeDocument;
        private bool _syncingZoomSlider;
        private bool _syncingDocumentSelection;
        private bool _switchingDocument;
        private bool _applyingLinkedView;
        private RawOpenGlViewState? _linkedViewState;

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
            foreach (var document in _documents)
            {
                document.Dispose();
            }

            _documents.Clear();
            _imageSource = null;
            _activeDocument = null;
            base.OnClosed(e);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ImageList.ItemsSource = _documents;
            DocumentTabs.ItemsSource = _documents;
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
                ImageDocument document;
                if (fullPath.EndsWith(".rbuf.json", StringComparison.OrdinalIgnoreCase))
                {
                    var reference = RawBufferSnapshot.LoadReference(fullPath);
                    var source = CreateImageSource(reference.RawPath, reference.Descriptor, reference.RawByteLength);
                    document = new ImageDocument(fullPath, source, reference.Descriptor);
                }
                else
                {
                    var descriptor = ReadDescriptorFromFields();
                    var source = CreateImageSource(fullPath, descriptor, new FileInfo(fullPath).Length);
                    document = new ImageDocument(fullPath, source, descriptor);
                }

                _documents.Add(document);
                ActivateDocument(document);
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
                Filter = "Raw Buffer Metadata (*.rbuf.json)|*.rbuf.json|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) == true)
            {
                OpenPath(dialog.FileName);
            }
        }

        private void ActivateDocument(ImageDocument document)
        {
            if (document == null || ReferenceEquals(_activeDocument, document))
            {
                return;
            }

            SaveActiveDocumentView();
            _activeDocument = document;
            _imageSource = document.Source;
            _descriptor = document.Descriptor.Clone();
            _rendered = document.Rendered;
            _currentPath = document.DisplayPath;

            _syncingDocumentSelection = true;
            try
            {
                ImageList.SelectedItem = document;
                DocumentTabs.SelectedItem = document;
            }
            finally
            {
                _syncingDocumentSelection = false;
            }

            ApplyDescriptorToFields();
            FileText.Text = _currentPath ?? string.Empty;
            _switchingDocument = true;
            try
            {
                RenderCurrentBuffer();
            }
            finally
            {
                _switchingDocument = false;
            }
        }

        private void SaveActiveDocumentView()
        {
            if (_activeDocument == null)
            {
                return;
            }

            var viewState = OpenGlImageView.GetViewState();
            if (viewState != null)
            {
                _activeDocument.ViewState = viewState;
            }
        }

        private RawOpenGlViewState? GetViewStateForRender()
        {
            if (_activeDocument == null)
            {
                return null;
            }

            if (LinkViewsBox.IsChecked == true && _linkedViewState != null && _linkedViewState.Matches(_descriptor.Width, _descriptor.Height))
            {
                return _linkedViewState;
            }

            return _activeDocument.ViewState;
        }

        private void ApplyRestoredView(RawOpenGlViewState? viewState)
        {
            if (viewState == null)
            {
                return;
            }

            _applyingLinkedView = true;
            try
            {
                OpenGlImageView.TryApplyViewState(viewState);
            }
            finally
            {
                _applyingLinkedView = false;
            }
        }

        private void RefreshDocumentSelectors()
        {
            ImageList.Items.Refresh();
            DocumentTabs.Items.Refresh();
        }

        private void SavePng_Click(object sender, RoutedEventArgs e)
        {
            if (_rendered == null)
            {
                MessageBox.Show(this, "PNG export is available only when the CPU preview cache is enabled.", "Export PNG", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show(this, "No buffer to save.", "Export Snapshot", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                _descriptor = _activeDocument == null ? _descriptor : _activeDocument.Descriptor.Clone();
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
                _activeDocument?.SetPath(_currentPath);
                FileText.Text = _currentPath ?? string.Empty;
                RefreshDocumentSelectors();
                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Export Snapshot failed", MessageBoxButton.OK, MessageBoxImage.Error);
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
            return _descriptor.Clone();
        }

        private void ApplyDescriptorToFields()
        {
            WidthBox.Text = _descriptor.Width.ToString(CultureInfo.InvariantCulture);
            HeightBox.Text = _descriptor.Height.ToString(CultureInfo.InvariantCulture);
            StrideBox.Text = _descriptor.Stride.ToString(CultureInfo.InvariantCulture);
            ValidBitsBox.Text = _descriptor.ValidBits.ToString(CultureInfo.InvariantCulture);
            FormatBox.Text = _descriptor.PixelFormat.ToString();
            ByteOrderBox.Text = _descriptor.ByteOrder.ToString();
        }

        private void RenderCurrentBuffer()
        {
            DiagnosticsList.Items.Clear();
            if (_imageSource == null)
            {
                StatusText.Text = "Ready";
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
                var viewState = GetViewStateForRender();
                OpenGlImageView.LoadRawImageSource(_imageSource);
                ApplyRestoredView(viewState);
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
                if (_activeDocument != null)
                {
                    _activeDocument.Rendered = null;
                }

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
            if (_activeDocument != null)
            {
                _activeDocument.Rendered = _rendered;
            }

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

        private static BitmapSource? CreateThumbnailSource(RawImageSource source, RawImageDescriptor descriptor)
        {
            try
            {
                var diagnostics = source.Analyze();
                if (RawBufferDiagnostics.HasErrors(diagnostics))
                {
                    return null;
                }

                const int targetWidth = 96;
                const int targetHeight = 72;
                var stepX = Math.Max(1, (int)Math.Ceiling(descriptor.Width / (double)targetWidth));
                var stepY = Math.Max(1, (int)Math.Ceiling(descriptor.Height / (double)targetHeight));
                var sampleStep = Math.Max(stepX, stepY);
                var rendered = source.RenderTileSampled(0, 0, descriptor.Width, descriptor.Height, sampleStep, source.CreateRenderOptions());
                return CreateBitmapSource(rendered);
            }
            catch
            {
                return null;
            }
        }

        private void ClearPreview()
        {
            _rendered = null;
            if (_activeDocument != null)
            {
                _activeDocument.Rendered = null;
            }

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
            foreach (var file in files)
            {
                OpenPath(file);
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
            if (_switchingDocument || _applyingLinkedView || _activeDocument == null)
            {
                return;
            }

            var viewState = OpenGlImageView.GetViewState();
            if (viewState == null)
            {
                return;
            }

            _activeDocument.ViewState = viewState;
            if (LinkViewsBox.IsChecked == true)
            {
                _linkedViewState = viewState;
            }
        }

        private void ImageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingDocumentSelection)
            {
                return;
            }

            var document = ImageList.SelectedItem as ImageDocument;
            if (document != null)
            {
                ActivateDocument(document);
            }
        }

        private void ImageList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Delete)
            {
                return;
            }

            RemoveSelectedDocument();
            e.Handled = true;
        }

        private void RemoveSelectedDocument()
        {
            var document = ImageList.SelectedItem as ImageDocument;
            if (document == null)
            {
                return;
            }

            var index = _documents.IndexOf(document);
            if (index < 0)
            {
                return;
            }

            var wasActive = ReferenceEquals(_activeDocument, document);
            if (wasActive)
            {
                _activeDocument = null;
                _imageSource = null;
                _rendered = null;
                _currentPath = null;
            }

            _syncingDocumentSelection = true;
            try
            {
                _documents.RemoveAt(index);
            }
            finally
            {
                _syncingDocumentSelection = false;
            }

            document.Dispose();

            if (_documents.Count > 0)
            {
                ActivateDocument(_documents[Math.Min(index, _documents.Count - 1)]);
            }
            else
            {
                ClearPreview();
                FileText.Text = string.Empty;
                PixelText.Text = string.Empty;
                DiagnosticsList.Items.Clear();
                HistogramCanvas.Children.Clear();
                UpdateStatus();
                UpdateZoomStatus();
            }
        }

        private void DocumentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingDocumentSelection)
            {
                return;
            }

            var document = DocumentTabs.SelectedItem as ImageDocument;
            if (document != null)
            {
                ActivateDocument(document);
            }
        }

        private void LinkViewsBox_Changed(object sender, RoutedEventArgs e)
        {
            if (LinkViewsBox.IsChecked == true)
            {
                _linkedViewState = OpenGlImageView.GetViewState();
                if (_activeDocument != null && _linkedViewState != null)
                {
                    _activeDocument.ViewState = _linkedViewState;
                }
            }
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
            if (_activeDocument != null)
            {
                _activeDocument.Status = StatusText.Text;
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

        private sealed class ImageDocument : IDisposable
        {
            public string DisplayPath { get; private set; }
            public string Title { get; private set; }
            public RawImageSource Source { get; set; }
            public RawImageDescriptor Descriptor { get; set; }
            public RenderedImage? Rendered { get; set; }
            public RawOpenGlViewState? ViewState { get; set; }
            public BitmapSource? Thumbnail { get; private set; }
            public string Status { get; set; }

            public string Summary
            {
                get
                {
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} x {1}  {2}",
                        Descriptor.Width,
                        Descriptor.Height,
                        Descriptor.PixelFormat);
                }
            }

            public ImageDocument(string displayPath, RawImageSource source, RawImageDescriptor descriptor)
            {
                DisplayPath = Path.GetFullPath(displayPath);
                Title = CreateTitle(DisplayPath);
                Source = source ?? throw new ArgumentNullException("source");
                Descriptor = descriptor == null ? throw new ArgumentNullException("descriptor") : descriptor.Clone();
                Thumbnail = CreateThumbnailSource(Source, Descriptor);
                Status = string.Empty;
            }

            public void SetPath(string displayPath)
            {
                DisplayPath = Path.GetFullPath(displayPath);
                Title = CreateTitle(DisplayPath);
            }

            public void Dispose()
            {
                Source.Dispose();
            }

            private static string CreateTitle(string displayPath)
            {
                var fileName = Path.GetFileName(displayPath);
                const string metadataSuffix = ".rbuf.json";
                if (fileName.EndsWith(metadataSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    return fileName.Substring(0, fileName.Length - metadataSuffix.Length);
                }

                return fileName;
            }
        }
    }
}
