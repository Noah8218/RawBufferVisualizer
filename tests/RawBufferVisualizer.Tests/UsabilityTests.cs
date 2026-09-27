using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.OpenGlCanvas;
using RawBufferVisualizer.Presentation;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.Wpf;
using RawBufferVisualizer.VisualStudio.Vssdk;

namespace RawBufferVisualizer.Tests
{
    internal static class UsabilityTests
    {
        private static int _checks;

        public static void RunAll()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                    var root = Path.Combine(Path.GetTempPath(), "usability-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(root);
                    RawInput();
                    DocumentsAndExports(root);
                    BorrowedLifetime(root);
                    FileReadRecovery(root);
                    InspectionStatus();
                    Console.WriteLine($"Usability: {_checks} assertions passed. Evidence inputs: {root}");
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw new InvalidOperationException("Usability regression failed.", failure);
        }

        private static void RawInput()
        {
            var model = new RawImportViewModel("input.raw", 640 * 480);
            Check(model.Width == "" && model.Height == "" && !model.ConfirmCommand.CanExecute(null), "RAW starts unconfirmed without inherited dimensions");
            model.Width = "640"; model.Height = "480";
            Check(model.Descriptor?.Stride == 640 && model.Descriptor.PixelFormat == RawPixelFormat.Mono8, "Blank stride means minimum packed stride");
            model.Stride = "640";
            Check(model.Descriptor?.GetRequiredByteCount() == 307200, "Explicit Mono8 settings fit exact bytes");
            model.ValidBits = "16"; Check(model.Descriptor == null, "Eight-bit formats reject misleading valid bits"); model.ValidBits = "8";
            model.Width = "2147483648";
            Check(model.Descriptor == null, "Integer overflow is rejected");
            model.Width = "-1"; Check(model.Descriptor == null, "Negative width is rejected");
            model.Width = "640"; model.Stride = "639";
            Check(model.Descriptor == null && model.ValidationMessage.Contains("Stride"), "Short stride is rejected");
            model.Stride = "abc"; Check(model.Descriptor == null, "Nonnumeric stride is rejected");
            model.Stride = "640"; model.Height = "481";
            Check(model.Descriptor == null, "Short input cannot be admitted");
            model.Height = "479";
            Check(model.Descriptor != null && model.ValidationMessage.Contains("trailing"), "Trailing bytes are explicitly disclosed");
            model.Width = "320"; model.Height = "240"; model.Stride = ""; model.Format = RawPixelFormat.Mono16;
            Check(model.ValidBits == "16" && model.Descriptor?.Stride == 640, "Format supplies matching bit and automatic stride defaults");
            model.ValidBits = "17"; Check(model.Descriptor == null, "Invalid Mono16 bit count is rejected");
            model.ValidBits = "12"; model.ByteOrder = RawByteOrder.BigEndian;
            Check(model.Descriptor?.ByteOrder == RawByteOrder.BigEndian && model.Descriptor.ValidBits == 12, "Explicit valid bits and endian are preserved");
            bool? closed = null; model.CloseRequested += result => closed = result;
            model.ConfirmCommand.Execute(null); Check(closed == true, "Confirmation returns success");
            model.CancelCommand.Execute(null); Check(closed == false, "Cancel is distinct from confirmation");
            model.Format = RawPixelFormat.Binary;
            Check(model.ValidBits == "1" && model.Descriptor?.ValidBits == 1, "Binary setup preserves the existing one-valid-bit convention");
        }

        private static void DocumentsAndExports(string root)
        {
            var mono = new RawImageDescriptor { Width = 640, Height = 480, Stride = 640, PixelFormat = RawPixelFormat.Mono8, ValidBits = 8 };
            var rgb = new RawImageDescriptor { Width = 320, Height = 240, Stride = 960, PixelFormat = RawPixelFormat.RGB24, ValidBits = 8 };
            var bytes = Enumerable.Range(0, 640 * 480).Select(value => (byte)(value % 256)).ToArray();
            var rawPath = Path.Combine(root, "image.raw"); File.WriteAllBytes(rawPath, bytes);
            var metadata = Path.Combine(root, "image.rbuf.json"); RawBufferSnapshot.Save(metadata, bytes, mono);
            var rgbPath = Path.Combine(root, "rgb.rbuf.json"); RawBufferSnapshot.Save(rgbPath, new byte[320 * 240 * 3], rgb);
            var canvas = new TestCanvas(); var dialogs = new TestDialogs();
            using (var model = new ViewerViewModel(canvas, dialogs))
            {
                model.Histogram.SetVisible(false);
                Check(model.IsEmpty && model.ZoomText == "—" && model.WidthText == "—", "Empty descriptor and zoom are placeholders");
                Check(!model.SavePngCommand.CanExecute(null) && !model.SaveSnapshotCommand.CanExecute(null) && !model.FitCommand.CanExecute(null) && !model.CloseCommand.CanExecute(null), "Unavailable commands are disabled");
                model.OpenCommand.Execute(rgbPath); var previous = model.SelectedDocument;
                dialogs.RawSettings = null; model.OpenCommand.Execute(rawPath);
                Check(model.Documents.Count == 1 && model.SelectedDocument == previous && model.ActionStatus.Contains("cancelled"), "RAW cancel keeps previous selection");
                dialogs.RawSettings = new RawImageDescriptor { Width = 640, Height = 481, Stride = 640, PixelFormat = RawPixelFormat.Mono8, ValidBits = 8 };
                model.OpenCommand.Execute(rawPath);
                Check(model.Documents.Count == 1 && model.SelectedDocument == previous && model.ActionStatus.Contains("failed"), "Invalid confirmed descriptor is revalidated at file boundary");
                dialogs.RawSettings = mono; model.OpenCommand.Execute(rawPath); var raw = model.SelectedDocument!;
                Check(raw.Descriptor.Width == 640 && raw.Descriptor.Height == 480 && raw.Descriptor.PixelFormat == RawPixelFormat.Mono8, "RAW uses explicit settings after RGB input");
                model.OpenCommand.Execute(metadata); var meta = model.SelectedDocument!;
                Check(raw.Rendered!.Bgra32.SequenceEqual(meta.Rendered!.Bgra32), "RAW and matching snapshot have identical displayed pixels");
                model.OpenCommand.Execute(rawPath.ToUpperInvariant());
                Check(model.Documents.Count == 3 && model.SelectedDocument == raw && dialogs.RawRequests == 3, "Canonical case-insensitive reopen activates without another RAW prompt");
                canvas.Zoom(2); var state = raw.ViewState;
                model.SelectedDocument = meta; model.SelectedDocument = raw;
                Check(ReferenceEquals(canvas.State, state) && canvas.ZoomScale == 2, "Document selection restores saved view state");
                model.DuplicateCommand.Execute(null);
                Check(model.Documents.Count == 4 && model.SelectedDocument != raw && model.SelectedDocument!.Source != raw.Source, "Explicit copy gets an independent source/view");
                model.CloseCommand.Execute(null);
                Check(model.Documents.Count == 3 && model.SelectedDocument != null, "Close selects a remaining document");
                model.SelectedDocument = raw;
                model.OpenCommand.Execute(Path.Combine(root, "missing.rbuf.json"));
                Check(model.SelectedDocument == raw && model.ActionStatus.Contains("both"), "Missing snapshot keeps selection and explains companion recovery");
                model.OpenCommand.Execute(Path.Combine(root, "image.png"));
                Check(model.SelectedDocument == raw && model.ActionStatus.Contains("not RAW input"), "Unsupported image input is explained");
                dialogs.SavePath = null; model.SavePngCommand.Execute(null);
                Check(model.ActionStatus.Contains("cancelled"), "PNG cancel is visible");
                var png = Path.Combine(root, "result.png"); dialogs.SavePath = png; model.SavePngCommand.Execute(null);
                var savedPng = File.ReadAllBytes(png);
                Check(savedPng.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && model.ActionStatus.Contains(png), "PNG success names a real PNG output");
                using (var locked = File.Open(png, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) model.SavePngCommand.Execute(null);
                Check(model.ActionStatus.Contains("failed") && File.ReadAllBytes(png).SequenceEqual(savedPng), "PNG failure preserves existing output");
                Check(!Directory.GetFiles(root, ".rbv-*.tmp").Any(), "Failed PNG stage is cleaned");
                dialogs.SavePath = null; model.SaveSnapshotCommand.Execute(null); PumpUntil(() => model.ExportCompletion.IsCompleted);
                Check(model.ActionStatus.Contains("cancelled"), "Snapshot file-picker cancel is visible");
                dialogs.SavePath = Path.Combine(root, "saved.rbuf.json");
                model.SaveSnapshotCommand.Execute(null); model.SaveSnapshotCommand.Execute(null);
                Check(dialogs.Exports == 1 && !model.CloseCommand.CanExecute(null) && !model.OpenCommand.CanExecute(null), "Snapshot admits one operation and protects source-changing commands");
                PumpUntil(() => model.ExportCompletion.IsCompleted);
                var saved = RawBufferSnapshot.LoadReference(dialogs.SavePath);
                Check(File.ReadAllBytes(saved.RawPath).SequenceEqual(bytes) && model.ActionStatus.Contains(saved.RawPath) && model.ActionStatus.Contains(saved.MetadataPath), "Snapshot saves exact RAW bytes and reports both files");
                var count = model.Documents.Count; model.OpenCommand.Execute(saved.MetadataPath);
                Check(model.Documents.Count == count && model.SelectedDocument == raw, "Saved metadata reopens its existing document");
                dialogs.FailExport = true; model.SaveSnapshotCommand.Execute(null); PumpUntil(() => model.ExportCompletion.IsCompleted);
                Check(model.ActionStatus.Contains("failed") && model.SaveSnapshotCommand.CanExecute(null), "Snapshot failure is visible and permits retry");
                model.OpenExampleCommand.Execute(null); var example = model.SelectedDocument;
                model.OpenExampleCommand.Execute(null);
                Check(example?.IsExample == true && model.SelectedDocument == example && model.SavePngCommand.CanExecute(null), "Example is usable and repeated opening selects it");
                while (model.Documents.Count > 0) model.CloseCommand.Execute(null);
                Check(model.IsEmpty && model.ZoomText == "—" && model.WidthText == "—" && model.Histogram.Result == null && !model.SavePngCommand.CanExecute(null), "Last close restores empty state and clears histogram");
            }
        }

        private static void FileReadRecovery(string root)
        {
            var descriptor = new RawImageDescriptor { Width = 17, Height = 13, Stride = 32, PixelFormat = RawPixelFormat.Mono8, ValidBits = 8 };
            var bytes = Enumerable.Repeat((byte)42, (int)descriptor.GetRequiredByteCount()).ToArray();
            var path = Path.Combine(root, "mutable.raw");
            File.WriteAllBytes(path, bytes);
            var document = new ViewerDocument(path, RawImageSource.FromFile(path, descriptor));
            var captured = new ViewerDocument(null, RawImageSource.FromMemory(bytes, descriptor));
            var canvas = new TestCanvas(); var dialogs = new TestDialogs();
            using (var model = new ViewerViewModel(canvas, dialogs))
            {
                model.Documents.Add(document); model.Documents.Add(captured); model.SelectedDocument = document;
                PumpUntil(() => !model.Histogram.IsRunning);
                Check(model.HasImage && model.Histogram.Result?.Minimum == 42 && model.DiagnosticSummary.StartsWith("OK"), "File source accepts a final row without trailing padding");
                File.WriteAllBytes(path, new byte[16]);
                canvas.Hover(0, 0);
                Check(model.PixelText.Contains("unavailable") && model.DiagnosticSummary.Contains("failed") && model.Diagnostics.Any(d => d.Contains("buffer length: 16")), "Even an early pixel rejects a truncated image and diagnoses current file length");
                Check(document.Source.Length == bytes.Length, "Original payload length remains the snapshot contract");
                model.Histogram.ActionCommand.Execute(null); PumpUntil(() => !model.Histogram.IsRunning);
                Check(model.Histogram.Error != null && model.Histogram.Result == null && model.DiagnosticSummary.Contains("failed"), "Histogram failure agrees with document diagnostics");
                dialogs.SavePath = Path.Combine(root, "truncated-export.rbuf.json");
                model.SaveSnapshotCommand.Execute(null); PumpUntil(() => model.ExportCompletion.IsCompleted);
                Check(model.ActionStatus.Contains("failed") && !Directory.GetFiles(root, "truncated-export*").Any() && !Directory.GetFiles(root, ".rbv-*.tmp").Any(), "Truncated snapshot export leaves no partial output");
                model.SelectedDocument = captured; PumpUntil(() => !model.Histogram.IsRunning);
                Check(model.DiagnosticSummary.StartsWith("OK") && model.Histogram.Error == null && captured.Source.ReadAllBytes().SequenceEqual(bytes), "Read failure does not leak into captured data or diagnostics");
                model.SelectedDocument = document; PumpUntil(() => !model.Histogram.IsRunning);
                Check(!model.HasImage && model.DiagnosticSummary.Contains("failed") && model.Histogram.ActionCommand.CanExecute(null), "Reselecting a damaged file stays failed but retains Refresh recovery");
                File.WriteAllBytes(path, bytes);
                model.Histogram.ActionCommand.Execute(null); PumpUntil(() => !model.Histogram.IsRunning);
                Check(model.HasImage && model.DiagnosticSummary.StartsWith("OK") && model.Histogram.Error == null && model.Histogram.Result?.Minimum == 42, "Explicit Refresh reloads the restored file and clears the old failure");
                File.Delete(path);
                model.Histogram.ActionCommand.Execute(null); PumpUntil(() => !model.Histogram.IsRunning);
                Check(model.DiagnosticSummary.Contains("failed") && model.Diagnostics.Any(d => d.Contains("RAW file unavailable")), "Removed file reports unavailable instead of stale OK");
                File.WriteAllBytes(path, bytes);
                using (var locked = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    model.Histogram.ActionCommand.Execute(null); PumpUntil(() => !model.Histogram.IsRunning);
                    Check(model.Histogram.Error != null && model.DiagnosticSummary.Contains("failed"), "Read sharing failure remains an error even with valid file length");
                }
                model.Histogram.ActionCommand.Execute(null); PumpUntil(() => !model.Histogram.IsRunning);
                Check(model.HasImage && model.DiagnosticSummary.StartsWith("OK"), "Unlock and Refresh recover without reopening the document");
                model.CloseCommand.Execute(null); PumpUntil(() => !model.Histogram.IsRunning);
                Check(model.SelectedDocument == captured && model.DiagnosticSummary.StartsWith("OK"), "Closing repaired file retains the captured document");
            }
        }

        private static void InspectionStatus()
        {
            var model = new AutomaticInspectionStatusViewModel();
            model.SetImageStatus("Captured 19 x 11 BGR24");
            Check(model.Status == "Debugger stopped" && !model.CanScan, "No debug session disables Scan Now");
            model.SetMode(InspectionDebuggerMode.Running);
            Check(model.Status == "Debugger running" && !model.CanScan, "Run mode is explicit and disables scanning");
            model.SetMode(InspectionDebuggerMode.Paused);
            Check(model.Status == "Debugger paused" && model.CanScan, "Break mode updates independently of automatic scanning");
            model.Frame = "OpenVisionLab.OpenVisionLabApplication.Run";
            model.SetDetails("Auto Inspect is off and saved.");
            Check(model.CanScan && model.Status == "Debugger paused", "Saved Auto Inspect guidance cannot override actual debugger state");
            model.IsBusy = true; Check(!model.CanScan && model.Status == "Scanning…", "Busy scan disables duplicate admission");
            model.IsBusy = false; model.SetDetails("Injected discovery failure", "Scan failed");
            Check(model.Status == "Scan failed" && model.CanScan, "Failure is visible and manual retry remains available");
            model.SetMode(InspectionDebuggerMode.Stopped);
            Check(model.Status == "Debugger stopped" && !model.CanScan && !model.Frame.Contains("OpenVisionLab") && model.StatusDetails.Contains("Captured"), "Session end clears old frame and scan alert while retaining capture information");
            model.SetTransientStatus("Release highlights closed");
            Check(model.Status == "Release highlights closed" && model.StatusDetails.Contains("Captured"), "An action notice keeps the captured-image details");
            model.SetMode(InspectionDebuggerMode.Running);
            Check(model.Status == "Debugger running" && !model.CanScan, "Continuing after closing release highlights clears the old notice");
            model.SetTransientStatus("Environment check closed");
            model.SetMode(InspectionDebuggerMode.Stopped);
            Check(model.Status == "Debugger stopped" && !model.CanScan, "Session end clears a transient panel notice");
            model.SetMode(InspectionDebuggerMode.Paused);
            model.SetTransientStatus("Opening image");
            model.SetImageStatus("Captured 19 x 11 BGR24");
            Check(model.Status == "Debugger paused" && model.CanScan, "A completed image update clears the opening notice");
            model.SetTransientStatus("Environment check refreshed");
            model.SetDetails("Injected discovery failure", "Scan failed");
            Check(model.Status == "Scan failed", "Current scan details replace an earlier action notice");
            model.SetImageStatus("Live unavailable", true);
            model.SetTransientStatus("Support report copied");
            model.SetMode(InspectionDebuggerMode.Stopped);
            Check(model.Status == "Live unavailable" && !model.CanScan, "Clearing a notice on session end preserves the actual image error");
            model.SetImageStatus("Captured 19 x 11 BGR24");
            Check(model.Status == "Debugger stopped", "Selecting a valid capture recovers from the image error");
        }

        private static void BorrowedLifetime(string root)
        {
            var source = new GatedSource();
            var document = new ViewerDocument(Path.Combine(root, "borrow.raw"), source);
            using (var model = new ViewerViewModel(new TestCanvas(), new TestDialogs()))
            {
                model.Documents.Add(document); model.SelectedDocument = document;
                PumpUntil(() => source.Entered.IsSet);
                model.CloseCommand.Execute(null);
                Check(source.Disposals == 0 && model.IsEmpty, "Closing during histogram detaches UI but retains borrowed source");
                source.Gate.Set(); PumpUntil(() => model.Histogram.Completion.IsCompleted);
                Check(source.Disposals == 1 && model.Histogram.Result == null, "Settled histogram releases once and cannot publish after close");
                document.Dispose(); Check(source.Disposals == 1, "Document disposal is idempotent");
            }
            source.Gate.Dispose(); source.Entered.Dispose();
        }

        private static void PumpUntil(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Usability task did not settle.");
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                Thread.Sleep(1);
            }
        }
        private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); _checks++; Console.WriteLine("PASS " + message); }

        private sealed class TestDialogs : IViewerDialogHost
        {
            public RawImageDescriptor? RawSettings;
            public string? SavePath;
            public int RawRequests, Exports;
            public bool FailExport;
            public string? ChooseOpenPath() => null;
            public RawImageDescriptor? ConfigureRaw(string path, long length) { RawRequests++; return RawSettings; }
            public string? ChooseSavePath(string name, bool snapshot) => SavePath;
            public async Task<RawBufferSnapshotReference?> ExportSnapshot(SnapshotExportViewModel model)
            {
                Exports++;
                if (FailExport) throw new IOException("Injected export failure");
                var result = await model.RunAsync();
                if (model.Error != null) throw new InvalidOperationException(model.Error);
                return result;
            }
        }

        private sealed class TestCanvas : IViewerCanvas
        {
            private RawImageDescriptor? _descriptor;
            public event EventHandler? ViewChanged;
            public event EventHandler<RawOpenGlPixelEventArgs>? PixelHovered;
            public void Hover(int x, int y) => PixelHovered?.Invoke(this, new RawOpenGlPixelEventArgs(x, y));
            public double ZoomScale { get; private set; } = 684;
            public int TileCount => _descriptor == null ? 0 : 1;
            public RawOpenGlViewState? State;
            public RawOpenGlViewState? GetViewState() => State;
            public void Load(RawImageSource source, RawOpenGlViewState? state) { _descriptor = source.Descriptor; State = state; ZoomScale = state == null ? 1 : _descriptor.Width / state.Width; }
            public void Clear() { _descriptor = null; State = null; }
            public void Fit() => Zoom(1);
            public void Zoom(double scale)
            {
                ZoomScale = scale;
                if (_descriptor != null) State = new RawOpenGlViewState(_descriptor.Width, _descriptor.Height, 0, 0, _descriptor.Width / scale, _descriptor.Height / scale);
                ViewChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private sealed class GatedSource : RawImageSource
        {
            public readonly ManualResetEventSlim Gate = new ManualResetEventSlim(false), Entered = new ManualResetEventSlim(false);
            public int Disposals;
            private readonly RawImageSource _inner;
            public GatedSource() : base(new RawImageDescriptor { Width = 1, Height = 1, Stride = 1, PixelFormat = RawPixelFormat.Mono8, ValidBits = 8 }, 1, null)
                => _inner = RawImageSource.FromMemory(new byte[] { 42 }, Descriptor);
            public override bool IsFileBacked => false;
            public override RawImageSource WithDescriptor(RawImageDescriptor descriptor) => _inner.WithDescriptor(descriptor);
            public override RawRenderOptions CreateRenderOptions() => _inner.CreateRenderOptions();
            public override RenderedImage RenderTile(int x, int y, int width, int height, RawRenderOptions? options) => _inner.RenderTile(x, y, width, height, options);
            public override string DescribePixel(int x, int y) => _inner.DescribePixel(x, y);
            public override byte[] ReadAllBytes() => _inner.ReadAllBytes();
            public override void CopyRawTo(string path) => _inner.CopyRawTo(path);
            public override RawPixelHistogram GetHistogram(RawHistogramChannel channel, CancellationToken cancellationToken)
            {
                Entered.Set();
                if (!Gate.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Gated source timeout");
                return _inner.GetHistogram(channel, CancellationToken.None);
            }
            public override void Dispose() { Disposals++; _inner.Dispose(); }
        }
    }
}
