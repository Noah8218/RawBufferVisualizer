using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.OpenGlCanvas;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.VisualStudio;
using Line = System.Windows.Shapes.Line;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    public partial class RawBufferToolWindowControl : UserControl
    {
        private const long MaxCpuPreviewBytes = 512L * 1024L * 1024L;
        private const long MaxInMemorySourceBytes = 512L * 1024L * 1024L;

        private readonly ObservableCollection<ImageDocument> _documents = new ObservableCollection<ImageDocument>();
        private ImageDocument? _activeDocument;
        private bool _syncingZoomSlider;
        private bool _syncingDocumentSelection;
        private bool _switchingDocument;
        private bool _applyingLinkedView;
        private RawOpenGlViewState? _linkedViewState;

        public RawBufferToolWindowControl()
        {
            InitializeComponent();
            ImageList.ItemsSource = _documents;
            OpenGlImageView.PixelHovered += OpenGlImageView_PixelHovered;
            OpenGlImageView.ViewChanged += OpenGlImageView_ViewChanged;
            UpdateZoomStatus();
            UpdateStatus();
        }

        public void OpenHandoffRequest(string requestPath)
        {
            try
            {
                var metadataPath = ReadHandoffRequestWithRetry(requestPath);
                OpenPath(metadataPath);
            }
            catch (Exception ex)
            {
                DiagnosticsList.Items.Add("Error: handoff failed. " + ex.Message);
            }
        }

        public void OpenPath(string path)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                var reference = RawBufferSnapshot.LoadReference(fullPath);
                var source = CreateImageSource(reference.RawPath, reference.Descriptor, reference.RawByteLength);
                var document = new ImageDocument(fullPath, source, reference.Descriptor);
                _documents.Add(document);
                ActivateDocument(document);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Open failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Raw Buffer Metadata (*.rbuf.json)|*.rbuf.json|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                OpenPath(dialog.FileName);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            OpenGlImageView.ClearImage();
            foreach (var document in _documents)
            {
                document.Dispose();
            }

            _documents.Clear();
            _activeDocument = null;
            DescriptorText.Text = string.Empty;
            PixelText.Text = string.Empty;
            HistogramCanvas.Children.Clear();
            DiagnosticsList.Items.Clear();
            UpdateStatus();
        }

        private void ActivateDocument(ImageDocument document)
        {
            if (ReferenceEquals(_activeDocument, document))
            {
                return;
            }

            SaveActiveDocumentView();
            _activeDocument = document;

            _syncingDocumentSelection = true;
            try
            {
                ImageList.SelectedItem = document;
            }
            finally
            {
                _syncingDocumentSelection = false;
            }

            DescriptorText.Text = FormatDescriptor(document);
            _switchingDocument = true;
            try
            {
                RenderActiveDocument();
            }
            finally
            {
                _switchingDocument = false;
            }
        }

        private void RenderActiveDocument()
        {
            DiagnosticsList.Items.Clear();
            HistogramCanvas.Children.Clear();
            PixelText.Text = string.Empty;

            if (_activeDocument == null)
            {
                UpdateStatus();
                return;
            }

            var diagnostics = _activeDocument.Source.Analyze();
            foreach (var diagnostic in diagnostics)
            {
                DiagnosticsList.Items.Add(diagnostic.ToString());
            }

            if (RawBufferDiagnostics.HasErrors(diagnostics))
            {
                OpenGlImageView.ClearImage();
                UpdateStatus();
                return;
            }

            try
            {
                var viewState = GetViewStateForRender(_activeDocument);
                OpenGlImageView.LoadRawImageSource(_activeDocument.Source);
                ApplyRestoredView(viewState);
            }
            catch (Exception ex)
            {
                OpenGlImageView.ClearImage();
                DiagnosticsList.Items.Add("Error: image display failed. " + ex.Message);
                UpdateStatus();
                return;
            }

            DrawHistogramIfReasonable(_activeDocument);
            UpdateStatus();
        }

        private void DrawHistogramIfReasonable(ImageDocument document)
        {
            var estimatedPreviewBytes = RawImageTilePlanner.EstimateBgraByteCount(document.Descriptor);
            if (document.Source.IsFileBacked || estimatedPreviewBytes > MaxCpuPreviewBytes)
            {
                DiagnosticsList.Items.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    document.Source.IsFileBacked
                        ? "Info: CPU histogram skipped because the source is file-backed. Display uses {1:N0} tiles."
                        : "Info: CPU histogram skipped because BGRA preview would require {0:N0} bytes. Display uses {1:N0} tiles.",
                    estimatedPreviewBytes,
                    OpenGlImageView.TileCount));
                return;
            }

            var rendered = document.Source.RenderTile(0, 0, document.Descriptor.Width, document.Descriptor.Height, null);
            DrawHistogram(rendered);
        }

        private void DrawHistogram(RenderedImage rendered)
        {
            var bins = new int[256];
            for (var i = 0; i < rendered.Bgra32.Length; i += 4)
            {
                bins[(rendered.Bgra32[i] + rendered.Bgra32[i + 1] + rendered.Bgra32[i + 2]) / 3]++;
            }

            var max = bins.Max();
            if (max <= 0)
            {
                return;
            }

            var width = HistogramCanvas.ActualWidth > 0 ? HistogramCanvas.ActualWidth : 230;
            var height = HistogramCanvas.ActualHeight > 0 ? HistogramCanvas.ActualHeight : 110;
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

        private RawOpenGlViewState? GetViewStateForRender(ImageDocument document)
        {
            if (LinkViewsBox.IsChecked == true && _linkedViewState != null && _linkedViewState.Matches(document.Descriptor.Width, document.Descriptor.Height))
            {
                return _linkedViewState;
            }

            return document.ViewState;
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

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncingZoomSlider || OpenGlImageView == null)
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

        private void LinkViewsBox_Changed(object sender, RoutedEventArgs e)
        {
            if (LinkViewsBox.IsChecked == true)
            {
                _linkedViewState = OpenGlImageView.GetViewState();
                if (_activeDocument != null)
                {
                    _activeDocument.ViewState = _linkedViewState;
                }
            }
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

        private void OpenGlImageView_PixelHovered(object? sender, RawOpenGlPixelEventArgs e)
        {
            if (_activeDocument == null
                || e.X < 0
                || e.Y < 0
                || e.X >= _activeDocument.Descriptor.Width
                || e.Y >= _activeDocument.Descriptor.Height)
            {
                PixelText.Text = string.Empty;
                return;
            }

            PixelText.Text = _activeDocument.Source.DescribePixel(e.X, e.Y);
        }

        private void UpdateZoomStatus()
        {
            if (ZoomText == null || ZoomSlider == null || OpenGlImageView == null)
            {
                return;
            }

            var zoom = OpenGlImageView.ZoomScale;
            _syncingZoomSlider = true;
            try
            {
                ZoomSlider.Value = Math.Max(ZoomSlider.Minimum, Math.Min(ZoomSlider.Maximum, zoom));
            }
            finally
            {
                _syncingZoomSlider = false;
            }

            ZoomText.Text = string.Format(CultureInfo.InvariantCulture, "{0:0.#}%", zoom * 100);
        }

        private void UpdateStatus()
        {
            if (_activeDocument == null)
            {
                StatusText.Text = string.Format(CultureInfo.InvariantCulture, "{0:N0} images", _documents.Count);
                return;
            }

            StatusText.Text = string.Format(
                CultureInfo.InvariantCulture,
                "{0} x {1}, {2}, {3:N0} bytes, tiles {4:N0}",
                _activeDocument.Descriptor.Width,
                _activeDocument.Descriptor.Height,
                _activeDocument.Descriptor.PixelFormat,
                _activeDocument.Source.Length,
                OpenGlImageView.TileCount);
        }

        private static string ReadHandoffRequestWithRetry(string requestPath)
        {
            Exception? last = null;
            for (var i = 0; i < 5; i++)
            {
                try
                {
                    return VisualizerHandoffInbox.ReadSnapshotRequest(requestPath);
                }
                catch (IOException ex)
                {
                    last = ex;
                    Thread.Sleep(50);
                }
            }

            throw last ?? new IOException("Handoff request could not be read.");
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
                var bitmap = BitmapSource.Create(
                    rendered.Width,
                    rendered.Height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    rendered.Bgra32,
                    rendered.Stride);
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private static string FormatDescriptor(ImageDocument document)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Width      {0}\nHeight     {1}\nStride     {2}\nFormat     {3}\nValid Bits {4}\nByte Order {5}\nBytes      {6:N0}\nFile       {7}",
                document.Descriptor.Width,
                document.Descriptor.Height,
                document.Descriptor.Stride,
                document.Descriptor.PixelFormat,
                document.Descriptor.ValidBits,
                document.Descriptor.ByteOrder,
                document.Source.Length,
                document.DisplayPath);
        }

        private sealed class ImageDocument : IDisposable
        {
            public string DisplayPath { get; private set; }
            public string Title { get; private set; }
            public RawImageSource Source { get; private set; }
            public RawImageDescriptor Descriptor { get; private set; }
            public RawOpenGlViewState? ViewState { get; set; }
            public BitmapSource? Thumbnail { get; private set; }

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
            }

            public void Dispose()
            {
                Source.Dispose();
            }

            private static string CreateTitle(string displayPath)
            {
                var fileName = Path.GetFileName(displayPath);
                const string suffix = ".rbuf.json";
                return fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                    ? fileName.Substring(0, fileName.Length - suffix.Length)
                    : fileName;
            }
        }
    }
}
