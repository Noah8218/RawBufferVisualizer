using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
        private const int HistogramMaxSampleDimension = 1024;

        private enum LayoutMode
        {
            Unknown,
            Narrow,
            Medium,
            Wide
        }

        private readonly ObservableCollection<ImageDocument> _documents = new ObservableCollection<ImageDocument>();
        private readonly DispatcherTimer _performanceTimer;
        private ImageDocument? _activeDocument;
        private LayoutMode _layoutMode = LayoutMode.Unknown;
        private bool _syncingZoomSlider;
        private bool _syncingDocumentSelection;
        private bool _switchingDocument;
        private bool _applyingLinkedView;
        private bool _automationProbeRunning;
        private RawOpenGlViewState? _linkedViewState;
        private ImageDocument? _compareA;
        private ImageDocument? _compareB;
        private readonly DispatcherTimer _blinkTimer;
        private bool _blinkShowingA;
        private double _lastZoomStatus = double.NaN;
        private double _lastOpenPathMilliseconds;
        private int _lastHoverX = -1;
        private int _lastHoverY = -1;
        private int _pinnedInspectorX = -1;
        private int _pinnedInspectorY = -1;
        private double _lastCompactInspectorHeight = 168;
        private string _lastFramebufferCapturePath = string.Empty;
        private string _lastFramebufferCaptureError = string.Empty;

        public RawBufferToolWindowControl()
        {
            InitializeComponent();
            ImageList.ItemsSource = _documents;
            OpenGlImageView.PixelHovered += OpenGlImageView_PixelHovered;
            OpenGlImageView.PixelPinned += OpenGlImageView_PixelPinned;
            OpenGlImageView.PixelSelected += OpenGlImageView_PixelSelected;
            OpenGlImageView.ViewChanged += OpenGlImageView_ViewChanged;
            OpenGlImageView.SelectionOverlayEnabled = SelectionOverlayBox.IsChecked == true;
            InterpretPixelFormatBox.ItemsSource = Enum.GetValues(typeof(RawPixelFormat));
            InterpretByteOrderBox.ItemsSource = Enum.GetValues(typeof(RawByteOrder));
            _performanceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _performanceTimer.Tick += delegate { UpdatePerformanceText(); };
            _performanceTimer.Start();
            Unloaded += delegate { _performanceTimer.Stop(); };
            _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _blinkTimer.Tick += BlinkTimer_Tick;
            Unloaded += delegate { _blinkTimer.Stop(); };
            ClearPixelStatus();
            UpdateZoomStatus();
            UpdatePerformanceText();
            UpdateCompareText();
            UpdateStatus();
            UpdateTempUsageStatus();
            ApplyResponsiveLayout(ActualWidth);
        }

        public void OpenHandoffRequest(string requestPath)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OpenHandoffRequest(requestPath));
                return;
            }

            try
            {
                SetTransientStatus("Loading handoff...");
                var request = ReadHandoffRequestWithRetry(requestPath);
                VisualizerHandoffInbox.TryDeleteRequest(requestPath);
                if (request.IsError)
                {
                    var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
                        ? (string.IsNullOrWhiteSpace(request.SourceType) ? "Debugger visualizer" : request.SourceType)
                        : request.DisplayName;
                    AddErrorDocument(displayName, new InvalidOperationException(request.ErrorMessage));
                }
                else
                {
                    OpenPath(request.MetadataPath, request.DisplayName, request.SourceType);
                }
            }
            catch (Exception ex)
            {
                DiagnosticsList.Items.Add("Error: handoff failed. " + ex.Message);
                WriteAutomationProbeFailureIfRequested(requestPath, ex);
            }
            finally
            {
                VisualizerHandoffInbox.TryDeleteRequest(requestPath);
                UpdateTempUsageStatus();
            }
        }

        public void OpenPath(string path)
        {
            OpenPath(path, null, null);
        }

        private void OpenPath(string path, string? title, string? sourceType)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OpenPath(path, title, sourceType));
                return;
            }

            var openWatch = Stopwatch.StartNew();
            var fullPath = path;
            try
            {
                fullPath = Path.GetFullPath(path);
                SetTransientStatus("Opening " + Path.GetFileName(fullPath));
                var reference = RawBufferSnapshot.LoadReference(fullPath);
                var source = CreateImageSource(reference.RawPath, reference.Descriptor, reference.RawByteLength);
                var resolvedSourceType = string.IsNullOrWhiteSpace(sourceType) ? "Raw snapshot" : sourceType!;
                var document = new ImageDocument(
                    fullPath,
                    source,
                    reference.Descriptor,
                    title,
                    resolvedSourceType,
                    ShouldDeleteSnapshotDirectoryOnDispose(fullPath));
                _documents.Add(document);
                ActivateDocument(document);
                DiagnosticsList.Items.Insert(0, string.Format(
                    CultureInfo.InvariantCulture,
                    "Info: {0} source ready, {1}, open {2:0.0} ms.",
                    GetSourceMode(document.Source),
                    FormatByteCount(document.Source.Length),
                    openWatch.Elapsed.TotalMilliseconds));
                _lastOpenPathMilliseconds = openWatch.Elapsed.TotalMilliseconds;
                ScheduleAutomationProbeIfRequested(fullPath);
            }
            catch (Exception ex)
            {
                _lastOpenPathMilliseconds = openWatch.Elapsed.TotalMilliseconds;
                AddErrorDocument(fullPath, ex);
                WriteAutomationProbeFailureIfRequested(fullPath, ex);
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RAWBUFFERVISUALIZER_DOCKED_PERF_JSON")))
                {
                    DiagnosticsList.Items.Add("Open failed details: " + ex.GetType().Name);
                }
            }
            finally
            {
                openWatch.Stop();
                UpdateTempUsageStatus();
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
            _compareA = null;
            _compareB = null;
            _blinkTimer.Stop();
            DescriptorText.Text = string.Empty;
            SetPixelDetails(string.Empty, string.Empty, string.Empty, string.Empty);
            SetMarkerText(string.Empty);
            ClearPixelStatus();
            HistogramCanvas.Children.Clear();
            DiagnosticsList.Items.Clear();
            OpenGlImageView.ResetRenderStats();
            UpdatePerformanceText();
            UpdateCompareText();
            UpdateStatus();
            UpdateTempUsageStatus();
        }

        private void SaveVisiblePng_Click(object sender, RoutedEventArgs e)
        {
            if (!CanSaveActiveImage("Save PNG"))
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png",
                FileName = GetDefaultExportName("-view.png")
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                OpenGlImageView.SaveFramebufferPng(dialog.FileName);
                DiagnosticsList.Items.Add("Info: saved visible PNG to " + dialog.FileName);
            }
            catch (Exception ex)
            {
                DiagnosticsList.Items.Add("Error: PNG export failed. " + ex.Message);
                MessageBox.Show(ex.Message, "Save PNG failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveSnapshot_Click(object sender, RoutedEventArgs e)
        {
            if (!CanSaveActiveImage("Save Snapshot"))
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Raw Buffer Metadata (*.rbuf.json)|*.rbuf.json",
                FileName = GetDefaultExportName(".rbuf.json")
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var rawPath = RawBufferSnapshot.SaveMetadata(dialog.FileName, _activeDocument!.Descriptor);
                _activeDocument.Source.CopyRawTo(rawPath);
                DiagnosticsList.Items.Add("Info: saved raw snapshot to " + dialog.FileName);
            }
            catch (Exception ex)
            {
                DiagnosticsList.Items.Add("Error: snapshot export failed. " + ex.Message);
                MessageBox.Show(ex.Message, "Save Snapshot failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanSaveActiveImage(string title)
        {
            if (_activeDocument == null)
            {
                MessageBox.Show("Select an image before saving.", title, MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            if (_activeDocument.IsError)
            {
                MessageBox.Show("Error rows cannot be saved as images.", title, MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            return true;
        }

        private string GetDefaultExportName(string suffix)
        {
            var title = _activeDocument == null ? "raw-buffer" : _activeDocument.Title;
            return SanitizeFileName(title) + suffix;
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                builder.Append(invalid.Contains(ch) ? '_' : ch);
            }

            var result = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? "raw-buffer" : result;
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
            UpdateInterpretationControls(document.Descriptor);
            UpdateCompareText();
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
            _lastHoverX = -1;
            _lastHoverY = -1;
            _pinnedInspectorX = -1;
            _pinnedInspectorY = -1;
            SetInspectorPinnedState(false);
            SetMarkerText(string.Empty);
            SetPixelDetails(string.Empty, string.Empty, string.Empty, string.Empty);
            ClearPixelStatus();

            if (_activeDocument == null)
            {
                UpdateStatus();
                return;
            }

            if (_activeDocument.IsError)
            {
                OpenGlImageView.ClearImage();
                DiagnosticsList.Items.Add("Error: " + _activeDocument.ErrorMessage);
                UpdatePerformanceText();
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
                OpenGlImageView.ResetRenderStats();
                OpenGlImageView.LoadRawImageSource(_activeDocument.Source);
                UpdateLevelsControls();
                ApplyRestoredView(viewState);
                UpdatePerformanceText();
            }
            catch (Exception ex)
            {
                OpenGlImageView.ClearImage();
                UpdatePerformanceText();
                DiagnosticsList.Items.Add("Error: image display failed. " + ex.Message);
                UpdateStatus();
                return;
            }

            DrawHistogramIfReasonable(_activeDocument);
            UpdateStatus();
        }

        private void AddErrorDocument(string displayPath, Exception exception)
        {
            var document = ImageDocument.CreateError(
                displayPath,
                exception.Message,
                ShouldDeleteSnapshotDirectoryOnDispose(displayPath));
            _documents.Add(document);
            ActivateDocument(document);
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

            var sampleStep = GetHistogramSampleStep(document.Descriptor);
            var rendered = document.Source.RenderTileSampled(0, 0, document.Descriptor.Width, document.Descriptor.Height, sampleStep, document.Source.CreateRenderOptions());
            if (sampleStep > 1)
            {
                DiagnosticsList.Items.Add(string.Format(CultureInfo.InvariantCulture, "Info: histogram sampled every {0:N0} pixels for responsiveness.", sampleStep));
            }

            DrawHistogram(rendered);
        }

        private static int GetHistogramSampleStep(RawImageDescriptor descriptor)
        {
            var maxDimension = Math.Max(descriptor.Width, descriptor.Height);
            return Math.Max(1, (int)Math.Ceiling(maxDimension / (double)HistogramMaxSampleDimension));
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
            if (ReferenceEquals(_compareA, document))
            {
                _compareA = null;
            }

            if (ReferenceEquals(_compareB, document))
            {
                _compareB = null;
            }

            if (wasActive)
            {
                _activeDocument = null;
                _blinkTimer.Stop();
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
                OpenGlImageView.ClearImage();
                DescriptorText.Text = string.Empty;
                DiagnosticsList.Items.Clear();
                HistogramCanvas.Children.Clear();
                SetPixelDetails(string.Empty, string.Empty, string.Empty, string.Empty);
                SetMarkerText(string.Empty);
                ClearPixelStatus();
                OpenGlImageView.ClearPinnedMarker();
                UpdatePerformanceText();
                UpdateCompareText();
                UpdateStatus();
            }

            UpdateTempUsageStatus();
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

        private void ApplyInterpretation_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDocument == null)
            {
                return;
            }

            try
            {
                var descriptor = _activeDocument.Descriptor.Clone();
                descriptor.PixelFormat = (RawPixelFormat)InterpretPixelFormatBox.SelectedItem;
                descriptor.ByteOrder = (RawByteOrder)InterpretByteOrderBox.SelectedItem;
                descriptor.Stride = ParsePositiveInt(InterpretStrideTextBox.Text, "Stride");
                descriptor.ValidBits = ParsePositiveInt(InterpretValidBitsTextBox.Text, "Valid bits");
                var source = _activeDocument.Source.WithDescriptor(descriptor);
                _activeDocument.ReplaceSource(source, descriptor);
                ImageList.Items.Refresh();
                DescriptorText.Text = FormatDescriptor(_activeDocument);
                UpdateInterpretationControls(descriptor);
                RenderActiveDocument();
            }
            catch (Exception ex)
            {
                DiagnosticsList.Items.Add("Error: reinterpret failed. " + ex.Message);
            }
        }

        private void SetCompareA_Click(object sender, RoutedEventArgs e)
        {
            _compareA = _activeDocument;
            UpdateCompareText();
        }

        private void SetCompareB_Click(object sender, RoutedEventArgs e)
        {
            _compareB = _activeDocument;
            UpdateCompareText();
        }

        private void ShowDiff_Click(object sender, RoutedEventArgs e)
        {
            if (_compareA == null || _compareB == null)
            {
                UpdateCompareText("Set A and B first.");
                return;
            }

            try
            {
                var source = new RawImageDifferenceSource(_compareA.Source, _compareB.Source);
                var document = new ImageDocument(
                    "A/B diff",
                    source,
                    source.Descriptor,
                    "Diff: " + _compareA.Title + " | " + _compareB.Title,
                    "A/B diff");
                _documents.Add(document);
                ActivateDocument(document);
            }
            catch (Exception ex)
            {
                UpdateCompareText("Diff failed: " + ex.Message);
            }
        }

        private void ShowSplit_Click(object sender, RoutedEventArgs e)
        {
            if (_compareA == null || _compareB == null)
            {
                UpdateCompareText("Set A and B first.");
                return;
            }

            try
            {
                var source = new RawImageSplitSource(_compareA.Source, _compareB.Source);
                var document = new ImageDocument(
                    "A/B split",
                    source,
                    source.Descriptor,
                    "Split: " + _compareA.Title + " | " + _compareB.Title,
                    "A/B split");
                _documents.Add(document);
                ActivateDocument(document);
            }
            catch (Exception ex)
            {
                UpdateCompareText("Split failed: " + ex.Message);
            }
        }

        private void Blink_Click(object sender, RoutedEventArgs e)
        {
            if (_compareA == null || _compareB == null)
            {
                UpdateCompareText("Set A and B first.");
                return;
            }

            if (_blinkTimer.IsEnabled)
            {
                _blinkTimer.Stop();
                BlinkButton.Content = "Blink";
                return;
            }

            _blinkShowingA = false;
            BlinkButton.Content = "Stop";
            _blinkTimer.Start();
        }

        private void BlinkTimer_Tick(object? sender, EventArgs e)
        {
            if (_compareA == null || _compareB == null)
            {
                _blinkTimer.Stop();
                BlinkButton.Content = "Blink";
                return;
            }

            _blinkShowingA = !_blinkShowingA;
            ActivateDocument(_blinkShowingA ? _compareA : _compareB);
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

        private void SelectionOverlayBox_Changed(object sender, RoutedEventArgs e)
        {
            if (OpenGlImageView != null && SelectionOverlayBox != null)
            {
                OpenGlImageView.SelectionOverlayEnabled = SelectionOverlayBox.IsChecked == true;
            }
        }

        private void InspectorToggleButton_Changed(object sender, RoutedEventArgs e)
        {
            ApplyResponsiveLayout(ActualWidth);
        }

        private void OpenGlImageView_ViewChanged(object? sender, EventArgs e)
        {
            UpdateZoomStatus();
            UpdatePerformanceText();
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
                _lastHoverX = -1;
                _lastHoverY = -1;
                if (!IsInspectorPinned)
                {
                    SetPixelDetails(string.Empty, string.Empty, string.Empty, string.Empty);
                    ClearPixelStatus();
                }

                return;
            }

            _lastHoverX = e.X;
            _lastHoverY = e.Y;
            if (!IsInspectorPinned)
            {
                UpdateInspectorPixel(_activeDocument, e.X, e.Y);
            }
        }

        private void OpenGlImageView_PixelSelected(object? sender, RawOpenGlPixelEventArgs e)
        {
            if (_activeDocument == null
                || e.X < 0
                || e.Y < 0
                || e.X >= _activeDocument.Descriptor.Width
                || e.Y >= _activeDocument.Descriptor.Height)
            {
                return;
            }

            _lastHoverX = e.X;
            _lastHoverY = e.Y;
            if (!IsInspectorPinned)
            {
                UpdateInspectorPixel(_activeDocument, e.X, e.Y);
            }
        }

        private void OpenGlImageView_PixelPinned(object? sender, RawOpenGlPixelEventArgs e)
        {
            if (_activeDocument == null || e.X < 0 || e.Y < 0 || e.X >= _activeDocument.Descriptor.Width || e.Y >= _activeDocument.Descriptor.Height)
            {
                _pinnedInspectorX = -1;
                _pinnedInspectorY = -1;
                SetInspectorPinnedState(false);
                SetMarkerText(string.Empty);
                if (_activeDocument != null
                    && _lastHoverX >= 0
                    && _lastHoverY >= 0
                    && _lastHoverX < _activeDocument.Descriptor.Width
                    && _lastHoverY < _activeDocument.Descriptor.Height)
                {
                    UpdateInspectorPixel(_activeDocument, _lastHoverX, _lastHoverY);
                }
                else
                {
                    SetPixelDetails(string.Empty, string.Empty, string.Empty, string.Empty);
                    ClearPixelStatus();
                }

                return;
            }

            _pinnedInspectorX = e.X;
            _pinnedInspectorY = e.Y;
            SetInspectorPinnedState(true);
            SetMarkerText(string.Format(
                CultureInfo.InvariantCulture,
                "X {0}  Y {1}\n{2}",
                e.X,
                e.Y,
                _activeDocument.Source.DescribePixel(e.X, e.Y)));
            UpdateInspectorPixel(_activeDocument, e.X, e.Y);
        }

        private void PinMarker_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDocument == null)
            {
                return;
            }

            int x;
            int y;
            if (!OpenGlImageView.TryGetSelectedPixel(out x, out y))
            {
                x = _lastHoverX;
                y = _lastHoverY;
            }

            if (x < 0 || y < 0 || x >= _activeDocument.Descriptor.Width || y >= _activeDocument.Descriptor.Height)
            {
                return;
            }

            OpenGlImageView.PinMarkerAtImagePixel(x, y);
        }

        private void ClearMarker_Click(object sender, RoutedEventArgs e)
        {
            OpenGlImageView.ClearPinnedMarker();
        }

        private void ApplyLevels_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyLevels(BlackLevelTextBox.Text, WhiteLevelTextBox.Text);
            }
            catch (Exception ex)
            {
                DiagnosticsList.Items.Add("Error: levels failed. " + ex.Message);
            }
        }

        private void ApplyCompactLevels_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyLevels(CompactBlackLevelTextBox.Text, CompactWhiteLevelTextBox.Text);
            }
            catch (Exception ex)
            {
                DiagnosticsList.Items.Add("Error: levels failed. " + ex.Message);
            }
        }

        private void ApplyLevels(string blackText, string whiteText)
        {
            var black = ParseDouble(blackText, "Black");
            var white = ParseDouble(whiteText, "White");
            OpenGlImageView.SetRenderLevels(black, white);
            UpdateLevelsControls();
            UpdatePerformanceText();
        }

        private void AutoLevels_Click(object sender, RoutedEventArgs e)
        {
            OpenGlImageView.ResetRenderLevels();
            UpdateLevelsControls();
            UpdatePerformanceText();
        }

        private void UpdateZoomStatus()
        {
            if (ZoomText == null || ZoomSlider == null || OpenGlImageView == null)
            {
                return;
            }

            var zoom = OpenGlImageView.ZoomScale;
            if (Math.Abs(_lastZoomStatus - zoom) < 0.0001)
            {
                return;
            }

            _lastZoomStatus = zoom;
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

        private void UpdatePerformanceText()
        {
            if (PerformanceText == null || OpenGlImageView == null)
            {
                return;
            }

            if (_activeDocument == null)
            {
                SetPerformanceText("Perf: no image");
                return;
            }

            var stats = OpenGlImageView.GetRenderStatsSnapshot();
            SetPerformanceText(string.Format(
                CultureInfo.InvariantCulture,
                "Perf: frames {0:N0}, max {1:0.0} ms, wheel {2:N0}/{3:0.0} ms, drag {4:N0}/{5:0.0} ms, upload {6:N0}/{7:0.0} ms",
                stats.FrameCount,
                stats.MaxFrameMilliseconds,
                stats.WheelInputCount,
                stats.MaxWheelInputMilliseconds,
                stats.DragInputCount,
                stats.MaxDragInputMilliseconds,
                stats.TextureUploadCount,
                stats.MaxTextureUploadMilliseconds));
        }

        private void UpdateInterpretationControls(RawImageDescriptor descriptor)
        {
            InterpretPixelFormatBox.SelectedItem = descriptor.PixelFormat;
            InterpretByteOrderBox.SelectedItem = descriptor.ByteOrder;
            InterpretStrideTextBox.Text = descriptor.Stride.ToString(CultureInfo.InvariantCulture);
            InterpretValidBitsTextBox.Text = descriptor.ValidBits.ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateCompareText(string? message = null)
        {
            if (CompareText == null)
            {
                return;
            }

            CompareText.Text = string.Format(
                CultureInfo.InvariantCulture,
                "A: {0}\nB: {1}{2}",
                _compareA == null ? "-" : _compareA.Title,
                _compareB == null ? "-" : _compareB.Title,
                string.IsNullOrWhiteSpace(message) ? string.Empty : "\n" + message);
        }

        private static int ParsePositiveInt(string text, string name)
        {
            int value;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value <= 0)
            {
                throw new InvalidOperationException(name + " must be a positive integer.");
            }

            return value;
        }

        private static double ParseDouble(string text, string name)
        {
            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                throw new InvalidOperationException(name + " must be a number.");
            }

            return value;
        }

        private void UpdateLevelsControls()
        {
            var options = OpenGlImageView.GetRenderOptionsSnapshot();
            if (options == null)
            {
                BlackLevelTextBox.Text = string.Empty;
                WhiteLevelTextBox.Text = string.Empty;
                CompactBlackLevelTextBox.Text = string.Empty;
                CompactWhiteLevelTextBox.Text = string.Empty;
                return;
            }

            var blackText = options.BlackLevel.ToString("0.###", CultureInfo.InvariantCulture);
            var whiteText = options.WhiteLevel.ToString("0.###", CultureInfo.InvariantCulture);
            BlackLevelTextBox.Text = blackText;
            WhiteLevelTextBox.Text = whiteText;
            CompactBlackLevelTextBox.Text = blackText;
            CompactWhiteLevelTextBox.Text = whiteText;
        }

        private void SetPixelDetails(string pixel, string neighborhood, string roi, string line)
        {
            PixelText.Text = pixel;
            PixelNeighborhoodText.Text = neighborhood;
            RoiStatsText.Text = roi;
            LineProfileText.Text = line;
            CompactPixelText.Text = pixel;
            CompactPixelNeighborhoodText.Text = neighborhood;
            CompactRoiStatsText.Text = roi;
        }

        private bool IsInspectorPinned
        {
            get { return _pinnedInspectorX >= 0 && _pinnedInspectorY >= 0; }
        }

        private void UpdateInspectorPixel(ImageDocument document, int x, int y)
        {
            var pixelDescription = document.Source.DescribePixel(x, y);
            SetPixelDetails(
                pixelDescription,
                BuildPixelNeighborhood(document, x, y, 2),
                BuildRoiStats(document, x, y, 2),
                BuildLineProfile(document, x, y));
            UpdatePixelStatus(document, x, y, pixelDescription);
        }

        private void SetInspectorPinnedState(bool pinned)
        {
            PixelHeadingText.Text = pinned ? "Pinned Pixel" : "Pixel";
            PixelNeighborhoodHeadingText.Text = pinned ? "Pinned 5x5 Values" : "Hover 5x5 Values";
            PixelStatsHeadingText.Text = pinned ? "Pinned 5x5 Stats" : "Hover 5x5 Stats";
            CompactPixelHeadingText.Text = pinned ? "Pinned" : "Current";
            CompactPixelNeighborhoodHeadingText.Text = pinned ? "Pinned 5x5 Values" : "Hover 5x5 Values";
            CompactPixelStatsHeadingText.Text = pinned ? "Pinned 5x5 Stats" : "Hover 5x5 Stats";
        }

        private void SetMarkerText(string text)
        {
            MarkerText.Text = text;
            CompactMarkerText.Text = text;
        }

        private void SetPerformanceText(string text)
        {
            PerformanceText.Text = text;
            CompactPerformanceText.Text = text;
        }

        private static string BuildPixelNeighborhood(ImageDocument document, int centerX, int centerY, int radius)
        {
            var builder = new StringBuilder();
            for (var y = centerY - radius; y <= centerY + radius; y++)
            {
                if (y < 0 || y >= document.Descriptor.Height)
                {
                    continue;
                }

                for (var x = centerX - radius; x <= centerX + radius; x++)
                {
                    if (x < 0 || x >= document.Descriptor.Width)
                    {
                        builder.Append("     ");
                        continue;
                    }

                    builder.Append(CompactPixelValue(document.Source.DescribePixel(x, y)).PadLeft(5));
                }

                builder.AppendLine();
            }

            return builder.ToString().TrimEnd();
        }

        private static string BuildRoiStats(ImageDocument document, int centerX, int centerY, int radius)
        {
            var count = 0;
            var sum = 0.0;
            var sumSquares = 0.0;
            var min = double.MaxValue;
            var max = double.MinValue;

            for (var y = Math.Max(0, centerY - radius); y <= Math.Min(document.Descriptor.Height - 1, centerY + radius); y++)
            {
                for (var x = Math.Max(0, centerX - radius); x <= Math.Min(document.Descriptor.Width - 1, centerX + radius); x++)
                {
                    double value;
                    if (!TryExtractNumericPixelValue(document.Source.DescribePixel(x, y), out value))
                    {
                        continue;
                    }

                    count++;
                    sum += value;
                    sumSquares += value * value;
                    if (value < min)
                    {
                        min = value;
                    }

                    if (value > max)
                    {
                        max = value;
                    }
                }
            }

            if (count == 0)
            {
                return "No numeric pixels";
            }

            var mean = sum / count;
            var variance = Math.Max(0, (sumSquares / count) - (mean * mean));
            var stdDev = Math.Sqrt(variance);
            return string.Format(
                CultureInfo.InvariantCulture,
                "n={0}  min={1:0.###}  max={2:0.###}\nmean={3:0.###}  std={4:0.###}",
                count,
                min,
                max,
                mean,
                stdDev);
        }

        private static bool TryExtractNumericPixelValue(string description, out double value)
        {
            if (TryReadDoubleToken(description, "GV=", out value)
                || TryReadDoubleToken(description, "Value=", out value)
                || TryReadDoubleToken(description, "Bayer R=", out value)
                || TryReadDoubleToken(description, "Bayer G=", out value)
                || TryReadDoubleToken(description, "Bayer B=", out value))
            {
                return true;
            }

            int r;
            int g;
            int b;
            if (TryReadIntToken(description, "R=", out r)
                && TryReadIntToken(description, "G=", out g)
                && TryReadIntToken(description, "B=", out b))
            {
                value = (r + g + b) / 3.0;
                return true;
            }

            return false;
        }

        private static string BuildLineProfile(ImageDocument document, int centerX, int y)
        {
            var start = Math.Max(0, centerX - 8);
            var end = Math.Min(document.Descriptor.Width - 1, centerX + 8);
            var builder = new StringBuilder();
            for (var x = start; x <= end; x++)
            {
                if (x > start)
                {
                    builder.Append(' ');
                }

                builder.Append(CompactPixelValue(document.Source.DescribePixel(x, y)));
            }

            return builder.ToString();
        }

        private void ClearPixelStatus()
        {
            if (PixelPositionText == null)
            {
                return;
            }

            PixelPositionText.Text = "X -  Y -";
            PixelColorText.Text = "RGB -";
            PixelRawText.Text = "Bytes -";
            PixelValueChip.Visibility = Visibility.Visible;
            PixelChannelsPanel.Visibility = Visibility.Collapsed;
            PixelAChip.Visibility = Visibility.Collapsed;
            PixelValueText.Text = "GV -";
            PixelRText.Text = "R -";
            PixelGText.Text = "G -";
            PixelBText.Text = "B -";
            PixelAText.Text = "A -";
            PixelSwatch.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            PixelRSwatch.Background = new SolidColorBrush(Color.FromRgb(85, 0, 0));
            PixelGSwatch.Background = new SolidColorBrush(Color.FromRgb(0, 85, 0));
            PixelBSwatch.Background = new SolidColorBrush(Color.FromRgb(0, 0, 85));
            PixelASwatch.Background = new SolidColorBrush(Color.FromRgb(85, 85, 85));
        }

        private void UpdatePixelStatus(ImageDocument document, int x, int y, string description)
        {
            PixelPositionText.Text = string.Format(CultureInfo.InvariantCulture, "X {0}  Y {1}", x, y);
            PixelColorText.Text = BuildPixelColorStatus(document.Descriptor, description);
            PixelRawText.Text = BuildPixelByteStatus(description);
            UpdatePixelChannelStatus(document.Descriptor, description);
        }

        private void UpdatePixelChannelStatus(RawImageDescriptor descriptor, string description)
        {
            int r;
            int g;
            int b;
            if (TryReadIntToken(description, "R=", out r)
                && TryReadIntToken(description, "G=", out g)
                && TryReadIntToken(description, "B=", out b))
            {
                PixelValueChip.Visibility = Visibility.Collapsed;
                PixelChannelsPanel.Visibility = Visibility.Visible;
                PixelAChip.Visibility = TryReadIntToken(description, "A=", out var a) ? Visibility.Visible : Visibility.Collapsed;

                PixelRText.Text = "R " + r.ToString(CultureInfo.InvariantCulture);
                PixelGText.Text = "G " + g.ToString(CultureInfo.InvariantCulture);
                PixelBText.Text = "B " + b.ToString(CultureInfo.InvariantCulture);
                PixelRSwatch.Background = new SolidColorBrush(Color.FromRgb(ToByte(r), 0, 0));
                PixelGSwatch.Background = new SolidColorBrush(Color.FromRgb(0, ToByte(g), 0));
                PixelBSwatch.Background = new SolidColorBrush(Color.FromRgb(0, 0, ToByte(b)));

                if (PixelAChip.Visibility == Visibility.Visible)
                {
                    PixelAText.Text = "A " + a.ToString(CultureInfo.InvariantCulture);
                    PixelASwatch.Background = new SolidColorBrush(Color.FromRgb(ToByte(a), ToByte(a), ToByte(a)));
                }

                return;
            }

            PixelValueChip.Visibility = Visibility.Visible;
            PixelChannelsPanel.Visibility = Visibility.Collapsed;
            PixelAChip.Visibility = Visibility.Collapsed;

            PixelValueText.Text = BuildPixelColorStatus(descriptor, description);
            PixelSwatch.Background = new SolidColorBrush(GetPixelStatusColor(descriptor, description));
        }

        private static string BuildPixelColorStatus(RawImageDescriptor descriptor, string description)
        {
            int r;
            int g;
            int b;
            if (TryReadIntToken(description, "R=", out r)
                && TryReadIntToken(description, "G=", out g)
                && TryReadIntToken(description, "B=", out b))
            {
                int a;
                return TryReadIntToken(description, "A=", out a)
                    ? string.Format(CultureInfo.InvariantCulture, "RGB R={0} G={1} B={2} A={3}", r, g, b, a)
                    : string.Format(CultureInfo.InvariantCulture, "RGB R={0} G={1} B={2}", r, g, b);
            }

            double gv;
            if (TryReadDoubleToken(description, "GV=", out gv))
            {
                return string.Format(CultureInfo.InvariantCulture, "GV {0:0.###}", gv);
            }

            double value;
            if (TryReadDoubleToken(description, "Value=", out value))
            {
                return string.Format(CultureInfo.InvariantCulture, "Value {0:0.###}", value);
            }

            foreach (var key in new[] { "Bayer R=", "Bayer G=", "Bayer B=" })
            {
                int bayerValue;
                if (TryReadIntToken(description, key, out bayerValue))
                {
                    return key.TrimEnd('=') + " " + bayerValue.ToString(CultureInfo.InvariantCulture);
                }
            }

            return descriptor.PixelFormat.ToString();
        }

        private static string BuildPixelByteStatus(string description)
        {
            var raw = ReadTailToken(description, "Raw=") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw) || raw == "-")
            {
                return "Bytes -";
            }

            var parts = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "Bytes -";
            }

            var decimalParts = new string[parts.Length];
            var hexParts = new string[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                byte value;
                if (byte.TryParse(parts[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                {
                    decimalParts[i] = value.ToString(CultureInfo.InvariantCulture);
                    hexParts[i] = "0x" + value.ToString("X2", CultureInfo.InvariantCulture);
                }
                else
                {
                    decimalParts[i] = parts[i];
                    hexParts[i] = parts[i];
                }
            }

            return parts.Length == 1
                ? string.Format(CultureInfo.InvariantCulture, "Byte {0} ({1})", decimalParts[0], hexParts[0])
                : string.Format(CultureInfo.InvariantCulture, "Bytes {0}", string.Join(" ", decimalParts));
        }

        private static Color GetPixelStatusColor(RawImageDescriptor descriptor, string description)
        {
            int r;
            int g;
            int b;
            if (TryReadIntToken(description, "R=", out r)
                && TryReadIntToken(description, "G=", out g)
                && TryReadIntToken(description, "B=", out b))
            {
                return Color.FromRgb(ToByte(r), ToByte(g), ToByte(b));
            }

            double gv;
            if (TryReadDoubleToken(description, "GV=", out gv) || TryReadDoubleToken(description, "Value=", out gv))
            {
                var gray = ScaleGray(descriptor, gv);
                return Color.FromRgb(gray, gray, gray);
            }

            int bayerValue;
            if (TryReadIntToken(description, "Bayer R=", out bayerValue))
            {
                return Color.FromRgb(ScaleGray(descriptor, bayerValue), 0, 0);
            }

            if (TryReadIntToken(description, "Bayer G=", out bayerValue))
            {
                return Color.FromRgb(0, ScaleGray(descriptor, bayerValue), 0);
            }

            if (TryReadIntToken(description, "Bayer B=", out bayerValue))
            {
                return Color.FromRgb(0, 0, ScaleGray(descriptor, bayerValue));
            }

            return Color.FromRgb(30, 30, 30);
        }

        private static bool TryReadIntToken(string text, string key, out int value)
        {
            var token = ReadToken(text, key);
            return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryReadDoubleToken(string text, string key, out double value)
        {
            var token = ReadToken(text, key);
            return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string? ReadToken(string text, string key)
        {
            var index = text.IndexOf(key, StringComparison.Ordinal);
            if (index < 0)
            {
                return null;
            }

            var start = index + key.Length;
            var end = start;
            while (end < text.Length && text[end] != ',' && !char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            return text.Substring(start, end - start).Trim();
        }

        private static string? ReadTailToken(string text, string key)
        {
            var index = text.IndexOf(key, StringComparison.Ordinal);
            if (index < 0)
            {
                return null;
            }

            var start = index + key.Length;
            var end = text.IndexOf(',', start);
            return (end < 0 ? text.Substring(start) : text.Substring(start, end - start)).Trim();
        }

        private static byte ScaleGray(RawImageDescriptor descriptor, double value)
        {
            if (descriptor.PixelFormat == RawPixelFormat.Float32)
            {
                return ToByte((int)Math.Round(value * 255.0));
            }

            var bits = descriptor.ValidBits <= 0 ? 8 : Math.Min(16, descriptor.ValidBits);
            var max = Math.Pow(2, bits) - 1;
            return ToByte((int)Math.Round(value / Math.Max(1.0, max) * 255.0));
        }

        private static byte ToByte(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value > 255 ? (byte)255 : (byte)value;
        }

        private static string CompactPixelValue(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return "-";
            }

            foreach (var key in new[] { "GV=", "Value=", "R=", "Bayer R=", "Bayer G=", "Bayer B=" })
            {
                var index = description.IndexOf(key, StringComparison.Ordinal);
                if (index < 0)
                {
                    continue;
                }

                var start = index + key.Length;
                var end = description.IndexOf(',', start);
                var value = end < 0 ? description.Substring(start) : description.Substring(start, end - start);
                return value.Trim();
            }

            return description.Length <= 8 ? description : description.Substring(0, 8);
        }

        private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveLayout(e.NewSize.Width);
        }

        private void ApplyResponsiveLayout(double width)
        {
            if (ImagesColumn == null
                || InspectorColumn == null
                || InspectorSplitterColumn == null
                || ImagesGridSplitter == null
                || InspectorGridSplitter == null
                || CompactInspectorGridSplitter == null
                || CompactInspectorRow == null
                || SelectionOverlayBox == null
                || InspectorPanel == null
                || CompactInspectorPanel == null
                || StatusText == null
                || TempUsageText == null)
            {
                return;
            }

            if (width <= 0 || double.IsNaN(width))
            {
                width = ActualWidth;
            }

            var nextMode = width < 760
                ? LayoutMode.Narrow
                : width < 1040
                    ? LayoutMode.Medium
                    : LayoutMode.Wide;
            var modeChanged = nextMode != _layoutMode;

            ImagesGridSplitter.Visibility = Visibility.Visible;

            if (nextMode == LayoutMode.Narrow)
            {
                if (modeChanged)
                {
                    ImagesColumn.Width = new GridLength(width < 620 ? 180 : 220);
                    InspectorColumn.Width = new GridLength(0);
                }

                InspectorSplitterColumn.Width = new GridLength(0);
                DescriptorPanel.Visibility = Visibility.Collapsed;
                InspectorPanel.Visibility = Visibility.Collapsed;
                InspectorGridSplitter.Visibility = Visibility.Collapsed;
                SetCompactInspectorVisible(InspectorToggleButton.IsChecked == true);
                InspectorToggleButton.Visibility = Visibility.Visible;
                LinkViewsBox.Visibility = width < 620 ? Visibility.Collapsed : Visibility.Visible;
                SelectionOverlayBox.Visibility = width < 620 ? Visibility.Collapsed : Visibility.Visible;
                TempUsageText.Visibility = width < 620 ? Visibility.Collapsed : Visibility.Visible;
                TempUsageText.Width = 82;
                StatusText.Width = 95;
            }
            else if (nextMode == LayoutMode.Medium)
            {
                if (modeChanged)
                {
                    ImagesColumn.Width = new GridLength(width < 880 ? 260 : 300);
                    InspectorColumn.Width = new GridLength(0);
                }

                InspectorSplitterColumn.Width = new GridLength(0);
                DescriptorPanel.Visibility = Visibility.Collapsed;
                InspectorPanel.Visibility = Visibility.Collapsed;
                InspectorGridSplitter.Visibility = Visibility.Collapsed;
                SetCompactInspectorVisible(true);
                InspectorToggleButton.Visibility = Visibility.Collapsed;
                LinkViewsBox.Visibility = Visibility.Visible;
                SelectionOverlayBox.Visibility = Visibility.Visible;
                TempUsageText.Visibility = Visibility.Visible;
                TempUsageText.Width = 92;
                StatusText.Width = 115;
            }
            else
            {
                if (modeChanged)
                {
                    ImagesColumn.Width = new GridLength(320);
                    InspectorColumn.Width = new GridLength(300);
                }

                InspectorSplitterColumn.Width = new GridLength(5);
                DescriptorPanel.Visibility = Visibility.Visible;
                InspectorPanel.Visibility = Visibility.Visible;
                InspectorGridSplitter.Visibility = Visibility.Visible;
                SetCompactInspectorVisible(false);
                InspectorToggleButton.Visibility = Visibility.Collapsed;
                LinkViewsBox.Visibility = Visibility.Visible;
                SelectionOverlayBox.Visibility = Visibility.Visible;
                TempUsageText.Visibility = Visibility.Visible;
                TempUsageText.Width = 100;
                StatusText.Width = 150;
            }

            _layoutMode = nextMode;
        }

        private void SetCompactInspectorVisible(bool visible)
        {
            if (CompactInspectorPanel == null || CompactInspectorGridSplitter == null || CompactInspectorRow == null)
            {
                return;
            }

            if (visible)
            {
                CompactInspectorPanel.Visibility = Visibility.Visible;
                CompactInspectorGridSplitter.Visibility = Visibility.Visible;
                if (CompactInspectorRow.Height.Value <= 0)
                {
                    CompactInspectorRow.Height = new GridLength(Math.Max(96, _lastCompactInspectorHeight));
                }

                return;
            }

            if (CompactInspectorRow.Height.Value > 0)
            {
                _lastCompactInspectorHeight = CompactInspectorRow.Height.Value;
            }

            CompactInspectorPanel.Visibility = Visibility.Collapsed;
            CompactInspectorGridSplitter.Visibility = Visibility.Collapsed;
            CompactInspectorRow.Height = new GridLength(0);
        }

        private void UpdateStatus()
        {
            if (_activeDocument == null)
            {
                StatusText.Text = string.Format(CultureInfo.InvariantCulture, "{0:N0} images", _documents.Count);
                WriteSessionStateIfRequested();
                return;
            }

            StatusText.Text = string.Format(
                CultureInfo.InvariantCulture,
                "{0}x{1} {2} {3} {4} tiles",
                _activeDocument.Descriptor.Width,
                _activeDocument.Descriptor.Height,
                _activeDocument.Descriptor.PixelFormat,
                GetSourceMode(_activeDocument.Source),
                OpenGlImageView.TileCount);
            WriteSessionStateIfRequested();
        }

        private void SetTransientStatus(string text)
        {
            if (StatusText != null)
            {
                StatusText.Text = text;
            }
        }

        private void UpdateTempUsageStatus()
        {
            if (TempUsageText == null)
            {
                return;
            }

            long byteCount;
            TempUsageText.Text = VisualStudioTempStore.TryGetRootByteCount(out byteCount)
                ? "Temp " + FormatByteCount(byteCount)
                : "Temp -";
        }

        private static string GetSourceMode(RawImageSource source)
        {
            return source.IsFileBacked ? "file" : "mem";
        }

        private static string FormatByteCount(long byteCount)
        {
            if (byteCount < 0)
            {
                return "-";
            }

            string[] units = { "B", "KB", "MB", "GB", "TB" };
            var value = (double)byteCount;
            var unitIndex = 0;
            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }

            return unitIndex == 0
                ? string.Format(CultureInfo.InvariantCulture, "{0:0} {1}", value, units[unitIndex])
                : string.Format(CultureInfo.InvariantCulture, "{0:0.#} {1}", value, units[unitIndex]);
        }

        private void WriteSessionStateIfRequested()
        {
            var outputPath = Environment.GetEnvironmentVariable("RAWBUFFERVISUALIZER_DOCKED_SESSION_JSON");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var errorCount = _documents.Count(document => document.IsError);
                long tempByteCount;
                var hasTempByteCount = VisualStudioTempStore.TryGetRootByteCount(out tempByteCount);
                var builder = new StringBuilder();
                builder.AppendLine("{");
                AppendJsonProperty(builder, "documentCount", _documents.Count, true);
                AppendJsonProperty(builder, "errorCount", errorCount, true);
                AppendJsonProperty(builder, "tempBytes", hasTempByteCount ? tempByteCount : -1, true);
                AppendJsonProperty(builder, "activeTitle", _activeDocument == null ? string.Empty : _activeDocument.Title, true);
                AppendJsonProperty(builder, "status", StatusText.Text, true);
                builder.AppendLine("  \"documents\": [");
                for (var i = 0; i < _documents.Count; i++)
                {
                    var document = _documents[i];
                    builder.AppendLine("    {");
                    AppendJsonProperty(builder, "title", document.Title, true, 6);
                    AppendJsonProperty(builder, "summary", document.Summary, true, 6);
                    AppendJsonProperty(builder, "sourceType", document.SourceType, true, 6);
                    AppendJsonProperty(builder, "width", document.Descriptor.Width, true, 6);
                    AppendJsonProperty(builder, "height", document.Descriptor.Height, true, 6);
                    AppendJsonProperty(builder, "stride", document.Descriptor.Stride, true, 6);
                    AppendJsonProperty(builder, "pixelFormat", document.Descriptor.PixelFormat.ToString(), true, 6);
                    AppendJsonProperty(builder, "sourceMode", GetSourceMode(document.Source), true, 6);
                    AppendJsonProperty(builder, "isError", document.IsError, true, 6);
                    AppendJsonProperty(builder, "hasThumbnail", document.Thumbnail != null, true, 6);
                    AppendJsonProperty(builder, "errorMessage", document.ErrorMessage, false, 6);
                    builder.Append("    }");
                    builder.AppendLine(i + 1 == _documents.Count ? string.Empty : ",");
                }

                builder.AppendLine("  ]");
                builder.AppendLine("}");
                File.WriteAllText(outputPath, builder.ToString());
            }
            catch
            {
                // Automation diagnostics must not affect normal Visual Studio usage.
            }
        }

        private void ScheduleAutomationProbeIfRequested(string metadataPath)
        {
            var outputPath = Environment.GetEnvironmentVariable("RAWBUFFERVISUALIZER_DOCKED_PERF_JSON");
            if (string.IsNullOrWhiteSpace(outputPath) || _automationProbeRunning)
            {
                return;
            }

            _automationProbeRunning = true;
            Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() => RunAutomationProbe(outputPath, metadataPath)));
        }

        private void WriteAutomationProbeFailureIfRequested(string metadataPath, Exception exception)
        {
            var outputPath = Environment.GetEnvironmentVariable("RAWBUFFERVISUALIZER_DOCKED_PERF_JSON");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return;
            }

            if (File.Exists(outputPath))
            {
                return;
            }

            WriteAutomationProbeResult(outputPath, metadataPath, 0, 0, 0, 0, null, null, null, null, null, exception);
        }

        private void RunAutomationProbe(string outputPath, string metadataPath)
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(45)
            };

            var settleTicks = 0;
            var zoomIndex = 0;
            var panIndex = 0;
            var phase = "settle";
            var maxZoomCommandMs = 0.0;
            var maxPanCommandMs = 0.0;
            var maxWheelCommandMs = 0.0;
            var maxDragCommandMs = 0.0;
            RawOpenGlRenderStats? zoomStats = null;
            RawOpenGlRenderStats? panStats = null;
            RawOpenGlRenderStats? wheelStats = null;
            RawOpenGlRenderStats? dragStats = null;
            RawOpenGlRenderStats? externalInputStats = null;
            var zoomScales = new[] { 0.12, 0.18, 0.25, 0.5, 1.0, 1.5, 2.0, 1.0, 0.35, 0.16 };
            var zoomIterations = GetEnvironmentInt("RAWBUFFERVISUALIZER_DOCKED_PERF_ZOOM_ITERATIONS", 30);
            var panIterations = GetEnvironmentInt("RAWBUFFERVISUALIZER_DOCKED_PERF_PAN_ITERATIONS", 30);
            var wheelIterations = GetEnvironmentInt("RAWBUFFERVISUALIZER_DOCKED_PERF_WHEEL_ITERATIONS", 60);
            var dragIterations = GetEnvironmentInt("RAWBUFFERVISUALIZER_DOCKED_PERF_DRAG_ITERATIONS", 120);
            var externalInputSeconds = GetEnvironmentInt("RAWBUFFERVISUALIZER_DOCKED_PERF_EXTERNAL_INPUT_SECONDS", 0);
            var externalReadyPath = Environment.GetEnvironmentVariable("RAWBUFFERVISUALIZER_DOCKED_PERF_EXTERNAL_READY_FILE");
            var layoutWaitTicks = 0;
            var wheelIndex = 0;
            var dragIndex = 0;
            var externalTicks = 0;

            timer.Tick += delegate
            {
                try
                {
                    if (phase == "settle")
                    {
                        layoutWaitTicks++;
                        if (ActualWidth <= 10 || ActualHeight <= 10 || OpenGlImageView.ActualWidth <= 10 || OpenGlImageView.ActualHeight <= 10)
                        {
                            if (layoutWaitTicks < 200)
                            {
                                return;
                            }

                            throw new InvalidOperationException("ToolWindow did not receive a visible layout size before performance probing.");
                        }

                        settleTicks++;
                        if (settleTicks < 12)
                        {
                            return;
                        }

                        OpenGlImageView.ResetRenderStats();
                        phase = "zoom";
                        return;
                    }

                    if (phase == "zoom")
                    {
                        if (zoomIndex < zoomIterations)
                        {
                            var commandWatch = Stopwatch.StartNew();
                            OpenGlImageView.SetZoomScale(zoomScales[zoomIndex % zoomScales.Length]);
                            commandWatch.Stop();
                            maxZoomCommandMs = Math.Max(maxZoomCommandMs, commandWatch.Elapsed.TotalMilliseconds);
                            zoomIndex++;
                            return;
                        }

                        zoomStats = OpenGlImageView.GetRenderStatsSnapshot();
                        OpenGlImageView.SetZoomScale(1.0);
                        OpenGlImageView.ResetRenderStats();
                        phase = "pan";
                        return;
                    }

                    if (phase == "pan")
                    {
                        if (panIndex < panIterations)
                        {
                            var dx = (panIndex % 4) < 2 ? 160 : -160;
                            var dy = (panIndex % 6) < 3 ? 96 : -96;
                            var commandWatch = Stopwatch.StartNew();
                            OpenGlImageView.PanByImagePixels(dx, dy);
                            commandWatch.Stop();
                            maxPanCommandMs = Math.Max(maxPanCommandMs, commandWatch.Elapsed.TotalMilliseconds);
                            panIndex++;
                            return;
                        }

                        panStats = OpenGlImageView.GetRenderStatsSnapshot();
                        OpenGlImageView.SetZoomScale(1.0);
                        OpenGlImageView.ResetRenderStats();
                        phase = "wheel";
                        return;
                    }

                    if (phase == "wheel")
                    {
                        if (wheelIndex < wheelIterations)
                        {
                            var x = OpenGlImageView.ActualWidth * ((wheelIndex % 3) + 1) / 4.0;
                            var y = OpenGlImageView.ActualHeight * ((wheelIndex % 2) + 1) / 3.0;
                            var delta = (wheelIndex % 6) < 3 ? 120 : -120;
                            var commandWatch = Stopwatch.StartNew();
                            OpenGlImageView.ZoomAtScreenPoint(new Point(x, y), delta);
                            commandWatch.Stop();
                            maxWheelCommandMs = Math.Max(maxWheelCommandMs, commandWatch.Elapsed.TotalMilliseconds);
                            wheelIndex++;
                            return;
                        }

                        wheelStats = OpenGlImageView.GetRenderStatsSnapshot();
                        OpenGlImageView.SetZoomScale(1.0);
                        OpenGlImageView.ResetRenderStats();
                        phase = "drag";
                        return;
                    }

                    if (phase == "drag")
                    {
                        if (dragIndex < dragIterations)
                        {
                            var dx = (dragIndex % 8) < 4 ? 16 : -16;
                            var dy = (dragIndex % 12) < 6 ? 10 : -10;
                            var commandWatch = Stopwatch.StartNew();
                            OpenGlImageView.PanByScreenPixels(dx, dy);
                            commandWatch.Stop();
                            maxDragCommandMs = Math.Max(maxDragCommandMs, commandWatch.Elapsed.TotalMilliseconds);
                            dragIndex++;
                            return;
                        }

                        dragStats = OpenGlImageView.GetRenderStatsSnapshot();
                        if (externalInputSeconds > 0)
                        {
                            OpenGlImageView.SetZoomScale(1.0);
                            OpenGlImageView.ResetRenderStats();
                            WriteExternalInputReadyFile(externalReadyPath);
                            phase = "external";
                            return;
                        }

                        timer.Stop();
                        SaveFramebufferIfRequested();
                        ProbePixelOverlayForAutomation();
                        WriteAutomationProbeResult(outputPath, metadataPath, maxZoomCommandMs, maxPanCommandMs, maxWheelCommandMs, maxDragCommandMs, zoomStats, panStats, wheelStats, dragStats, externalInputStats, null);
                        _automationProbeRunning = false;
                        return;
                    }

                    if (phase == "external")
                    {
                        externalTicks++;
                        if (externalTicks * timer.Interval.TotalSeconds < externalInputSeconds)
                        {
                            return;
                        }

                        externalInputStats = OpenGlImageView.GetRenderStatsSnapshot();
                        timer.Stop();
                        SaveFramebufferIfRequested();
                        ProbePixelOverlayForAutomation();
                        WriteAutomationProbeResult(outputPath, metadataPath, maxZoomCommandMs, maxPanCommandMs, maxWheelCommandMs, maxDragCommandMs, zoomStats, panStats, wheelStats, dragStats, externalInputStats, null);
                        _automationProbeRunning = false;
                    }
                }
                catch (Exception ex)
                {
                    timer.Stop();
                    WriteAutomationProbeResult(outputPath, metadataPath, maxZoomCommandMs, maxPanCommandMs, maxWheelCommandMs, maxDragCommandMs, zoomStats, panStats, wheelStats, dragStats, externalInputStats, ex);
                    _automationProbeRunning = false;
                }
            };

            timer.Start();
        }

        private void ProbePixelOverlayForAutomation()
        {
            if (_activeDocument == null || _activeDocument.IsError || OpenGlImageView.ActualWidth <= 10 || OpenGlImageView.ActualHeight <= 10)
            {
                return;
            }

            OpenGlImageView.SetZoomScale(12.0);
            OpenGlImageView.ProbePixelOverlayAtScreenPoint(new Point(
                Math.Max(1, OpenGlImageView.ActualWidth / 2),
                Math.Max(1, OpenGlImageView.ActualHeight / 2)));
            var x = Math.Max(0, _activeDocument.Descriptor.Width / 2);
            var y = Math.Max(0, _activeDocument.Descriptor.Height / 2);
            var description = _activeDocument.Source.DescribePixel(x, y);
            UpdatePixelStatus(_activeDocument, x, y, description);
            SetPixelDetails(
                description,
                BuildPixelNeighborhood(_activeDocument, x, y, 2),
                BuildRoiStats(_activeDocument, x, y, 2),
                BuildLineProfile(_activeDocument, x, y));
            OpenGlImageView.SelectPixelAtImagePixel(x, y);
            OpenGlImageView.PinMarkerAtImagePixel(x, y);
            var options = OpenGlImageView.GetRenderOptionsSnapshot();
            if (options != null)
            {
                OpenGlImageView.SetRenderLevels(options.BlackLevel, options.WhiteLevel);
                UpdateLevelsControls();
            }

            OpenGlImageView.SetZoomScale(48.0);
        }

        private void SaveFramebufferIfRequested()
        {
            var path = Environment.GetEnvironmentVariable("RAWBUFFERVISUALIZER_DOCKED_FRAMEBUFFER_PNG");
            _lastFramebufferCapturePath = path ?? string.Empty;
            _lastFramebufferCaptureError = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var capturePath = path!;
            try
            {
                OpenGlImageView.SaveFramebufferPng(capturePath);
            }
            catch (Exception ex)
            {
                _lastFramebufferCaptureError = ex.ToString();
                DiagnosticsList.Items.Add("Warning: framebuffer capture failed. " + ex.Message);
            }
        }

        private void WriteAutomationProbeResult(
            string outputPath,
            string metadataPath,
            double maxZoomCommandMs,
            double maxPanCommandMs,
            double maxWheelCommandMs,
            double maxDragCommandMs,
            RawOpenGlRenderStats? zoomStats,
            RawOpenGlRenderStats? panStats,
            RawOpenGlRenderStats? wheelStats,
            RawOpenGlRenderStats? dragStats,
            RawOpenGlRenderStats? externalInputStats,
            Exception? exception)
        {
            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var builder = new StringBuilder();
                builder.AppendLine("{");
                AppendJsonProperty(builder, "metadataPath", metadataPath, true);
                AppendJsonProperty(builder, "status", StatusText.Text, true);
                AppendJsonProperty(builder, "toolWindowActualWidth", ActualWidth, true);
                AppendJsonProperty(builder, "toolWindowActualHeight", ActualHeight, true);
                AppendJsonProperty(builder, "imageViewActualWidth", OpenGlImageView.ActualWidth, true);
                AppendJsonProperty(builder, "imageViewActualHeight", OpenGlImageView.ActualHeight, true);
                AppendJsonProperty(builder, "inspectorVisible", InspectorPanel.Visibility == Visibility.Visible, true);
                AppendJsonProperty(builder, "compactInspectorVisible", CompactInspectorPanel.Visibility == Visibility.Visible, true);
                AppendJsonProperty(builder, "vssdkAssembly", typeof(RawBufferToolWindowControl).Assembly.Location, true);
                AppendJsonProperty(builder, "openGlCanvasAssembly", typeof(RawOpenGlImageCanvas).Assembly.Location, true);
                AppendJsonProperty(builder, "framebufferCapturePath", _lastFramebufferCapturePath, true);
                AppendJsonProperty(builder, "framebufferCaptureError", _lastFramebufferCaptureError, true);
                AppendJsonProperty(builder, "pixelOverlayVisible", OpenGlImageView.PixelOverlayVisible, true);
                AppendJsonProperty(builder, "pixelOverlayText", OpenGlImageView.PixelOverlayText, true);
                AppendJsonProperty(builder, "pixelGridOverlayVisible", OpenGlImageView.PixelGridOverlayVisible, true);
                AppendJsonProperty(builder, "pixelStatusPosition", PixelPositionText.Text, true);
                AppendJsonProperty(builder, "pixelStatusColor", PixelColorText.Text, true);
                AppendJsonProperty(builder, "pixelStatusRaw", PixelRawText.Text, true);
                AppendJsonProperty(builder, "roiStats", RoiStatsText.Text, true);
                AppendJsonProperty(builder, "markerText", MarkerText.Text, true);
                AppendJsonProperty(builder, "selectedPixel", OpenGlImageView.SelectedPixelText, true);
                AppendJsonProperty(builder, "selectionOverlayEnabled", OpenGlImageView.SelectionOverlayEnabled, true);
                AppendJsonProperty(builder, "pinnedMarker", OpenGlImageView.PinnedMarkerText, true);
                AppendJsonProperty(builder, "blackLevel", BlackLevelTextBox.Text, true);
                AppendJsonProperty(builder, "whiteLevel", WhiteLevelTextBox.Text, true);
                AppendJsonProperty(builder, "openPathMs", _lastOpenPathMilliseconds, true);
                AppendJsonProperty(builder, "maxZoomCommandMs", maxZoomCommandMs, true);
                AppendJsonProperty(builder, "maxPanCommandMs", maxPanCommandMs, true);
                AppendJsonProperty(builder, "maxWheelCommandMs", maxWheelCommandMs, true);
                AppendJsonProperty(builder, "maxDragCommandMs", maxDragCommandMs, true);
                AppendJsonStats(builder, "zoom", zoomStats ?? new RawOpenGlRenderStats(), true);
                AppendJsonStats(builder, "pan", panStats ?? new RawOpenGlRenderStats(), true);
                AppendJsonStats(builder, "wheel", wheelStats ?? new RawOpenGlRenderStats(), true);
                AppendJsonStats(builder, "drag", dragStats ?? new RawOpenGlRenderStats(), true);
                AppendJsonStats(builder, "externalInput", externalInputStats ?? new RawOpenGlRenderStats(), exception != null);
                if (exception != null)
                {
                    AppendJsonProperty(builder, "error", exception.ToString(), false);
                }

                builder.AppendLine("}");
                File.WriteAllText(outputPath, builder.ToString());
            }
            catch
            {
                // Automation diagnostics must not break normal Visual Studio usage.
            }
        }

        private static void WriteExternalInputReadyFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            }
            catch
            {
                // Automation diagnostics must not break normal Visual Studio usage.
            }
        }

        private static int GetEnvironmentInt(string name, int fallback)
        {
            int parsed;
            return int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && parsed > 0
                ? parsed
                : fallback;
        }

        private static void AppendJsonStats(StringBuilder builder, string name, RawOpenGlRenderStats stats, bool trailingComma)
        {
            builder.Append("  \"").Append(name).AppendLine("\": {");
            AppendJsonProperty(builder, "frameCount", stats.FrameCount, true, 4);
            AppendJsonProperty(builder, "textureUploadCount", stats.TextureUploadCount, true, 4);
            AppendJsonProperty(builder, "averageFrameMs", stats.AverageFrameMilliseconds, true, 4);
            AppendJsonProperty(builder, "maxFrameMs", stats.MaxFrameMilliseconds, true, 4);
            AppendJsonProperty(builder, "averageUploadMs", stats.AverageTextureUploadMilliseconds, true, 4);
            AppendJsonProperty(builder, "maxUploadMs", stats.MaxTextureUploadMilliseconds, true, 4);
            AppendJsonProperty(builder, "wheelInputCount", stats.WheelInputCount, true, 4);
            AppendJsonProperty(builder, "dragInputCount", stats.DragInputCount, true, 4);
            AppendJsonProperty(builder, "averageWheelInputMs", stats.AverageWheelInputMilliseconds, true, 4);
            AppendJsonProperty(builder, "maxWheelInputMs", stats.MaxWheelInputMilliseconds, true, 4);
            AppendJsonProperty(builder, "averageDragInputMs", stats.AverageDragInputMilliseconds, true, 4);
            AppendJsonProperty(builder, "maxDragInputMs", stats.MaxDragInputMilliseconds, false, 4);
            builder.Append("  }");
            builder.AppendLine(trailingComma ? "," : string.Empty);
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, string value, bool trailingComma, int indent = 2)
        {
            builder.Append(' ', indent).Append('"').Append(name).Append("\": \"").Append(EscapeJson(value)).Append('"');
            builder.AppendLine(trailingComma ? "," : string.Empty);
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, double value, bool trailingComma, int indent = 2)
        {
            builder.Append(' ', indent).Append('"').Append(name).Append("\": ");
            builder.Append(Math.Round(value, 3).ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(trailingComma ? "," : string.Empty);
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, int value, bool trailingComma, int indent = 2)
        {
            builder.Append(' ', indent).Append('"').Append(name).Append("\": ");
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(trailingComma ? "," : string.Empty);
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, long value, bool trailingComma, int indent = 2)
        {
            builder.Append(' ', indent).Append('"').Append(name).Append("\": ");
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(trailingComma ? "," : string.Empty);
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, bool value, bool trailingComma, int indent = 2)
        {
            builder.Append(' ', indent).Append('"').Append(name).Append("\": ");
            builder.Append(value ? "true" : "false");
            builder.AppendLine(trailingComma ? "," : string.Empty);
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static VisualizerHandoffRequest ReadHandoffRequestWithRetry(string requestPath)
        {
            Exception? last = null;
            for (var i = 0; i < 5; i++)
            {
                try
                {
                    return VisualizerHandoffInbox.ReadSnapshotRequestInfo(requestPath);
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

        private static bool ShouldDeleteSnapshotDirectoryOnDispose(string metadataPath)
        {
            string snapshotDirectory;
            return VisualStudioTempStore.TryGetOwnedSnapshotDirectory(metadataPath, out snapshotDirectory);
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

        private static BitmapSource CreateErrorThumbnailSource()
        {
            const int width = 96;
            const int height = 72;
            var pixels = new byte[width * height * 4];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = (y * width + x) * 4;
                    var diagonal = Math.Abs(x - y) < 3 || Math.Abs((width - x - 1) - y) < 3;
                    var border = x < 2 || y < 2 || x >= width - 2 || y >= height - 2;
                    pixels[index] = diagonal || border ? (byte)80 : (byte)35;
                    pixels[index + 1] = diagonal || border ? (byte)80 : (byte)35;
                    pixels[index + 2] = diagonal || border ? (byte)210 : (byte)80;
                    pixels[index + 3] = 255;
                }
            }

            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
            bitmap.Freeze();
            return bitmap;
        }

        private static string FormatDescriptor(ImageDocument document)
        {
            if (document.IsError)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Type       Error\nReason     {0}\nFile       {1}",
                    document.ErrorMessage,
                    document.DisplayPath);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "Type       {0}\nWidth      {1}\nHeight     {2}\nStride     {3}\nMin Stride {4}\nFormat     {5}\nValid Bits {6}\nByte Order {7}\nBytes      {8:N0}\nExpected   {9:N0}\nFile       {10}",
                document.SourceType,
                document.Descriptor.Width,
                document.Descriptor.Height,
                document.Descriptor.Stride,
                document.Descriptor.GetMinimumStride(),
                document.Descriptor.PixelFormat,
                document.Descriptor.ValidBits,
                document.Descriptor.ByteOrder,
                document.Source.Length,
                document.Descriptor.GetRequiredByteCount(),
                document.DisplayPath);
        }

        private sealed class ImageDocument : IDisposable
        {
            public string DisplayPath { get; private set; }
            public string Title { get; private set; }
            public string SourceType { get; private set; }
            public RawImageSource Source { get; private set; }
            public RawImageDescriptor Descriptor { get; private set; }
            public RawOpenGlViewState? ViewState { get; set; }
            public BitmapSource? Thumbnail { get; private set; }
            public string ErrorMessage { get; private set; }
            private readonly string? _ownedSnapshotDirectory;
            private bool _disposed;

            public bool IsError
            {
                get { return !string.IsNullOrWhiteSpace(ErrorMessage); }
            }

            public string Summary
            {
                get
                {
                    if (IsError)
                    {
                        return "Error  " + ErrorMessage;
                    }

                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} x {1}  {2}  stride {3}  {4}",
                        Descriptor.Width,
                        Descriptor.Height,
                        Descriptor.PixelFormat,
                        Descriptor.Stride,
                        SourceType);
                }
            }

            public ImageDocument(
                string displayPath,
                RawImageSource source,
                RawImageDescriptor descriptor,
                string? title,
                string sourceType,
                bool deleteSnapshotDirectoryOnDispose = false)
            {
                DisplayPath = GetDisplayPath(displayPath);
                Title = string.IsNullOrWhiteSpace(title) ? CreateTitle(DisplayPath) : title!.Trim();
                SourceType = string.IsNullOrWhiteSpace(sourceType) ? "Unknown" : sourceType;
                Source = source ?? throw new ArgumentNullException("source");
                Descriptor = descriptor == null ? throw new ArgumentNullException("descriptor") : descriptor.Clone();
                Thumbnail = CreateThumbnailSource(Source, Descriptor);
                ErrorMessage = string.Empty;
                _ownedSnapshotDirectory = GetOwnedSnapshotDirectory(DisplayPath, deleteSnapshotDirectoryOnDispose);
            }

            private ImageDocument(string displayPath, string errorMessage, bool deleteSnapshotDirectoryOnDispose)
            {
                DisplayPath = GetDisplayPath(displayPath);
                Title = "Open failed: " + CreateTitle(DisplayPath);
                SourceType = "Error";
                Descriptor = new RawImageDescriptor
                {
                    Width = 1,
                    Height = 1,
                    Stride = 1,
                    PixelFormat = RawPixelFormat.Mono8,
                    ValidBits = 8,
                    ByteOrder = RawByteOrder.LittleEndian
                };
                Source = RawImageSource.FromMemory(new byte[] { 0 }, Descriptor);
                Thumbnail = CreateErrorThumbnailSource();
                ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "Unknown open failure." : errorMessage;
                _ownedSnapshotDirectory = GetOwnedSnapshotDirectory(DisplayPath, deleteSnapshotDirectoryOnDispose);
            }

            public static ImageDocument CreateError(string displayPath, string errorMessage, bool deleteSnapshotDirectoryOnDispose = false)
            {
                return new ImageDocument(displayPath, errorMessage, deleteSnapshotDirectoryOnDispose);
            }

            public void ReplaceSource(RawImageSource source, RawImageDescriptor descriptor)
            {
                if (source == null)
                {
                    throw new ArgumentNullException("source");
                }

                Source = source;
                Descriptor = descriptor == null ? throw new ArgumentNullException("descriptor") : descriptor.Clone();
                Thumbnail = CreateThumbnailSource(Source, Descriptor);
                ErrorMessage = string.Empty;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                Source.Dispose();
                if (_ownedSnapshotDirectory != null)
                {
                    VisualStudioTempStore.TryDeleteDirectory(_ownedSnapshotDirectory);
                }
            }

            private static string CreateTitle(string displayPath)
            {
                var fileName = Path.GetFileName(displayPath);
                const string suffix = ".rbuf.json";
                return fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                    ? fileName.Substring(0, fileName.Length - suffix.Length)
                    : fileName;
            }

            private static string GetDisplayPath(string displayPath)
            {
                try
                {
                    return Path.GetFullPath(displayPath);
                }
                catch
                {
                    return displayPath ?? string.Empty;
                }
            }

            private static string? GetOwnedSnapshotDirectory(string displayPath, bool deleteSnapshotDirectoryOnDispose)
            {
                if (!deleteSnapshotDirectoryOnDispose)
                {
                    return null;
                }

                string snapshotDirectory;
                return VisualStudioTempStore.TryGetOwnedSnapshotDirectory(displayPath, out snapshotDirectory)
                    ? snapshotDirectory
                    : null;
            }
        }
    }
}
