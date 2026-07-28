using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Microsoft.VisualStudio.Shell;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.OpenGlCanvas;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.VisualStudio;
using RawBufferVisualizer.VisualStudio.ObjectSource;
using Line = System.Windows.Shapes.Line;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    public partial class RawBufferToolWindowControl : UserControl
    {
        private const long MaxCpuPreviewBytes = 512L * 1024L * 1024L;
        private const long MaxInMemorySourceBytes = 512L * 1024L * 1024L;
        private const int MaxManagedArrayDebuggerElements = 256;
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
        private bool _automaticScanPending;
        private bool _automaticScanRunning;
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
        private CancellationTokenSource? _diagnosisCancellation;
        private readonly List<string> _recentExpressions = new List<string>();
        private readonly AutomaticVisionInspector _automaticVisionInspector = new AutomaticVisionInspector();
        private readonly AutomaticInspectionPreferencesStore _automaticInspectionPreferencesStore =
            AutomaticInspectionPreferencesStore.CreateDefault();
        private AutomaticInspectionPreferences _automaticInspectionPreferences =
            new AutomaticInspectionPreferences();
        private bool _loadingAutomaticInspectionPreferences = true;
        private EnvDTE80.DTE2? _dte;

        public void SetDte(EnvDTE80.DTE2 dte)
        {
            _dte = dte;
        }

        public bool IsAutoInspectEnabled
        {
            get
            {
                return AutoInspectBox == null
                    ? _automaticInspectionPreferences.AutoScanOnBreak
                    : AutoInspectBox.IsChecked == true;
            }
        }

        public RawBufferToolWindowControl()
        {
            InitializeComponent();
            LoadAutomaticInspectionPreferences();
            ImageList.ItemsSource = _documents;
            OpenGlImageView.PixelHovered += OpenGlImageView_PixelHovered;
            OpenGlImageView.PixelPinned += OpenGlImageView_PixelPinned;
            OpenGlImageView.PixelSelected += OpenGlImageView_PixelSelected;
            OpenGlImageView.ViewChanged += OpenGlImageView_ViewChanged;
            OpenGlImageView.SourceUnavailable += OpenGlImageView_SourceUnavailable;
            OpenGlImageView.SelectionOverlayEnabled = SelectionOverlayBox.IsChecked == true;
            InterpretPixelFormatBox.ItemsSource = Enum.GetValues(typeof(RawPixelFormat));
            InterpretByteOrderBox.ItemsSource = Enum.GetValues(typeof(RawByteOrder));
            CompactInterpretPixelFormatBox.ItemsSource = Enum.GetValues(typeof(RawPixelFormat));
            CompactInterpretByteOrderBox.ItemsSource = Enum.GetValues(typeof(RawByteOrder));
            _performanceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _performanceTimer.Tick += delegate { UpdatePerformanceText(); };
            _performanceTimer.Start();
            Unloaded += delegate { _performanceTimer.Stop(); };
            _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _blinkTimer.Tick += BlinkTimer_Tick;
            Unloaded += delegate { _blinkTimer.Stop(); };
            Unloaded += delegate { CancelDiagnosis(); };
            ClearPixelStatus();
            UpdateZoomStatus();
            UpdatePerformanceText();
            UpdateCompareText();
            UpdateStatus();
            UpdateTempUsageStatus();
            ApplyResponsiveLayout(ActualWidth);
        }

        public void ScanLocals()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_dte == null || _dte.Debugger == null)
            {
                SetAutomaticScanStatus("Break in the debugger to scan the current frame.");
                return;
            }

            if (_dte.Debugger.CurrentMode != EnvDTE.dbgDebugMode.dbgBreakMode)
            {
                SetAutomaticScanStatus("Automatic inspection waits for Break Mode.");
                return;
            }

            SetAutomaticScanStatus("Scanning current stack frame...");
            AutomaticVisionScanResult scan;
            try
            {
                scan = _automaticVisionInspector.Scan(_dte.Debugger);
            }
            catch (Exception ex)
            {
                SetAutomaticScanStatus("Scan failed: " + ex.Message);
                return;
            }

            RawBufferVisualizerPackageLog.Write(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Automatic scan inferred {0} candidate(s) from {1} local(s) and {2} argument(s); {3} duplicate expression(s) skipped",
                    scan.Inspections.Count,
                    scan.LocalExpressionCount,
                    scan.ArgumentExpressionCount,
                    scan.DuplicateExpressionCount));
            RemoveAutomaticInspectionDocuments();
            AutomaticFrameText.Text = string.IsNullOrWhiteSpace(scan.FrameDisplayName)
                ? "Current stack frame"
                : scan.FrameDisplayName;

            var opened = 0;
            var needsMapping = 0;
            var failed = 0;
            var hidden = 0;
            _automaticScanRunning = true;
            try
            {
                for (var i = 0; i < scan.Inspections.Count; i++)
                {
                    var inspection = scan.Inspections[i];
                    try
                    {
                        var mapping = inspection.Mapping ?? inspection.CreateTransientMapping();
                        var dataTypeName = inspection.GetDataTypeName();
                        var isArrayBacked = dataTypeName.EndsWith("[]", StringComparison.Ordinal);
                        var canOpen = inspection.UsesSavedMapping || inspection.Inference.CanAutoOpen;
                        if (canOpen)
                        {
                            RawBufferVisualizerPackageLog.Write(
                                "Automatic scan opening " + inspection.RootExpression);
                            string openError;
                            var openedSuccessfully = isArrayBacked
                                ? TryOpenMappedArray(
                                    _dte.Debugger,
                                    scan.FrameDisplayName,
                                    inspection.RootExpression,
                                    mapping,
                                    inspection.RuntimeTypeName,
                                    dataTypeName,
                                    inspection.UsesSavedMapping ? (RawPixelFormat?)null : inspection.Inference.PixelFormat,
                                    out openError,
                                    inspection.StableKey)
                                : TryOpenMappedVariable(
                                    _dte.Debugger,
                                    inspection.RootExpression,
                                    mapping,
                                    inspection.RuntimeTypeName,
                                    out openError,
                                    inspection.StableKey);
                            if (openedSuccessfully)
                            {
                                RawBufferVisualizerPackageLog.Write(
                                    "Automatic scan opened " + inspection.RootExpression);
                                var document = FindHandoffDocument(inspection.StableKey);
                                if (document != null)
                                {
                                    document.SetAutomaticInspection(
                                        inspection.UsesSavedMapping ? 100 : inspection.Inference.ConfidenceScore,
                                        inspection.GetMembersSummary(),
                                        inspection.UsesSavedMapping
                                            ? "Saved type mapping applied and live memory validated."
                                            : "Member inference and live memory descriptor validation passed.",
                                        inspection.Inventory,
                                        inspection.AssemblyName,
                                        GetDebuggeeProcessId(_dte.Debugger),
                                        false,
                                        mapping.Members);
                                    ImageList.Items.Refresh();
                                    UpdateAutomaticInspectionPanel(document);
                                }

                                opened++;
                                continue;
                            }

                            RawBufferVisualizerPackageLog.Write(
                                "Automatic scan open failed " + inspection.RootExpression + ": " + openError);
                            AddAutomaticOpenFailure(inspection, openError);
                            failed++;
                            continue;
                        }

                        if (inspection.Inference.ConfidenceScore < 40)
                        {
                            hidden++;
                            continue;
                        }

                        var reason = isArrayBacked
                            ? "Managed array buffer was recognized, but its debugger memory could not be read safely."
                            : inspection.Inference.RequiresExplicitLayout
                                ? "Padding, chunk data, or an image offset was detected; map an explicit stride or use an SDK adapter."
                                : inspection.Inference.RequiresPixelFormatMapping
                                    ? "Pixel format needs one explicit mapping before this buffer can be opened."
                                    : "Required image metadata is incomplete or below the automatic-open confidence gate.";
                        AddAutomaticMappingCandidate(inspection, reason);
                        needsMapping++;
                    }
                    catch (Exception ex)
                    {
                        var reason = "Unexpected inspection failure: " + ex.Message;
                        RawBufferVisualizerPackageLog.Write(
                            "Automatic scan candidate error " + inspection.RootExpression + ": " + ex);
                        AddAutomaticOpenFailure(inspection, reason);
                        failed++;
                    }
                }
            }
            finally
            {
                _automaticScanRunning = false;
            }

            var activeAutomaticDocument = _documents.LastOrDefault(
                    document => document.IsAutomaticInspection && !document.IsError)
                ?? _documents.LastOrDefault(document => document.IsAutomaticInspection);
            if (activeAutomaticDocument != null)
            {
                ActivateDocument(activeAutomaticDocument);
            }

            if (scan.Inspections.Count == 0)
            {
                SetAutomaticScanStatus("No image-like locals or arguments met the 40% candidate threshold.");
            }
            else
            {
                SetAutomaticScanStatus(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} detected: {1} opened, {2} need mapping, {3} failed{4}.",
                    scan.Inspections.Count,
                    opened,
                    needsMapping,
                    failed,
                    hidden > 0 ? ", " + hidden + " hidden" : string.Empty));
            }

            UpdateStatus();
        }

        public void ScheduleAutomaticScan()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_automaticScanPending)
            {
                return;
            }

            _automaticScanPending = true;
#pragma warning disable VSTHRD001, VSTHRD110
            // The debugger's Break event can arrive before the debug engine has released its
            // transition locks. Scheduling at ContextIdle lets that event return before DTE
            // expressions and paused-process memory are inspected. This control also runs in
            // the standalone layout smoke host, where the VS JoinableTaskFactory is unavailable.
            Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() =>
                {
                    _automaticScanPending = false;
                    RawBufferVisualizerPackageLog.Write("Automatic scan started");
                    try
                    {
                        ScanLocals();
                        RawBufferVisualizerPackageLog.Write("Automatic scan ended");
                    }
                    catch (Exception ex)
                    {
                        _automaticScanRunning = false;
                        RawBufferVisualizerPackageLog.Write("Automatic scan unhandled error " + ex);
                        throw;
                    }
                }));
#pragma warning restore VSTHRD001, VSTHRD110
        }

        private void AddAutomaticMappingCandidate(AutomaticVisionInspection inspection, string reason)
        {
            var details = inspection.Inference.Reasons.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, inspection.Inference.Reasons);
            var document = ImageDocument.CreateError(
                inspection.RootExpression,
                inspection.RuntimeTypeName,
                "MappingRequired",
                reason,
                details,
                false,
                inspection.Inventory,
                inspection.AssemblyName,
                _dte == null || _dte.Debugger == null ? 0 : GetDebuggeeProcessId(_dte.Debugger));
            document.SetAutomaticInspection(
                inspection.UsesSavedMapping ? 100 : inspection.Inference.ConfidenceScore,
                inspection.GetMembersSummary(),
                reason,
                inspection.Inventory,
                inspection.AssemblyName,
                _dte == null || _dte.Debugger == null ? 0 : GetDebuggeeProcessId(_dte.Debugger),
                true,
                inspection.Inference.Members);
            _documents.Add(document);
            if (!_automaticScanRunning)
            {
                ActivateDocument(document);
            }
        }

        private void AddAutomaticOpenFailure(AutomaticVisionInspection inspection, string reason)
        {
            var details = inspection.Inference.Reasons.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, inspection.Inference.Reasons);
            var document = ImageDocument.CreateError(
                inspection.RootExpression,
                inspection.RuntimeTypeName,
                "AutomaticOpenFailed",
                string.IsNullOrWhiteSpace(reason)
                    ? "The detected image could not be opened. Scan again after checking its lifetime and current values."
                    : reason,
                details,
                false,
                inspection.Inventory,
                inspection.AssemblyName,
                _dte == null || _dte.Debugger == null ? 0 : GetDebuggeeProcessId(_dte.Debugger));
            document.SetAutomaticInspection(
                inspection.UsesSavedMapping ? 100 : inspection.Inference.ConfidenceScore,
                inspection.GetMembersSummary(),
                "Recognition succeeded, but current-value or paused-memory validation failed. Other detected images remain available.",
                inspection.Inventory,
                inspection.AssemblyName,
                _dte == null || _dte.Debugger == null ? 0 : GetDebuggeeProcessId(_dte.Debugger),
                false,
                inspection.Inference.Members);
            _documents.Add(document);
        }

        private void RemoveAutomaticInspectionDocuments()
        {
            for (var i = _documents.Count - 1; i >= 0; i--)
            {
                var document = _documents[i];
                if (!document.IsAutomaticInspection)
                {
                    continue;
                }

                if (ReferenceEquals(_compareA, document))
                {
                    _compareA = null;
                }

                if (ReferenceEquals(_compareB, document))
                {
                    _compareB = null;
                }

                if (ReferenceEquals(_activeDocument, document))
                {
                    _activeDocument = null;
                }

                _documents.RemoveAt(i);
                document.Dispose();
            }

            if (_activeDocument == null && _documents.Count > 0)
            {
                ActivateDocument(_documents[_documents.Count - 1]);
            }
            else if (_activeDocument == null)
            {
                UpdateAutomaticInspectionPanel(null);
                OpenGlImageView.ClearImage();
                DescriptorText.Text = string.Empty;
            }
        }

        private void SetAutomaticScanStatus(string text)
        {
            if (AutomaticScanStatusText != null)
            {
                AutomaticScanStatusText.Text = text;
            }
        }

        private void ScanNow_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ScanLocals();
        }

        private void AutoInspectBox_Changed(object sender, RoutedEventArgs e)
        {
            var enabled = AutoInspectBox.IsChecked == true;
            _automaticInspectionPreferences.AutoScanOnBreak = enabled;
            if (_loadingAutomaticInspectionPreferences)
            {
                return;
            }

            string saveError;
            if (!_automaticInspectionPreferencesStore.TrySave(_automaticInspectionPreferences, out saveError))
            {
                SetAutomaticScanStatus(
                    (enabled ? "Enabled" : "Paused") + ", but the preference was not saved. " + saveError);
                return;
            }

            if (enabled)
            {
                SetAutomaticScanStatus("Enabled and saved. The next Break Mode event will refresh this list.");
            }
            else
            {
                SetAutomaticScanStatus("Paused and saved. Scan Now remains available.");
            }
        }

        private void LoadAutomaticInspectionPreferences()
        {
            _automaticInspectionPreferences = _automaticInspectionPreferencesStore.Load();
            AutoInspectBox.IsChecked = _automaticInspectionPreferences.AutoScanOnBreak;
            _loadingAutomaticInspectionPreferences = false;
            if (!string.IsNullOrWhiteSpace(_automaticInspectionPreferencesStore.LastLoadError))
            {
                SetAutomaticScanStatus(_automaticInspectionPreferencesStore.LastLoadError);
            }
            else
            {
                SetAutomaticScanStatus(
                    _automaticInspectionPreferences.AutoScanOnBreak
                        ? "Auto Inspect on Break is enabled. This preference persists across Visual Studio restarts."
                        : "Auto Inspect on Break is paused. Scan Now remains available.");
            }
        }

        public void OpenHandoffRequest(string requestPath)
        {
            if (!Dispatcher.CheckAccess())
            {
#pragma warning disable VSTHRD001
                // VSTHRD001: Dispatcher.Invoke is intentional here. This WPF control is also
                // hosted outside Visual Studio (layout smoke host), where JoinableTaskFactory
                // and Shell assemblies are unavailable, so the vs-threading JTF pattern cannot
                // be used. Callers hold no locks and the UI thread never waits on these
                // background handoff threads, so the synchronous marshal cannot deadlock.
                Dispatcher.Invoke(() => OpenHandoffRequest(requestPath));
#pragma warning restore VSTHRD001
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
                    AddErrorDocument(
                        displayName,
                        request.SourceType,
                        request.ErrorType,
                        request.ErrorMessage,
                        request.ErrorDetails,
                        request.MemberInventory,
                        request.ItemAssemblyName,
                        request.DebuggeeProcessId);
                }
                else if (request.IsLiveMemory)
                {
                    OpenLiveMemory(request);
                }
                else
                {
                    OpenPath(
                        request.MetadataPath,
                        request.DisplayName,
                        request.SourceType,
                        request.HandoffId,
                        request.IsPreview);
                }
            }
            catch (Exception ex)
            {
                AddErrorDocument(
                    requestPath,
                    "Debugger handoff",
                    ex.GetType().FullName ?? ex.GetType().Name,
                    ex.Message,
                    ex.ToString());
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
            OpenPath(path, null, null, null, false);
        }

        private void OpenPath(
            string path,
            string? title,
            string? sourceType,
            string? handoffId,
            bool isPreview)
        {
            if (!Dispatcher.CheckAccess())
            {
#pragma warning disable VSTHRD001
                // VSTHRD001: Dispatcher.Invoke is intentional here. This WPF control is also
                // hosted outside Visual Studio (layout smoke host), where JoinableTaskFactory
                // and Shell assemblies are unavailable, so the vs-threading JTF pattern cannot
                // be used. Callers hold no locks and the UI thread never waits on these
                // background handoff threads, so the synchronous marshal cannot deadlock.
                Dispatcher.Invoke(() => OpenPath(path, title, sourceType, handoffId, isPreview));
#pragma warning restore VSTHRD001
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
                var existingDocument = FindHandoffDocument(handoffId);
                if (existingDocument != null)
                {
                    var wasActive = ReferenceEquals(_activeDocument, existingDocument);
                    existingDocument.ReplaceSource(
                        fullPath,
                        source,
                        reference.Descriptor,
                        title,
                        resolvedSourceType,
                        isPreview);
                    ImageList.Items.Refresh();
                    if (wasActive)
                    {
                        DescriptorText.Text = FormatDescriptor(existingDocument);
                        UpdateInterpretationControls(existingDocument.Descriptor);
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

                    if (!isPreview)
                    {
                        DiagnosticsList.Items.Insert(0, "Info: full-resolution debugger transfer completed.");
                    }
                    _lastOpenPathMilliseconds = openWatch.Elapsed.TotalMilliseconds;
                    ScheduleAutomationProbeIfRequested(fullPath);
                    return;
                }

                var document = new ImageDocument(
                    fullPath,
                    source,
                    reference.Descriptor,
                    title,
                    resolvedSourceType,
                    ShouldDeleteSnapshotDirectoryOnDispose(fullPath),
                    handoffId,
                    isPreview);
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
                AddErrorDocument(
                    fullPath,
                    string.IsNullOrWhiteSpace(sourceType) ? "Raw snapshot" : sourceType!,
                    ex.GetType().FullName ?? ex.GetType().Name,
                    ex.Message,
                    ex.ToString());
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
            CancelDiagnosis();
            DiagnosisPanel.Visibility = Visibility.Collapsed;
            CompactDiagnosisPanel.Visibility = Visibility.Collapsed;
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

        private void ImageListContextMenu_Opening(object sender, RoutedEventArgs e)
        {
            UpdateMapThisTypeMenuItem();
        }

        private void UpdateMapThisTypeMenuItem()
        {
            var document = ImageList.SelectedItem as ImageDocument;
            MapThisTypeMenuItem.Visibility = document != null
                && document.IsError
                && document.HasMemberInventory
                && (!document.IsAutomaticInspection || document.AutomaticMappingRequired)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OpenVariable_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenVariableDialog(_recentExpressions, EvaluateOpenVariable)
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();
        }

        // Open Variable is always invoked from the UI thread (button click or dialog event handler).
        // The analyzer cannot propagate the UI-thread assertion across the private helpers below.
#pragma warning disable VSTHRD010
        private string EvaluateOpenVariable(string expressionText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.IsNullOrWhiteSpace(expressionText))
            {
                return "Enter an expression.";
            }

            EnvDTE.DTE? dte;
            try
            {
                dte = TryGetDte();
            }
            catch
            {
                dte = null;
            }

            if (dte == null)
            {
                return "Open Variable is only available inside Visual Studio with an active debug session.";
            }

            try
            {
                var debugger = dte.Debugger;
                if (debugger == null || debugger.CurrentMode != EnvDTE.dbgDebugMode.dbgBreakMode)
                {
                    return "No active debug session in break mode. Break in the debugger and try again.";
                }

                var expression = debugger.GetExpression(expressionText, true, 2000);
                if (expression == null || !expression.IsValidValue)
                {
                    return "Expression could not be evaluated: " + expressionText;
                }

                var typeName = expression.Type ?? string.Empty;
                var mapping = TypeMappingStore.Default.FindMappingByTypeNameOnly(typeName);
                if (mapping != null)
                {
                    string openError;
                    if (!TryOpenMappedVariable(debugger, expressionText, mapping, typeName, out openError))
                    {
                        return openError;
                    }

                    RememberExpression(expressionText);
                    return string.Empty;
                }

                var inventory = BuildEnvInventory(expression);
                if (inventory.Count == 0)
                {
                    return "Expression has no readable data members: " + expressionText;
                }

                var mappingDialog = new TypeMappingDialog(inventory, typeName, string.Empty, GetDebuggeeProcessId(debugger))
                {
                    Owner = Window.GetWindow(this)
                };
                if (mappingDialog.ShowDialog() == true)
                {
                    RememberExpression(expressionText);
                    DiagnosticsList.Items.Insert(0, "Info: type mapping saved for " + typeName + ". Open Variable again to apply it.");
                    return string.Empty;
                }

                return "Mapping was not saved.";
            }
            catch (Exception ex)
            {
                return "Open Variable failed: " + ex.Message;
            }
        }

        private bool TryOpenMappedVariable(
            EnvDTE.Debugger debugger,
            string baseExpression,
            TypeMapping mapping,
            string typeName,
            out string error,
            string? handoffId = null)
        {
            error = string.Empty;
            var members = mapping.Members;
            if (members == null)
            {
                error = "Type mapping is empty.";
                return false;
            }

            var dataExpression = EvaluateChild(debugger, baseExpression, members.Data);
            if (dataExpression == null)
            {
                error = "Mapped data member could not be evaluated: " + members.Data;
                return false;
            }

            if ((dataExpression.Type ?? string.Empty).EndsWith("[]", StringComparison.Ordinal))
            {
                error = "Array-backed mapped types open via collections (wrap the variable in an array, for example new[]{ " + baseExpression + " }).";
                return false;
            }

            long address;
            if (!TryParsePointerValue(dataExpression.Value, out address) || address == 0)
            {
                error = "Mapped data member is not a readable pointer: " + members.Data;
                return false;
            }

            if (!TryValidateMappedPointerLayout(debugger, baseExpression, members, address, out error))
            {
                return false;
            }

            RawImageDescriptor descriptor;
            long bufferLength;
            if (!TryBuildMappedDescriptor(
                debugger,
                baseExpression,
                mapping,
                out descriptor,
                out bufferLength,
                out error))
            {
                return false;
            }

            var processId = GetDebuggeeProcessId(debugger);
            if (processId <= 0)
            {
                error = "No debugged process is available for live memory reads.";
                return false;
            }

            try
            {
                OpenLiveMemory(new VisualizerHandoffRequest(
                    string.Empty,
                    baseExpression,
                    typeName,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    handoffId ?? string.Empty,
                    false,
                    processId,
                    address,
                    bufferLength,
                    descriptor));
            }
            catch (Exception ex)
            {
                error = "Live memory open failed: " + ex.Message;
                return false;
            }

            return true;
        }

        private bool TryOpenMappedArray(
            EnvDTE.Debugger debugger,
            string frameFunctionName,
            string baseExpression,
            TypeMapping mapping,
            string typeName,
            string dataTypeName,
            RawPixelFormat? inferredPixelFormat,
            out string error,
            string? handoffId = null)
        {
            error = string.Empty;
            var members = mapping.Members;
            if (members == null || string.IsNullOrWhiteSpace(members.Data))
            {
                error = "Type mapping does not name a managed array data member.";
                return false;
            }

            var dataExpression = EvaluateChild(debugger, baseExpression, members.Data);
            if (dataExpression == null || !(dataExpression.Type ?? string.Empty).EndsWith("[]", StringComparison.Ordinal))
            {
                error = "Mapped data member is not a readable managed array: " + members.Data;
                return false;
            }

            RawImageDescriptor descriptor;
            long mappedBufferLength;
            if (!TryBuildMappedDescriptor(
                debugger,
                baseExpression,
                mapping,
                out descriptor,
                out mappedBufferLength,
                out error,
                inferredPixelFormat))
            {
                return false;
            }

            var elementSize = GetManagedArrayElementSize(dataTypeName);
            if (elementSize == 0)
            {
                error = "Managed array element type is not supported: " + dataTypeName;
                return false;
            }

            long elementCount;
            if (!TryReadOptionalChildLong(debugger, baseExpression, members.Data + ".Length", out elementCount)
                || elementCount <= 0)
            {
                error = "Managed array length could not be evaluated without calling debuggee methods.";
                return false;
            }

            var availableByteCount = elementCount * elementSize;
            var requiredByteCount = descriptor.GetRequiredByteCount();
            if (requiredByteCount <= 0 || requiredByteCount > MaxInMemorySourceBytes)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "Automatic managed-array open is limited to {0:N0} bytes; this descriptor requires {1:N0}.",
                    MaxInMemorySourceBytes,
                    requiredByteCount);
                return false;
            }

            if (availableByteCount < requiredByteCount || mappedBufferLength < requiredByteCount)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "Managed array is too short for the inferred descriptor ({0:N0} available, {1:N0} required).",
                    Math.Min(availableByteCount, mappedBufferLength),
                    requiredByteCount);
                return false;
            }

            byte[] buffer;
            var firstElementExpression = "(" + baseExpression + "." + members.Data + ")[0]";
            if (!VisualStudioDebugFrameContext.TryReadArrayBytes(
                frameFunctionName,
                baseExpression + "." + members.Data,
                firstElementExpression,
                dataTypeName,
                checked((int)requiredByteCount),
                out buffer,
                out error))
            {
                var nativeReadError = error;
                if (!TryCopyManagedArrayFromDebugger(
                    dataExpression,
                    dataTypeName,
                    checked((int)requiredByteCount),
                    out buffer,
                    out error))
                {
                    error = nativeReadError + " " + error;
                    return false;
                }
            }

            try
            {
                OpenManagedArrayBuffer(
                    buffer,
                    descriptor,
                    baseExpression,
                    typeName,
                    handoffId ?? string.Empty);
            }
            catch (Exception ex)
            {
                error = "Managed array open failed: " + ex.Message;
                return false;
            }

            return true;
        }

        private static bool TryCopyManagedArrayFromDebugger(
            EnvDTE.Expression arrayExpression,
            string dataTypeName,
            int requiredByteCount,
            out byte[] buffer,
            out string error)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            buffer = Array.Empty<byte>();
            error = string.Empty;

            var elementSize = GetManagedArrayElementSize(dataTypeName);
            if (elementSize <= 0 || requiredByteCount <= 0 || requiredByteCount % elementSize != 0)
            {
                error = "Managed array byte layout is not supported.";
                return false;
            }

            var requiredElements = requiredByteCount / elementSize;
            if (requiredElements > MaxManagedArrayDebuggerElements)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "The debugger did not expose contiguous array memory. Safe element-copy fallback is limited to {0:N0} elements; this image needs {1:N0}.",
                    MaxManagedArrayDebuggerElements,
                    requiredElements);
                return false;
            }

            EnvDTE.Expressions? elements;
            try
            {
                elements = arrayExpression.DataMembers;
            }
            catch (Exception ex)
            {
                error = "Managed array elements could not be enumerated safely: " + ex.Message;
                return false;
            }

            if (elements == null || elements.Count < requiredElements)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "Managed array element enumeration was incomplete ({0}/{1}).",
                    elements == null ? 0 : elements.Count,
                    requiredElements);
                return false;
            }

            buffer = new byte[requiredByteCount];
            for (var i = 0; i < requiredElements; i++)
            {
                EnvDTE.Expression element;
                try
                {
                    element = elements.Item(i + 1);
                }
                catch (Exception ex)
                {
                    error = string.Format(
                        CultureInfo.InvariantCulture,
                        "Managed array element {0} could not be read: {1}",
                        i,
                        ex.Message);
                    buffer = Array.Empty<byte>();
                    return false;
                }

                var rawValue = element == null ? string.Empty : element.Value;
                if (!TryWriteManagedArrayElement(buffer, i * elementSize, dataTypeName, rawValue))
                {
                    error = string.Format(
                        CultureInfo.InvariantCulture,
                        "Managed array element {0} has an unreadable value: {1}",
                        i,
                        rawValue);
                    buffer = Array.Empty<byte>();
                    return false;
                }
            }

            return true;
        }

        private static bool TryWriteManagedArrayElement(
            byte[] destination,
            int offset,
            string dataTypeName,
            string rawValue)
        {
            var value = (rawValue ?? string.Empty).Trim();
            if (dataTypeName.EndsWith("Byte[]", StringComparison.OrdinalIgnoreCase))
            {
                byte parsed;
                if (!TryParseDebuggerByte(value, out parsed))
                {
                    return false;
                }

                destination[offset] = parsed;
                return true;
            }

            if (dataTypeName.EndsWith("UInt16[]", StringComparison.OrdinalIgnoreCase)
                || dataTypeName.EndsWith("UShort[]", StringComparison.OrdinalIgnoreCase))
            {
                ushort parsed;
                if (!TryParseDebuggerUInt16(value, out parsed))
                {
                    return false;
                }

                var bytes = BitConverter.GetBytes(parsed);
                Buffer.BlockCopy(bytes, 0, destination, offset, bytes.Length);
                return true;
            }

            if (dataTypeName.EndsWith("Single[]", StringComparison.OrdinalIgnoreCase)
                || dataTypeName.EndsWith("Float[]", StringComparison.OrdinalIgnoreCase))
            {
                float parsed;
                value = value.TrimEnd('f', 'F');
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                {
                    return false;
                }

                var bytes = BitConverter.GetBytes(parsed);
                Buffer.BlockCopy(bytes, 0, destination, offset, bytes.Length);
                return true;
            }

            return false;
        }

        private static bool TryParseDebuggerByte(string value, out byte parsed)
        {
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return byte.TryParse(
                    GetLeadingToken(value.Substring(2)),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out parsed);
            }

            return byte.TryParse(
                GetLeadingToken(value),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed);
        }

        private static bool TryParseDebuggerUInt16(string value, out ushort parsed)
        {
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ushort.TryParse(
                    GetLeadingToken(value.Substring(2)),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out parsed);
            }

            return ushort.TryParse(
                GetLeadingToken(value),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed);
        }

        private static string GetLeadingToken(string value)
        {
            var separator = value.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
            return separator < 0 ? value : value.Substring(0, separator);
        }

        private static bool TryBuildMappedDescriptor(
            EnvDTE.Debugger debugger,
            string baseExpression,
            TypeMapping mapping,
            out RawImageDescriptor descriptor,
            out long bufferLength,
            out string error,
            RawPixelFormat? inferredPixelFormat = null)
        {
            descriptor = new RawImageDescriptor();
            bufferLength = 0;
            error = string.Empty;
            var members = mapping.Members;
            if (members == null)
            {
                error = "Type mapping is empty.";
                return false;
            }

            int width;
            int height;
            if (!TryReadChildInt(debugger, baseExpression, members.Width, "width", out width, out error)
                || !TryReadChildInt(debugger, baseExpression, members.Height, "height", out height, out error))
            {
                return false;
            }

            var pixelFormat = inferredPixelFormat ?? RawPixelFormat.Mono8;
            if (!string.IsNullOrWhiteSpace(members.PixelFormat))
            {
                var formatExpression = EvaluateChild(debugger, baseExpression, members.PixelFormat);
                if (formatExpression == null)
                {
                    error = "Mapped pixel format member could not be evaluated: " + members.PixelFormat;
                    return false;
                }

                var rawValue = (formatExpression.Value ?? string.Empty).Trim();
                var normalized = rawValue.Contains(".")
                    ? rawValue.Substring(rawValue.LastIndexOf('.') + 1)
                    : rawValue;
                var formatName = normalized;
                string? mappedName;
                if (mapping.PixelFormatMap != null
                    && (mapping.PixelFormatMap.TryGetValue(rawValue, out mappedName)
                        || mapping.PixelFormatMap.TryGetValue(normalized, out mappedName))
                    && !string.IsNullOrWhiteSpace(mappedName))
                {
                    formatName = mappedName!;
                }

                if (!Enum.TryParse(formatName, true, out pixelFormat))
                {
                    error = string.Format(
                        CultureInfo.InvariantCulture,
                        "Mapped pixel format value '{0}' is not a known RawPixelFormat.",
                        rawValue);
                    return false;
                }
            }

            RawByteOrder byteOrder;
            if (!Enum.TryParse(mapping.ByteOrder, true, out byteOrder))
            {
                error = "Mapping byte order is not supported: " + mapping.ByteOrder;
                return false;
            }

            descriptor = new RawImageDescriptor
            {
                Width = width,
                Height = height,
                PixelFormat = pixelFormat,
                ByteOrder = byteOrder
            };
            var validBits = 0;
            var hasValidBitsMember = !string.IsNullOrWhiteSpace(members.ValidBits);
            if (hasValidBitsMember
                && !TryReadOptionalChildInt(debugger, baseExpression, members.ValidBits, out validBits))
            {
                error = "Mapped valid-bits member could not be evaluated: " + members.ValidBits;
                return false;
            }
            descriptor.ValidBits = hasValidBitsMember
                ? validBits
                : GetDefaultValidBitsForOpenVariable(pixelFormat);
            var stride = 0;
            var hasStrideMember = !string.IsNullOrWhiteSpace(members.Stride);
            var hasExplicitStride = hasStrideMember
                && TryReadOptionalChildInt(debugger, baseExpression, members.Stride, out stride)
                && stride > 0;
            if (hasStrideMember && !hasExplicitStride)
            {
                error = "Mapped stride member could not be evaluated as a positive value: " + members.Stride;
                return false;
            }
            descriptor.Stride = hasExplicitStride ? stride : descriptor.GetMinimumStride();

            var requiredByteCount = descriptor.GetRequiredByteCount();
            var hasBufferLengthMember = !string.IsNullOrWhiteSpace(members.BufferLength);
            var hasBufferLength = hasBufferLengthMember
                && TryReadOptionalChildLong(
                    debugger,
                    baseExpression,
                    members.BufferLength,
                    out bufferLength);
            if (hasBufferLengthMember && !hasBufferLength)
            {
                error = "Mapped buffer-length member could not be evaluated: " + members.BufferLength;
                return false;
            }
            if (!hasBufferLengthMember)
            {
                bufferLength = requiredByteCount;
            }
            else if (bufferLength < requiredByteCount)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "Mapped buffer length {0} is smaller than the required image size {1}.",
                    bufferLength,
                    requiredByteCount);
                return false;
            }
            else if (!hasExplicitStride && bufferLength != requiredByteCount)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "Mapped buffer length {0} does not match the contiguous image size {1}; map an explicit stride or use an SDK adapter.",
                    bufferLength,
                    requiredByteCount);
                return false;
            }

            var diagnostics = RawBufferDiagnostics.AnalyzeLength(bufferLength, descriptor);
            if (RawBufferDiagnostics.HasErrors(diagnostics))
            {
                error = diagnostics.First(diagnostic => diagnostic.Severity == RawDiagnosticSeverity.Error).Message;
                return false;
            }

            return true;
        }

        private static bool TryValidateMappedPointerLayout(
            EnvDTE.Debugger debugger,
            string baseExpression,
            TypeMappingMembers members,
            long dataAddress,
            out string error)
        {
            error = string.Empty;
            if (string.Equals(GetLeafMemberName(members.Data), "ImageData", StringComparison.OrdinalIgnoreCase))
            {
                var bufferExpression = EvaluateChild(debugger, baseExpression, "Buffer");
                long bufferAddress;
                if (bufferExpression != null
                    && (!TryParsePointerValue(bufferExpression.Value, out bufferAddress)
                        || bufferAddress != dataAddress))
                {
                    error = "Mapped ImageData is offset from Buffer; chunk-prefixed frames require an SDK adapter.";
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(members.Stride))
            {
                return true;
            }

            var paddingNames = new[] { "PaddingX", "XPadding", "RowPadding", "LinePadding" };
            for (var i = 0; i < paddingNames.Length; i++)
            {
                var paddingExpression = EvaluateChild(debugger, baseExpression, paddingNames[i]);
                if (paddingExpression == null)
                {
                    continue;
                }

                long padding;
                if (!long.TryParse(
                    paddingExpression.Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out padding))
                {
                    error = "Mapped row padding could not be validated: " + paddingNames[i];
                    return false;
                }

                if (padding != 0)
                {
                    error = string.Format(
                        CultureInfo.InvariantCulture,
                        "Mapped {0} is {1}, but no explicit stride was mapped.",
                        paddingNames[i],
                        padding);
                    return false;
                }
            }

            return true;
        }

        private static string GetLeafMemberName(string? memberPath)
        {
            if (string.IsNullOrWhiteSpace(memberPath))
            {
                return string.Empty;
            }

            var dot = memberPath!.LastIndexOf('.');
            return dot >= 0 && dot + 1 < memberPath.Length
                ? memberPath.Substring(dot + 1)
                : memberPath;
        }

        private static int GetManagedArrayElementSize(string dataTypeName)
        {
            if (dataTypeName.EndsWith("Byte[]", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (dataTypeName.EndsWith("UInt16[]", StringComparison.OrdinalIgnoreCase)
                || dataTypeName.EndsWith("UShort[]", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (dataTypeName.EndsWith("Single[]", StringComparison.OrdinalIgnoreCase)
                || dataTypeName.EndsWith("Float[]", StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            return 0;
        }

        private void RememberExpression(string expressionText)
        {
            _recentExpressions.Remove(expressionText);
            _recentExpressions.Insert(0, expressionText);
            while (_recentExpressions.Count > 10)
            {
                _recentExpressions.RemoveAt(_recentExpressions.Count - 1);
            }
        }

        private static EnvDTE.DTE? TryGetDte()
        {
            try
            {
                return Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            }
            catch
            {
                return null;
            }
        }

        private static EnvDTE.Expression? EvaluateChild(EnvDTE.Debugger debugger, string baseExpression, string? memberName)
        {
            if (string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            try
            {
                var expression = debugger.GetExpression(baseExpression + "." + memberName, true, 2000);
                return expression != null && expression.IsValidValue ? expression : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryReadChildInt(
            EnvDTE.Debugger debugger,
            string baseExpression,
            string? memberName,
            string role,
            out int value,
            out string error)
        {
            value = 0;
            error = string.Empty;
            var expression = EvaluateChild(debugger, baseExpression, memberName);
            if (expression == null)
            {
                error = "Mapped " + role + " member could not be evaluated: " + memberName;
                return false;
            }

            if (!int.TryParse(expression.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value <= 0)
            {
                error = "Mapped " + role + " member is not a positive integer: " + memberName;
                return false;
            }

            return true;
        }

        private static bool TryReadOptionalChildInt(EnvDTE.Debugger debugger, string baseExpression, string? memberName, out int value)
        {
            value = 0;
            var expression = EvaluateChild(debugger, baseExpression, memberName);
            return expression != null
                && int.TryParse(expression.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryReadOptionalChildLong(EnvDTE.Debugger debugger, string baseExpression, string? memberName, out long value)
        {
            value = 0;
            var expression = EvaluateChild(debugger, baseExpression, memberName);
            return expression != null
                && long.TryParse(expression.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static int GetDebuggeeProcessId(EnvDTE.Debugger debugger)
        {
            try
            {
                var processes = debugger.DebuggedProcesses;
                if (processes != null && processes.Count > 0)
                {
                    return processes.Item(1).ProcessID;
                }
            }
            catch
            {
                return 0;
            }

            return 0;
        }

        private static List<VisualizerMemberInventoryItem> BuildEnvInventory(EnvDTE.Expression expression)
        {
            var inventory = new List<VisualizerMemberInventoryItem>();
            EnvDTE.Expressions? members = null;
            try
            {
                members = expression.DataMembers;
            }
            catch
            {
                return inventory;
            }

            if (members == null)
            {
                return inventory;
            }

            foreach (EnvDTE.Expression member in members)
            {
                string name;
                try
                {
                    name = member.Name;
                }
                catch
                {
                    continue;
                }

                string type;
                try
                {
                    type = member.Type ?? string.Empty;
                }
                catch
                {
                    type = string.Empty;
                }

                string value;
                try
                {
                    value = member.Value ?? string.Empty;
                }
                catch
                {
                    value = "<unreadable>";
                }

                if (value.Length > 64)
                {
                    value = value.Substring(0, 64) + "...";
                }

                inventory.Add(new VisualizerMemberInventoryItem
                {
                    Name = name,
                    Kind = "Member",
                    TypeName = ShortenEnvTypeName(type),
                    SampleValue = value
                });
            }

            return inventory;
        }

        private static string ShortenEnvTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return string.Empty;
            }

            var arraySuffix = string.Empty;
            var result = typeName;
            if (result.EndsWith("[]", StringComparison.Ordinal))
            {
                arraySuffix = "[]";
                result = result.Substring(0, result.Length - 2);
            }

            var dot = result.LastIndexOf('.');
            if (dot >= 0 && dot + 1 < result.Length)
            {
                result = result.Substring(dot + 1);
            }

            return result + arraySuffix;
        }

        private static bool TryParsePointerValue(string text, out long address)
        {
            address = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return long.TryParse(
                    text.Substring(2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out address);
            }

            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out address);
        }

        private static int GetDefaultValidBitsForOpenVariable(RawPixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case RawPixelFormat.Mono16:
                    return 16;
                case RawPixelFormat.Float32:
                    return 32;
                case RawPixelFormat.Binary:
                    return 1;
                case RawPixelFormat.Mono10PackedLsb:
                    return 10;
                case RawPixelFormat.Mono12PackedLsb:
                    return 12;
                default:
                    return 8;
            }
        }
#pragma warning restore VSTHRD010

        private void MapThisType_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.VerifyAccess();
            var document = ImageList.SelectedItem as ImageDocument;
            if (document == null || !document.IsError || !document.HasMemberInventory)
            {
                return;
            }

            ShowTypeMappingDialog(document);
        }

        private void MapCandidate_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.VerifyAccess();
            var element = sender as FrameworkElement;
            var document = element == null ? null : element.DataContext as ImageDocument;
            if (document == null || !document.HasMemberInventory)
            {
                return;
            }

            ImageList.SelectedItem = document;
            ShowTypeMappingDialog(document);
            e.Handled = true;
        }

        private void EditAutomaticMapping_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.VerifyAccess();
            if (_activeDocument != null && _activeDocument.IsAutomaticInspection && _activeDocument.HasMemberInventory)
            {
                ShowTypeMappingDialog(_activeDocument);
            }
        }

        private void ShowTypeMappingDialog(ImageDocument document)
        {
            Dispatcher.VerifyAccess();
            var dialog = new TypeMappingDialog(
                document.MemberInventory!,
                document.SourceType,
                document.ItemAssemblyName,
                document.DebuggeeProcessId,
                document.SuggestedMappingMembers,
                document.IsAutomaticInspection
                    && document.ErrorMessage.StartsWith(
                        "Pixel format needs one explicit mapping",
                        StringComparison.Ordinal))
            {
                Owner = Window.GetWindow(this)
            };
            if (dialog.ShowDialog() == true)
            {
                DiagnosticsList.Items.Insert(0, "Info: type mapping saved for " + document.SourceType + ". Refreshing the current frame.");
                if (_dte != null)
                {
                    ScanLocals();
                }
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
            UpdateAutomaticInspectionPanel(document);
            CancelDiagnosis();
            DiagnosisPanel.Visibility = Visibility.Collapsed;
            CompactDiagnosisPanel.Visibility = Visibility.Collapsed;

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

        private void UpdateAutomaticInspectionPanel(ImageDocument? document)
        {
            if (AutomaticInspectionPanel == null)
            {
                return;
            }

            if (document == null || !document.IsAutomaticInspection)
            {
                AutomaticInspectionPanel.Visibility = Visibility.Collapsed;
                AutomaticInspectionConfidenceText.Text = string.Empty;
                AutomaticInspectionMembersText.Text = string.Empty;
                AutomaticInspectionValidationText.Text = string.Empty;
                return;
            }

            AutomaticInspectionPanel.Visibility = Visibility.Visible;
            AutomaticInspectionConfidenceText.Text = document.AutomaticConfidenceText;
            AutomaticInspectionMembersText.Text = document.AutomaticMembersSummary;
            AutomaticInspectionValidationText.Text = document.AutomaticValidationSummary;
            EditAutomaticMappingButton.Visibility = document.HasMemberInventory
                && (!document.IsError || document.AutomaticMappingRequired)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void RenderActiveDocument()
        {
            HideErrorPanel();
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
                ShowErrorPanel(_activeDocument);
                UpdatePerformanceText();
                UpdateStatus();
                return;
            }

            if (_activeDocument.IsSourceUnavailable)
            {
                OpenGlImageView.ClearImage();
                DiagnosticsList.Items.Add("Warning: " + _activeDocument.SourceUnavailableMessage);
                UpdatePerformanceText();
                UpdateStatus();
                return;
            }

            var diagnostics = _activeDocument.Source.Analyze();
            foreach (var diagnostic in diagnostics)
            {
                DiagnosticsList.Items.Add(diagnostic.ToString());
            }

            if (_activeDocument.IsPreview)
            {
                DiagnosticsList.Items.Insert(0, "Info: sampled preview is visible while full-resolution transfer continues.");
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
            catch (RawImageSourceUnavailableException ex)
            {
                OpenGlImageView_SourceUnavailable(
                    OpenGlImageView,
                    new RawOpenGlSourceUnavailableEventArgs(ex));
                return;
            }
            catch (Exception ex)
            {
                OpenGlImageView.ClearImage();
                UpdatePerformanceText();
                DiagnosticsList.Items.Add("Error: image display failed. " + ex.Message);
                UpdateStatus();
                return;
            }

            try
            {
                DrawHistogramIfReasonable(_activeDocument);
            }
            catch (RawImageSourceUnavailableException ex)
            {
                OpenGlImageView_SourceUnavailable(
                    OpenGlImageView,
                    new RawOpenGlSourceUnavailableEventArgs(ex));
            }
            UpdateStatus();
        }

        private void AddErrorDocument(
            string displayPath,
            string sourceType,
            string errorType,
            string errorMessage,
            string errorDetails,
            List<VisualizerMemberInventoryItem>? memberInventory = null,
            string? itemAssemblyName = null,
            int debuggeeProcessId = 0)
        {
            var document = ImageDocument.CreateError(
                displayPath,
                sourceType,
                errorType,
                errorMessage,
                errorDetails,
                ShouldDeleteSnapshotDirectoryOnDispose(displayPath),
                memberInventory,
                itemAssemblyName,
                debuggeeProcessId);
            _documents.Add(document);
            ActivateDocument(document);
        }

        private void ShowErrorPanel(ImageDocument document)
        {
            OpenGlImageView.Visibility = Visibility.Collapsed;
            ErrorIdText.Text = document.ErrorId;
            ErrorMessageText.Text = document.ErrorMessage;
            ErrorPanel.Visibility = Visibility.Visible;
        }

        private void HideErrorPanel()
        {
            if (ErrorPanel == null)
            {
                return;
            }

            ErrorPanel.Visibility = Visibility.Collapsed;
            OpenGlImageView.Visibility = Visibility.Visible;
            ErrorIdText.Text = string.Empty;
            ErrorMessageText.Text = string.Empty;
        }

        private void CopySupportReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(CreateActiveSupportReport());
                DiagnosticsList.Items.Insert(0, "Info: support report copied to the clipboard.");
                SetTransientStatus("Support report copied");
            }
            catch (Exception ex)
            {
                DiagnosticsList.Items.Insert(0, "Error: support report copy failed. " + ex.Message);
                SetTransientStatus("Report copy failed");
            }
        }

        private void OpenManagedArrayBuffer(
            byte[] buffer,
            RawImageDescriptor descriptor,
            string displayName,
            string sourceType,
            string handoffId)
        {
            var openWatch = Stopwatch.StartNew();
            var displayPath = "managed-array:" + displayName;
            var source = RawImageSource.FromMemory(buffer, descriptor);
            var existingDocument = FindHandoffDocument(handoffId);
            if (existingDocument != null)
            {
                var wasActive = ReferenceEquals(_activeDocument, existingDocument);
                existingDocument.ReplaceSource(
                    displayPath,
                    source,
                    descriptor,
                    displayName,
                    sourceType,
                    false);
                ImageList.Items.Refresh();
                if (wasActive)
                {
                    DescriptorText.Text = FormatDescriptor(existingDocument);
                    UpdateInterpretationControls(existingDocument.Descriptor);
                    RenderActiveDocument();
                }
            }
            else
            {
                var document = new ImageDocument(
                    displayPath,
                    source,
                    descriptor,
                    displayName,
                    sourceType,
                    false,
                    handoffId,
                    false);
                _documents.Add(document);
                if (!_automaticScanRunning)
                {
                    ActivateDocument(document);
                }
            }

            openWatch.Stop();
            _lastOpenPathMilliseconds = openWatch.Elapsed.TotalMilliseconds;
            DiagnosticsList.Items.Insert(0, string.Format(
                CultureInfo.InvariantCulture,
                "Info: managed debugger array copied safely, {0}, open {1:0.0} ms.",
                FormatByteCount(buffer.LongLength),
                openWatch.Elapsed.TotalMilliseconds));
        }

        private void OpenLiveMemory(VisualizerHandoffRequest request)
        {
            if (!request.IsLiveMemory || request.LiveDescriptor == null)
            {
                throw new InvalidDataException("Live debugger image metadata is incomplete.");
            }

            var openWatch = Stopwatch.StartNew();
            var displayPath = string.Format(
                CultureInfo.InvariantCulture,
                "debuggee-{0}-0x{1:X}",
                request.LiveProcessId,
                request.LiveBufferAddress);
            var source = RawImageSource.FromProcessMemory(
                request.LiveProcessId,
                request.LiveBufferAddress,
                request.LiveBufferLength,
                request.LiveDescriptor);
            var resolvedSourceType = string.IsNullOrWhiteSpace(request.SourceType)
                ? "Debugger live memory"
                : request.SourceType;
            var existingDocument = FindHandoffDocument(request.HandoffId);
            if (existingDocument != null)
            {
                var wasActive = ReferenceEquals(_activeDocument, existingDocument);
                existingDocument.ReplaceSource(
                    displayPath,
                    source,
                    request.LiveDescriptor,
                    request.DisplayName,
                    resolvedSourceType,
                    false);
                ImageList.Items.Refresh();
                if (wasActive)
                {
                    DescriptorText.Text = FormatDescriptor(existingDocument);
                    UpdateInterpretationControls(existingDocument.Descriptor);
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
            }
            else
            {
                var document = new ImageDocument(
                    displayPath,
                    source,
                    request.LiveDescriptor,
                    request.DisplayName,
                    resolvedSourceType,
                    false,
                    request.HandoffId,
                    false);
                _documents.Add(document);
                if (!_automaticScanRunning)
                {
                    ActivateDocument(document);
                }
            }

            openWatch.Stop();
            _lastOpenPathMilliseconds = openWatch.Elapsed.TotalMilliseconds;
            DiagnosticsList.Items.Insert(0, string.Format(
                CultureInfo.InvariantCulture,
                "Info: live debugger tiles ready, {0}, open {1:0.0} ms. Reopen after Continue.",
                FormatByteCount(request.LiveBufferLength),
                openWatch.Elapsed.TotalMilliseconds));
            UpdateTempUsageStatus();
        }

        private ImageDocument? FindHandoffDocument(string? handoffId)
        {
            if (string.IsNullOrWhiteSpace(handoffId))
            {
                return null;
            }

            return _documents.FirstOrDefault(document => string.Equals(
                document.HandoffId,
                handoffId,
                StringComparison.Ordinal));
        }

        private void OpenSupportLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var reportPath = WriteActiveSupportReportFile();
                var logDirectory = Path.GetDirectoryName(reportPath) ?? VisualStudioTempStore.RootDirectory;
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "/select,\"" + reportPath + "\"",
                    UseShellExecute = false
                });
                DiagnosticsList.Items.Insert(0, "Info: opened support logs at " + logDirectory);
                SetTransientStatus("Support logs opened");
            }
            catch (Exception ex)
            {
                DiagnosticsList.Items.Insert(0, "Error: support log folder failed. " + ex.Message);
                SetTransientStatus("Open logs failed");
            }
        }

        private string WriteActiveSupportReportFile()
        {
            var logDirectory = VisualStudioTempStore.RootDirectory;
            Directory.CreateDirectory(logDirectory);
            var reportPath = Path.Combine(logDirectory, "latest-error-report.txt");
            File.WriteAllText(reportPath, CreateActiveSupportReport(), new UTF8Encoding(false));
            return reportPath;
        }

        private string CreateActiveSupportReport()
        {
            var now = DateTime.UtcNow;
            var document = _activeDocument;
            var data = new VisualizerSupportReportData
            {
                ReportType = document != null && document.IsError ? "Visualization error" : "Diagnostics",
                ErrorId = document != null && document.IsError
                    ? document.ErrorId
                    : CreateSupportId("DIAG", now),
                TimestampUtc = document != null && document.IsError ? document.ErrorOccurredUtc : now,
                ExtensionVersion = GetExtensionVersion(),
                VisualStudioVersion = GetVisualStudioVersion(),
                OperatingSystem = Environment.OSVersion.VersionString,
                ProcessArchitecture = Environment.Is64BitProcess ? "x64" : "x86",
                SourceName = document == null ? string.Empty : document.Title,
                SourceType = document == null ? string.Empty : document.SourceType,
                ErrorType = document == null ? string.Empty : document.ErrorType,
                ErrorMessage = document == null ? string.Empty : document.ErrorMessage,
                ErrorDetails = document == null ? string.Empty : document.ErrorDetails,
                Descriptor = document == null || document.IsError ? string.Empty : FormatDescriptorForReport(document),
                DisplayPath = document == null ? string.Empty : document.DisplayPath,
                PackageLogPath = Path.Combine(VisualStudioTempStore.RootDirectory, "package.log"),
                ActivityLogPath = GetLatestActivityLogPath()
            };

            foreach (var diagnostic in DiagnosticsList.Items)
            {
                var text = Convert.ToString(diagnostic, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    data.Diagnostics.Add(text!);
                }
            }

            return VisualizerSupportReport.Create(data);
        }

        private static string FormatDescriptorForReport(ImageDocument document)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}x{1} {2}, stride {3}, valid bits {4}, {5}, {6:N0} bytes",
                document.Descriptor.Width,
                document.Descriptor.Height,
                document.Descriptor.PixelFormat,
                document.Descriptor.Stride,
                document.Descriptor.ValidBits,
                document.Descriptor.ByteOrder,
                document.Source.Length);
        }

        private static string CreateSupportId(string kind, DateTime utcNow)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "RBV-{0}-{1}-{2}",
                kind,
                utcNow.ToUniversalTime().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
                Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant());
        }

        private static string GetExtensionVersion()
        {
            try
            {
                var assemblyDirectory = Path.GetDirectoryName(typeof(RawBufferToolWindowControl).Assembly.Location);
                var extensionAssemblyPath = string.IsNullOrWhiteSpace(assemblyDirectory)
                    ? string.Empty
                    : Path.Combine(assemblyDirectory, "RawBufferVisualizer.VisualStudio.Extensibility.dll");
                if (File.Exists(extensionAssemblyPath))
                {
                    var fileVersion = FileVersionInfo.GetVersionInfo(extensionAssemblyPath).FileVersion;
                    if (!string.IsNullOrWhiteSpace(fileVersion))
                    {
                        return fileVersion;
                    }
                }
            }
            catch
            {
            }

            return typeof(RawBufferToolWindowControl).Assembly.GetName().Version?.ToString() ?? "Unknown";
        }

        private static string GetVisualStudioVersion()
        {
            try
            {
                using (var process = Process.GetCurrentProcess())
                {
                    var executablePath = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(executablePath))
                    {
                        return FileVersionInfo.GetVersionInfo(executablePath).FileVersion ?? "Unknown";
                    }
                }
            }
            catch
            {
            }

            return "Unknown";
        }

        private static string GetLatestActivityLogPath()
        {
            try
            {
                var root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft",
                    "VisualStudio");
                if (Directory.Exists(root))
                {
                    var latest = Directory.GetDirectories(root, "17.0_*")
                        .Select(directory => Path.Combine(directory, "ActivityLog.xml"))
                        .Where(File.Exists)
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(latest))
                    {
                        return latest!;
                    }
                }

                return Path.Combine(root, "17.0_*", "ActivityLog.xml");
            }
            catch
            {
                return "%APPDATA%\\Microsoft\\VisualStudio\\17.0_*\\ActivityLog.xml";
            }
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
            UpdateMapThisTypeMenuItem();
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

            var useCompact = ReferenceEquals(sender, CompactApplyInterpretationButton);

            try
            {
                var descriptor = _activeDocument.Descriptor.Clone();
                descriptor.PixelFormat = (RawPixelFormat)(useCompact ? CompactInterpretPixelFormatBox.SelectedItem : InterpretPixelFormatBox.SelectedItem);
                descriptor.ByteOrder = (RawByteOrder)(useCompact ? CompactInterpretByteOrderBox.SelectedItem : InterpretByteOrderBox.SelectedItem);
                descriptor.Width = ParsePositiveInt(useCompact ? CompactInterpretWidthTextBox.Text : InterpretWidthTextBox.Text, "Width");
                descriptor.Height = ParsePositiveInt(useCompact ? CompactInterpretHeightTextBox.Text : InterpretHeightTextBox.Text, "Height");
                descriptor.Stride = ParsePositiveInt(useCompact ? CompactInterpretStrideTextBox.Text : InterpretStrideTextBox.Text, "Stride");
                descriptor.ValidBits = ParsePositiveInt(useCompact ? CompactInterpretValidBitsTextBox.Text : InterpretValidBitsTextBox.Text, "Valid bits");
                ApplyInterpretationDescriptor(descriptor);
            }
            catch (Exception ex)
            {
                DiagnosticsList.Items.Add("Error: reinterpret failed. " + ex.Message);
            }
        }

        private void ApplyInterpretationDescriptor(RawImageDescriptor descriptor)
        {
            if (_activeDocument == null)
            {
                return;
            }

            var source = _activeDocument.Source.WithDescriptor(descriptor);
            _activeDocument.ReplaceSource(source, descriptor);
            ImageList.Items.Refresh();
            DescriptorText.Text = FormatDescriptor(_activeDocument);
            UpdateInterpretationControls(descriptor);
            RenderActiveDocument();
        }

        private void DiagnoseBuffer_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDocument == null || _activeDocument.IsError)
            {
                return;
            }

            var document = _activeDocument;
            CancelDiagnosis();
            var cancellation = new CancellationTokenSource();
            _diagnosisCancellation = cancellation;

            var useCompact = ReferenceEquals(sender, CompactDiagnoseBufferButton);
            if (useCompact)
            {
                CompactDiagnosisPanel.Visibility = Visibility.Visible;
                CompactDiagnosisStatusText.Text = "Diagnosing buffer interpretations...";
                CompactDiagnosisCandidateList.ItemsSource = null;
            }
            else
            {
                DiagnosisPanel.Visibility = Visibility.Visible;
                DiagnosisStatusText.Text = "Diagnosing buffer interpretations...";
                DiagnosisCandidateList.ItemsSource = null;
            }

            var source = document.Source;
#pragma warning disable VSTHRD110
            // VSTHRD110: the ContinueWith continuation observes every completion state
            // (RanToCompletion/Canceled/Faulted) on the UI thread scheduler, so no
            // awaitable result is dropped. This control is also hosted outside
            // Visual Studio, where JoinableTaskFactory is unavailable.
            Task.Run(
                delegate
                {
                    var diagnosed = BufferDoctor.Diagnose(source, cancellation.Token);
                    var rows = new List<DiagnosisCandidateItem>(diagnosed.Candidates.Count);
                    for (var i = 0; i < diagnosed.Candidates.Count; i++)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        rows.Add(new DiagnosisCandidateItem(diagnosed.Candidates[i], CreateCandidateThumbnail(source, diagnosed.Candidates[i])));
                    }

                    return new KeyValuePair<BufferDiagnosisResult, List<DiagnosisCandidateItem>>(diagnosed, rows);
                },
                cancellation.Token).ContinueWith(
                task => CompleteDiagnosis(task, document, cancellation),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.FromCurrentSynchronizationContext());
#pragma warning restore VSTHRD110
        }

        private void CompleteDiagnosis(
            Task<KeyValuePair<BufferDiagnosisResult, List<DiagnosisCandidateItem>>> task,
            ImageDocument document,
            CancellationTokenSource cancellation)
        {
            if (task.IsCanceled || cancellation.IsCancellationRequested || !ReferenceEquals(_activeDocument, document))
            {
                return;
            }

            if (task.IsFaulted)
            {
                var failure = task.Exception == null ? null : task.Exception.GetBaseException();
                var failureText = "Diagnosis failed: " + (failure == null ? "unknown error." : failure.Message);
                DiagnosisStatusText.Text = failureText;
                CompactDiagnosisStatusText.Text = failureText;
                return;
            }

#pragma warning disable VSTHRD002
            // VSTHRD002: this continuation only runs after the task completed, so
            // reading task.Result cannot block the UI thread.
            var result = task.Result.Key;
            var items = task.Result.Value;
#pragma warning restore VSTHRD002
            DiagnosisCandidateList.ItemsSource = items;
            CompactDiagnosisCandidateList.ItemsSource = items;
            var errorNotes = 0;
            for (var i = 0; i < result.Notes.Count; i++)
            {
                if (result.Notes[i].Severity == RawDiagnosticSeverity.Error)
                {
                    errorNotes++;
                }
            }

            var statusText = string.Format(
                CultureInfo.InvariantCulture,
                "Suggests {0} ranked candidate interpretation(s); it cannot detect the correct format automatically.{1} Select a row to apply it.",
                items.Count,
                errorNotes > 0 ? " Buffer diagnostics report " + errorNotes + " error(s)." : string.Empty);
            DiagnosisStatusText.Text = statusText;
            CompactDiagnosisStatusText.Text = statusText;
        }

        private void DiagnosisCandidateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            var item = listBox == null ? null : listBox.SelectedItem as DiagnosisCandidateItem;
            if (item == null || _activeDocument == null || _activeDocument.IsError)
            {
                return;
            }

            try
            {
                ApplyInterpretationDescriptor(item.Candidate.Descriptor);
            }
            catch (Exception ex)
            {
                DiagnosticsList.Items.Add("Error: candidate apply failed. " + ex.Message);
            }
        }

        private void CloseDiagnosis_Click(object sender, RoutedEventArgs e)
        {
            CancelDiagnosis();
            DiagnosisPanel.Visibility = Visibility.Collapsed;
            CompactDiagnosisPanel.Visibility = Visibility.Collapsed;
        }

        private void CancelDiagnosis()
        {
            if (_diagnosisCancellation != null)
            {
                _diagnosisCancellation.Cancel();
                _diagnosisCancellation.Dispose();
                _diagnosisCancellation = null;
            }
        }

        private static BitmapSource? CreateCandidateThumbnail(RawImageSource source, BufferInterpretationCandidate candidate)
        {
            try
            {
                using (var interpreted = source.WithDescriptor(candidate.Descriptor))
                {
                    return CreateThumbnailSource(interpreted, candidate.Descriptor);
                }
            }
            catch
            {
                return null;
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

        private void OpenGlImageView_SourceUnavailable(object? sender, RawOpenGlSourceUnavailableEventArgs e)
        {
            var document = _activeDocument;
            if (document == null || !document.MarkSourceUnavailable(e.Exception.Message))
            {
                return;
            }

            ImageList.Items.Refresh();
            DiagnosticsList.Items.Insert(
                0,
                "Warning: live debugger memory is unavailable. The last rendered image remains visible; pause at a valid breakpoint and open the visualizer again. "
                + e.Exception.Message);
            _lastHoverX = -1;
            _lastHoverY = -1;
            _pinnedInspectorX = -1;
            _pinnedInspectorY = -1;
            SetInspectorPinnedState(false);
            SetMarkerText(string.Empty);
            SetPixelDetails(string.Empty, string.Empty, string.Empty, string.Empty);
            ClearPixelStatus();
            UpdatePerformanceText();
            UpdateStatus();
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
            InterpretWidthTextBox.Text = descriptor.Width.ToString(CultureInfo.InvariantCulture);
            InterpretHeightTextBox.Text = descriptor.Height.ToString(CultureInfo.InvariantCulture);
            InterpretStrideTextBox.Text = descriptor.Stride.ToString(CultureInfo.InvariantCulture);
            InterpretValidBitsTextBox.Text = descriptor.ValidBits.ToString(CultureInfo.InvariantCulture);

            CompactInterpretPixelFormatBox.SelectedItem = descriptor.PixelFormat;
            CompactInterpretByteOrderBox.SelectedItem = descriptor.ByteOrder;
            CompactInterpretWidthTextBox.Text = descriptor.Width.ToString(CultureInfo.InvariantCulture);
            CompactInterpretHeightTextBox.Text = descriptor.Height.ToString(CultureInfo.InvariantCulture);
            CompactInterpretStrideTextBox.Text = descriptor.Stride.ToString(CultureInfo.InvariantCulture);
            CompactInterpretValidBitsTextBox.Text = descriptor.ValidBits.ToString(CultureInfo.InvariantCulture);
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

            if (_activeDocument.IsError)
            {
                StatusText.Text = "Error " + _activeDocument.ErrorId;
                WriteSessionStateIfRequested();
                return;
            }

            if (_activeDocument.IsSourceUnavailable)
            {
                StatusText.Text = "Live source unavailable";
                WriteSessionStateIfRequested();
                return;
            }

            StatusText.Text = string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1}x{2} {3} {4} {5} tiles",
                _activeDocument.IsPreview ? "Preview " : string.Empty,
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
            return source.IsLiveProcessBacked ? "live" : source.IsFileBacked ? "file" : "mem";
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
                AppendJsonProperty(builder, "activeErrorId", _activeDocument == null ? string.Empty : _activeDocument.ErrorId, true);
                AppendJsonProperty(builder, "activeSourceUnavailable", _activeDocument != null && _activeDocument.IsSourceUnavailable, true);
                AppendJsonProperty(builder, "errorPanelVisible", ErrorPanel != null && ErrorPanel.Visibility == Visibility.Visible, true);
                AppendJsonProperty(builder, "supportReportAvailable", _activeDocument != null && _activeDocument.IsError, true);
                AppendJsonProperty(builder, "autoInspectEnabled", IsAutoInspectEnabled, true);
                AppendJsonProperty(
                    builder,
                    "automaticScanStatus",
                    AutomaticScanStatusText == null ? string.Empty : AutomaticScanStatusText.Text,
                    true);
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
                    AppendJsonProperty(builder, "isAutomaticInspection", document.IsAutomaticInspection, true, 6);
                    AppendJsonProperty(builder, "automaticMappingRequired", document.AutomaticMappingRequired, true, 6);
                    AppendJsonProperty(builder, "isSourceUnavailable", document.IsSourceUnavailable, true, 6);
                    AppendJsonProperty(builder, "hasThumbnail", document.Thumbnail != null, true, 6);
                    AppendJsonProperty(builder, "errorId", document.ErrorId, true, 6);
                    AppendJsonProperty(builder, "errorType", document.ErrorType, true, 6);
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
#pragma warning disable VSTHRD001, VSTHRD110
            // VSTHRD001/VSTHRD110: fire-and-forget scheduling of the automation probe at
            // ContextIdle priority is intentional. The probe is diagnostics-only, the returned
            // DispatcherOperation has no result to observe, and JoinableTaskFactory cannot be
            // used because this control is also hosted outside Visual Studio.
            Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() => RunAutomationProbe(outputPath, metadataPath)));
#pragma warning restore VSTHRD001, VSTHRD110
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

        internal static BitmapSource? CreateThumbnailSource(RawImageSource source, RawImageDescriptor descriptor)
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
                    "Type       Error\nError ID   {0}\nSource     {1}\nReason     {2}\nFile       {3}",
                    document.ErrorId,
                    document.SourceType,
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

        private sealed class DiagnosisCandidateItem
        {
            public BufferInterpretationCandidate Candidate { get; private set; }
            public BitmapSource? Thumbnail { get; private set; }
            public string Title { get; private set; }
            public string Summary { get; private set; }
            public string ScoreText { get; private set; }
            public string AmbiguityNote { get; private set; }
            public string ReasonsText { get; private set; }

            public Visibility AmbiguityVisibility
            {
                get { return string.IsNullOrEmpty(AmbiguityNote) ? Visibility.Collapsed : Visibility.Visible; }
            }

            public DiagnosisCandidateItem(BufferInterpretationCandidate candidate, BitmapSource? thumbnail)
            {
                Candidate = candidate;
                Thumbnail = thumbnail;
                var descriptor = candidate.Descriptor;
                var padding = descriptor.Stride - descriptor.GetMinimumStride();
                Title = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}  {1} x {2}",
                    descriptor.PixelFormat,
                    descriptor.Width,
                    descriptor.Height);
                Summary = string.Format(
                    CultureInfo.InvariantCulture,
                    "stride {0}{1}, {2} bits, {3}",
                    descriptor.Stride,
                    padding > 0 ? " (+" + padding + " pad/row)" : string.Empty,
                    descriptor.ValidBits,
                    descriptor.ByteOrder == RawByteOrder.LittleEndian ? "LE" : "BE");
                ScoreText = candidate.Score.ToString(CultureInfo.InvariantCulture);
                AmbiguityNote = candidate.IsAmbiguousWithGroup
                    ? "Tied group: cannot be distinguished from buffer content."
                    : string.Empty;
                ReasonsText = candidate.Reasons.Count == 0
                    ? "No scoring reasons."
                    : string.Join("\n", candidate.Reasons);
            }
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
            public string ErrorType { get; private set; }
            public string ErrorDetails { get; private set; }
            public string ErrorId { get; private set; }
            public DateTime ErrorOccurredUtc { get; private set; }
            public string HandoffId { get; private set; }
            public bool IsPreview { get; private set; }
            public string SourceUnavailableMessage { get; private set; }
            public List<VisualizerMemberInventoryItem>? MemberInventory { get; private set; }
            public string ItemAssemblyName { get; private set; }
            public int DebuggeeProcessId { get; private set; }
            public bool IsAutomaticInspection { get; private set; }
            public int AutomaticConfidence { get; private set; }
            public string AutomaticMembersSummary { get; private set; }
            public string AutomaticValidationSummary { get; private set; }
            public bool AutomaticMappingRequired { get; private set; }
            public TypeMappingMembers? SuggestedMappingMembers { get; private set; }

            public Visibility MappingActionVisibility
            {
                get
                {
                    return IsAutomaticInspection
                        && IsError
                        && HasMemberInventory
                        && AutomaticMappingRequired
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }

            public Visibility AutomaticInspectionVisibility
            {
                get { return IsAutomaticInspection ? Visibility.Visible : Visibility.Collapsed; }
            }

            public string AutomaticConfidenceText
            {
                get
                {
                    return IsAutomaticInspection
                        ? "Confidence " + AutomaticConfidence.ToString(CultureInfo.InvariantCulture) + "%"
                        : string.Empty;
                }
            }

            public bool HasMemberInventory
            {
                get { return MemberInventory != null && MemberInventory.Count > 0; }
            }
            private readonly string? _ownedSnapshotDirectory;
            private bool _disposed;

            public void SetAutomaticInspection(
                int confidence,
                string membersSummary,
                string validationSummary,
                List<VisualizerMemberInventoryItem> memberInventory,
                string itemAssemblyName,
                int debuggeeProcessId,
                bool mappingRequired,
                TypeMappingMembers? suggestedMappingMembers = null)
            {
                IsAutomaticInspection = true;
                AutomaticConfidence = Math.Max(0, Math.Min(100, confidence));
                AutomaticMembersSummary = membersSummary ?? string.Empty;
                AutomaticValidationSummary = validationSummary ?? string.Empty;
                MemberInventory = memberInventory;
                ItemAssemblyName = itemAssemblyName ?? string.Empty;
                DebuggeeProcessId = debuggeeProcessId;
                AutomaticMappingRequired = mappingRequired;
                SuggestedMappingMembers = suggestedMappingMembers;
                var cleanTitle = Title.StartsWith("Open failed: ", StringComparison.Ordinal)
                    ? Title.Substring("Open failed: ".Length)
                    : Title.Trim();
                Title = IsError
                    ? (mappingRequired ? "[Map] " : "[Failed] ") + cleanTitle
                    : "[Auto] " + cleanTitle;
            }

            public bool IsError
            {
                get { return !string.IsNullOrWhiteSpace(ErrorMessage); }
            }

            public bool IsSourceUnavailable
            {
                get { return !string.IsNullOrWhiteSpace(SourceUnavailableMessage); }
            }

            public string Summary
            {
                get
                {
                    if (IsError)
                    {
                        return IsAutomaticInspection
                            ? AutomaticConfidenceText + "  " + ErrorMessage
                            : "Error  " + ErrorMessage;
                    }

                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}{1}{2}{3} x {4}  {5}  stride {6}  {7}",
                        IsAutomaticInspection ? AutomaticConfidenceText + "  " : string.Empty,
                        IsSourceUnavailable ? "Unavailable  " : string.Empty,
                        IsPreview ? "Preview  " : string.Empty,
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
                bool deleteSnapshotDirectoryOnDispose = false,
                string? handoffId = null,
                bool isPreview = false)
            {
                DisplayPath = GetDisplayPath(displayPath);
                Title = string.IsNullOrWhiteSpace(title) ? CreateTitle(DisplayPath) : title!.Trim();
                SourceType = string.IsNullOrWhiteSpace(sourceType) ? "Unknown" : sourceType;
                Source = source ?? throw new ArgumentNullException("source");
                Descriptor = descriptor == null ? throw new ArgumentNullException("descriptor") : descriptor.Clone();
                Thumbnail = CreateThumbnailSource(Source, Descriptor);
                ErrorMessage = string.Empty;
                ErrorType = string.Empty;
                ErrorDetails = string.Empty;
                ErrorId = string.Empty;
                ErrorOccurredUtc = DateTime.MinValue;
                HandoffId = handoffId ?? string.Empty;
                IsPreview = isPreview;
                SourceUnavailableMessage = string.Empty;
                MemberInventory = null;
                ItemAssemblyName = string.Empty;
                DebuggeeProcessId = 0;
                IsAutomaticInspection = false;
                AutomaticConfidence = 0;
                AutomaticMembersSummary = string.Empty;
                AutomaticValidationSummary = string.Empty;
                AutomaticMappingRequired = false;
                SuggestedMappingMembers = null;
                _ownedSnapshotDirectory = GetOwnedSnapshotDirectory(DisplayPath, deleteSnapshotDirectoryOnDispose);
            }

            private ImageDocument(
                string displayPath,
                string sourceType,
                string errorType,
                string errorMessage,
                string errorDetails,
                bool deleteSnapshotDirectoryOnDispose,
                List<VisualizerMemberInventoryItem>? memberInventory,
                string? itemAssemblyName,
                int debuggeeProcessId)
            {
                DisplayPath = GetDisplayPath(displayPath);
                Title = "Open failed: " + CreateTitle(DisplayPath);
                SourceType = string.IsNullOrWhiteSpace(sourceType) ? "Unknown" : sourceType;
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
                ErrorType = string.IsNullOrWhiteSpace(errorType) ? "Unknown" : errorType;
                ErrorDetails = errorDetails ?? string.Empty;
                ErrorOccurredUtc = DateTime.UtcNow;
                ErrorId = CreateSupportId("ERROR", ErrorOccurredUtc);
                HandoffId = string.Empty;
                IsPreview = false;
                SourceUnavailableMessage = string.Empty;
                MemberInventory = memberInventory;
                ItemAssemblyName = itemAssemblyName ?? string.Empty;
                DebuggeeProcessId = debuggeeProcessId;
                IsAutomaticInspection = false;
                AutomaticConfidence = 0;
                AutomaticMembersSummary = string.Empty;
                AutomaticValidationSummary = string.Empty;
                AutomaticMappingRequired = false;
                SuggestedMappingMembers = null;
                _ownedSnapshotDirectory = GetOwnedSnapshotDirectory(DisplayPath, deleteSnapshotDirectoryOnDispose);
            }

            public static ImageDocument CreateError(
                string displayPath,
                string sourceType,
                string errorType,
                string errorMessage,
                string errorDetails,
                bool deleteSnapshotDirectoryOnDispose = false,
                List<VisualizerMemberInventoryItem>? memberInventory = null,
                string? itemAssemblyName = null,
                int debuggeeProcessId = 0)
            {
                return new ImageDocument(
                    displayPath,
                    sourceType,
                    errorType,
                    errorMessage,
                    errorDetails,
                    deleteSnapshotDirectoryOnDispose,
                    memberInventory,
                    itemAssemblyName,
                    debuggeeProcessId);
            }

            public void ReplaceSource(RawImageSource source, RawImageDescriptor descriptor)
            {
                ReplaceSource(DisplayPath, source, descriptor, Title, SourceType, false);
            }

            public void ReplaceSource(
                string displayPath,
                RawImageSource source,
                RawImageDescriptor descriptor,
                string? title,
                string sourceType,
                bool isPreview)
            {
                if (source == null)
                {
                    throw new ArgumentNullException("source");
                }

                var previousSource = Source;
                DisplayPath = GetDisplayPath(displayPath);
                Title = string.IsNullOrWhiteSpace(title) ? CreateTitle(DisplayPath) : title!.Trim();
                SourceType = string.IsNullOrWhiteSpace(sourceType) ? "Unknown" : sourceType;
                Source = source;
                Descriptor = descriptor == null ? throw new ArgumentNullException("descriptor") : descriptor.Clone();
                Thumbnail = CreateThumbnailSource(Source, Descriptor);
                ViewState = null;
                IsPreview = isPreview;
                ErrorMessage = string.Empty;
                ErrorType = string.Empty;
                ErrorDetails = string.Empty;
                ErrorId = string.Empty;
                ErrorOccurredUtc = DateTime.MinValue;
                SourceUnavailableMessage = string.Empty;
                if (!ReferenceEquals(previousSource, source))
                {
                    previousSource.Dispose();
                }
            }

            public bool MarkSourceUnavailable(string message)
            {
                if (IsSourceUnavailable)
                {
                    return false;
                }

                SourceUnavailableMessage = string.IsNullOrWhiteSpace(message)
                    ? "Live debugger image memory is no longer readable."
                    : message;
                return true;
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
