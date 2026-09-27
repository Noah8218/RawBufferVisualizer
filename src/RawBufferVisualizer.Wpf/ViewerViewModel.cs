using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.OpenGlCanvas;
using RawBufferVisualizer.Presentation;
using RawBufferVisualizer.Sdk;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("RawBufferVisualizer.Tests")]

namespace RawBufferVisualizer.Wpf
{
    internal sealed class ViewerViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IViewerCanvas _canvas;
        private readonly IViewerDialogHost _dialogs;
        private readonly SnapshotExportOperation _export = new SnapshotExportOperation();
        private readonly ViewCommand[] _commands;
        private ViewerDocument? _selected;
        private RawOpenGlViewState? _linkedViewState;
        private bool _busy, _disposed, _switching, _hasPreview, _linkViews;

        public ViewerViewModel(IViewerCanvas canvas, IViewerDialogHost dialogs)
        {
            _canvas = canvas;
            _dialogs = dialogs;
            OpenCommand = new ViewCommand(value => Open(value as string), () => !_disposed && !_busy);
            OpenExampleCommand = new ViewCommand(_ => OpenExample(), () => !_disposed && !_busy);
            CloseCommand = new ViewCommand(value => CloseDocument(value as ViewerDocument ?? _selected), () => !_disposed && !_busy && _selected != null);
            DuplicateCommand = new ViewCommand(_ => Duplicate(), () => !_disposed && !_busy && _selected != null);
            SavePngCommand = new ViewCommand(_ => SavePng(), () => !_disposed && !_busy && _selected?.Rendered != null);
            SaveSnapshotCommand = new ViewCommand(_ => ExportCompletion = SaveSnapshotAsync(), () => !_disposed && !_busy && _selected != null);
            FitCommand = new ViewCommand(_ => _canvas.Fit(), () => !_disposed && HasImage);
            ActualSizeCommand = new ViewCommand(_ => _canvas.Zoom(1), () => !_disposed && HasImage);
            _commands = new[] { OpenCommand, OpenExampleCommand, CloseCommand, DuplicateCommand, SavePngCommand, SaveSnapshotCommand, FitCommand, ActualSizeCommand }.Cast<ViewCommand>().ToArray();
            _canvas.ViewChanged += OnViewChanged;
            _canvas.PixelHovered += OnPixelHovered;
            Histogram.PropertyChanged += OnHistogramChanged;
            Histogram.SetVisible(true);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public ObservableCollection<ViewerDocument> Documents { get; } = new ObservableCollection<ViewerDocument>();
        public ObservableCollection<string> Diagnostics { get; } = new ObservableCollection<string>();
        public HistogramViewModel Histogram { get; } = new HistogramViewModel();
        public Task ExportCompletion { get; private set; } = Task.CompletedTask;
        public ViewerDocument? SelectedDocument
        {
            get => _selected;
            set
            {
                if (value == null || !Documents.Contains(value) || ReferenceEquals(value, _selected) || _disposed) return;
                SaveViewState();
                _selected = value;
                RenderSelected();
            }
        }
        public bool HasDocuments => Documents.Count > 0;
        public bool IsEmpty => !HasDocuments;
        public bool HasImage => _hasPreview && _selected != null;
        public bool LinkViews
        {
            get => _linkViews;
            set { _linkViews = value; if (value && HasImage) _linkedViewState = _canvas.GetViewState(); Notify(); }
        }
        public double ZoomScale
        {
            get => HasImage ? Math.Max(0.01, Math.Min(16, _canvas.ZoomScale)) : 1;
            set { if (HasImage && Math.Abs(value - ZoomScale) > 0.000001) _canvas.Zoom(value); }
        }
        public string ZoomText => HasImage ? (_canvas.ZoomScale * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%" : "—";
        public string WidthText => _selected?.Descriptor.Width.ToString(CultureInfo.InvariantCulture) ?? "—";
        public string HeightText => _selected?.Descriptor.Height.ToString(CultureInfo.InvariantCulture) ?? "—";
        public string StrideText => _selected?.Descriptor.Stride.ToString(CultureInfo.InvariantCulture) ?? "—";
        public string FormatText => _selected?.Descriptor.PixelFormat.ToString() ?? "—";
        public string ValidBitsText => _selected?.Descriptor.ValidBits.ToString(CultureInfo.InvariantCulture) ?? "—";
        public string ByteOrderText => _selected?.Descriptor.ByteOrder.ToString() ?? "—";
        public string FileText => _selected?.DisplayPath ?? "No file open";
        public string PixelText { get; private set; } = "Move over an image to inspect a pixel.";
        public string DiagnosticSummary { get; private set; } = "No image to inspect";
        public string ImageStatus => _selected == null ? "No image open" : string.Format(CultureInfo.CurrentCulture,
            "{0} × {1} · {2} · {3:N0} bytes · {4:N0} tiles", _selected.Descriptor.Width, _selected.Descriptor.Height, _selected.Descriptor.PixelFormat, _selected.Source.Length, _canvas.TileCount);
        public string ActionStatus { get; private set; } = "Open a snapshot (.rbuf.json with its RAW file), or RAW data (.raw / .bin).";
        public string PngAvailability => _selected != null && _selected.Rendered == null
            ? "PNG unavailable for this image. Export Snapshot preserves the original data; large images use tiled display without a full PNG preview." : string.Empty;

        public ICommand OpenCommand { get; }
        public ICommand OpenExampleCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand DuplicateCommand { get; }
        public ICommand SavePngCommand { get; }
        public ICommand SaveSnapshotCommand { get; }
        public ICommand FitCommand { get; }
        public ICommand ActualSizeCommand { get; }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _canvas.ViewChanged -= OnViewChanged;
            _canvas.PixelHovered -= OnPixelHovered;
            Histogram.PropertyChanged -= OnHistogramChanged;
            Histogram.Dispose();
            _export.Dispose();
            _canvas.Clear();
            foreach (var document in Documents) document.Dispose();
            Documents.Clear();
            _selected = null;
            _hasPreview = false;
            Notify();
        }

        private void Open(string? path)
        {
            SetBusy(true);
            try
            {
                path = path ?? _dialogs.ChooseOpenPath();
                if (path == null) { SetStatus("Open cancelled. Current image kept."); return; }
                var fullPath = Path.GetFullPath(path);
                var existing = Documents.FirstOrDefault(document => document.MatchesPath(fullPath));
                if (existing != null) { SelectedDocument = existing; SetStatus("Already open — selected: " + existing.DisplayPath); return; }
                var metadata = fullPath.EndsWith(".rbuf.json", StringComparison.OrdinalIgnoreCase);
                var extension = Path.GetExtension(fullPath);
                if (!metadata && !extension.Equals(".raw", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".bin", StringComparison.OrdinalIgnoreCase))
                    throw new NotSupportedException("Open .rbuf.json snapshots or .raw / .bin data. PNG/JPEG images are not RAW input.");
                RawImageDescriptor? descriptor = null;
                if (!metadata)
                {
                    descriptor = _dialogs.ConfigureRaw(fullPath, ViewerFiles.GetLength(fullPath));
                    if (descriptor == null) { SetStatus("RAW setup cancelled. Current image kept."); return; }
                }
                if (_disposed) return;
                var document = ViewerFiles.Open(fullPath, descriptor);
                Documents.Add(document);
                SelectedDocument = document;
                SetStatus("Opened: " + fullPath);
            }
            catch (Exception ex)
            {
                SetStatus("Open failed. Current image kept. " + ex.Message
                    + (path?.EndsWith(".rbuf.json", StringComparison.OrdinalIgnoreCase) == true
                        ? " A snapshot needs both its .rbuf.json and the RAW file named inside it. Restore that RAW file to its recorded location, then reopen the metadata." : string.Empty));
            }
            finally { SetBusy(false); }
        }

        private void OpenExample()
        {
            try
            {
                var document = Documents.FirstOrDefault(item => item.IsExample);
                if (document == null) { document = ViewerFiles.CreateExample(); Documents.Add(document); }
                SelectedDocument = document;
                SetStatus("Example opened: Mono8 gradient, 640 × 480, stride 640 bytes. Try Fit, 1:1, pixel inspection and Histogram. No file was created.");
            }
            catch (Exception ex) { SetStatus("Example could not be opened. " + ex.Message); }
        }

        private void Duplicate()
        {
            if (_selected == null) return;
            try
            {
                var source = _selected.Source.WithDescriptor(_selected.Descriptor);
                var document = new ViewerDocument(_selected.IsExample ? null : _selected.DisplayPath, source);
                Documents.Add(document);
                SelectedDocument = document;
                SetStatus("Opened an independent view of the same data. Zoom and pan can differ; Link views shares the view for matching dimensions.");
            }
            catch (Exception ex) { SetStatus("Open copy failed. " + ex.Message); }
        }

        private void CloseDocument(ViewerDocument? document)
        {
            if (document == null) return;
            var index = Documents.IndexOf(document);
            if (index < 0) return;
            var active = ReferenceEquals(document, _selected);
            if (active)
            {
                Histogram.SetSource(null);
                _canvas.Clear();
                _selected = null;
                _hasPreview = false;
            }
            Documents.Remove(document);
            document.Dispose();
            if (active && Documents.Count > 0) SelectedDocument = Documents[Math.Min(index, Documents.Count - 1)];
            else if (Documents.Count == 0)
            {
                _linkedViewState = null;
                Diagnostics.Clear();
                DiagnosticSummary = "No image to inspect";
                PixelText = "Move over an image to inspect a pixel.";
            }
            SetStatus("Closed: " + document.Title + (Documents.Count == 0 ? ". Open a file or the example to continue." : string.Empty));
        }

        private void RenderSelected()
        {
            _switching = true;
            _hasPreview = false;
            Histogram.SetSource(null);
            Diagnostics.Clear();
            PixelText = "Move over the image to inspect a pixel.";
            var document = _selected!;
            try
            {
                if (RawBufferDiagnostics.HasErrors(document.Source.Analyze())) throw new InvalidDataException("The buffer layout is invalid. Check the diagnostics below.");
                var state = _linkViews && _linkedViewState?.Matches(document.Descriptor.Width, document.Descriptor.Height) == true ? _linkedViewState : document.ViewState;
                _canvas.Load(document.Source, state);
                _hasPreview = true;
                document.ReadError = null;
                if (document.Source.IsFileBacked || RawImageTilePlanner.EstimateBgraByteCount(document.Descriptor) > ViewerFiles.PreviewLimit)
                {
                    document.Rendered = null;
                }
                else if (document.Rendered == null)
                    document.Rendered = document.Source.RenderTile(0, 0, document.Descriptor.Width, document.Descriptor.Height, null);
            }
            catch (Exception ex)
            {
                document.ReadError = ex.Message;
                document.Rendered = null;
                _hasPreview = false;
                _canvas.Clear();
            }
            finally
            {
                RefreshDiagnostics(document);
                // Keep the existing Refresh action available when a file needs repair.
                if (_hasPreview || (document.Source.IsFileBacked && !document.Source.IsLiveProcessBacked))
                    Histogram.SetSource(document.Source, document.AcquireRead);
                _switching = false;
                Notify();
            }
        }

        private void RefreshDiagnostics(ViewerDocument document)
        {
            if (!ReferenceEquals(document, _selected)) return;
            var diagnostics = document.Source.Analyze();
            Diagnostics.Clear();
            foreach (var diagnostic in diagnostics) Diagnostics.Add(diagnostic.ToString());
            var failed = document.ReadError != null || RawBufferDiagnostics.HasErrors(diagnostics);
            DiagnosticSummary = failed ? "Needs attention — image read failed"
                : diagnostics.Any(item => item.Severity == RawDiagnosticSeverity.Warning) ? "Check image settings — warnings below" : "OK — buffer layout is valid";
            if (document.ReadError != null) Diagnostics.Add("Error: " + document.ReadError);
            if (failed && document.Source.IsFileBacked && !document.Source.IsLiveProcessBacked)
                Diagnostics.Add("Restore the RAW file, then click Histogram Refresh to reload. The previous preview may be out of date.");
            else if (document.Source.IsFileBacked)
                Diagnostics.Add("Info: tiled display is active. Export Snapshot saves the original buffer; PNG requires a full preview within the memory limit.");
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DiagnosticSummary)));
        }

        private void OnHistogramChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_disposed || _switching || _selected == null || Histogram.IsRunning || (Histogram.Error == null && Histogram.Result == null)) return;
            var recovering = _selected.ReadError != null || !_hasPreview;
            _selected.ReadError = Histogram.Error;
            if (recovering && Histogram.Result != null) RenderSelected();
            else RefreshDiagnostics(_selected);
        }

        private void SavePng()
        {
            var document = _selected;
            if (document?.Rendered == null) return;
            SetBusy(true);
            try
            {
                var path = _dialogs.ChooseSavePath(ExportName(document, ".png"), false);
                if (path == null) { SetStatus("PNG export cancelled. No output was changed."); return; }
                ViewerFiles.SavePng(path, document.Rendered);
                SetStatus("PNG saved (displayed image): " + Path.GetFullPath(path));
            }
            catch (Exception ex) { SetStatus("PNG export failed. " + ex.Message); }
            finally { SetBusy(false); }
        }

        private async Task SaveSnapshotAsync()
        {
            var document = _selected;
            if (document == null) return;
            SetBusy(true);
            try
            {
                var path = _dialogs.ChooseSavePath(ExportName(document, ".rbuf.json"), true);
                if (path == null || _disposed) { SetStatus("Snapshot export cancelled. No output was changed."); return; }
                using (document.AcquireRead())
                {
                    var model = new SnapshotExportViewModel(_export, path, document.Source, document.Descriptor);
                    var result = await _dialogs.ExportSnapshot(model);
                    if (_disposed) return;
                    if (result == null) { SetStatus("Snapshot export cancelled. No new snapshot was committed."); return; }
                    if (Documents.Contains(document)) document.SetPath(result.MetadataPath);
                    SetStatus("Snapshot saved. Reopen the metadata; keep both files together."
                        + Environment.NewLine + "Metadata: " + result.MetadataPath + Environment.NewLine + "RAW data: " + result.RawPath);
                }
            }
            catch (Exception ex)
            {
                if (!_disposed) { RefreshDiagnostics(document); SetStatus("Snapshot export failed. " + ex.Message); }
            }
            finally { SetBusy(false); }
        }

        private static string ExportName(ViewerDocument document, string extension)
        {
            var name = document.IsExample ? "mono8-example" : document.DisplayPath.EndsWith(".rbuf.json", StringComparison.OrdinalIgnoreCase)
                ? document.Title : Path.GetFileNameWithoutExtension(document.Title);
            return name + extension;
        }
        private void OnViewChanged(object? sender, EventArgs e) { if (!_switching) SaveViewState(); Notify(); }
        private void SaveViewState()
        {
            if (!HasImage || _switching) return;
            _selected!.ViewState = _canvas.GetViewState();
            if (_linkViews) _linkedViewState = _selected.ViewState;
        }
        private void OnPixelHovered(object? sender, RawOpenGlPixelEventArgs e)
        {
            try { PixelText = HasImage && e.X >= 0 && e.Y >= 0 ? _selected!.Source.DescribePixel(e.X, e.Y) : "Move over the image to inspect a pixel."; }
            catch (Exception ex)
            {
                PixelText = "Pixel unavailable: " + ex.Message;
                if (_selected != null) { _selected.ReadError = ex.Message; RefreshDiagnostics(_selected); }
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PixelText)));
        }
        private void SetStatus(string text) { ActionStatus = text; Notify(); }
        private void SetBusy(bool value) { _busy = value; Notify(); }
        private void Notify()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            foreach (var command in _commands) command.Refresh();
        }
    }
}
