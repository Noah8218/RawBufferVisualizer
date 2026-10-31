param(
    [string]$SourcePath = (Join-Path $PSScriptRoot '..\src\RawBufferVisualizer.VisualStudio.Vssdk\RawBufferToolWindowControl.xaml.cs'),
    [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputPath) { throw 'Preserve the existing result; do not repeat this check blindly.' }
if ('RawBufferFocusChecks_20261031.Checks' -as [type]) { throw 'Use a fresh supported PowerShell Core process to avoid reusing previously compiled source.' }
$source = Get-Content -LiteralPath $SourcePath -Raw
$method = [regex]::Matches($source, '(?ms)^        private void InspectorOverlay_Opened\(object sender, EventArgs e\)\r?\n        \{.*?^        \}\r?\n(?=\r?\n        private void InspectorOverlay_PreviewKeyDown)')
if ($method.Count -ne 1) { throw 'Expected exactly one complete production event handler.' }

# Compile the actual handler with a task-backed queue double, not a WPF window or debugger.
$prefix = @'
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace RawBufferFocusChecks_20261031 {
enum DispatcherPriority { Input }
static class RawBufferVisualizerPackageLog {
    internal static readonly ConcurrentQueue<string> Messages = new ConcurrentQueue<string>();
    internal static void Write(string message) { Messages.Enqueue(message); }
    internal static void Reset() { while (Messages.TryDequeue(out _)) {} }
    internal static bool Contains(string text) { foreach (var message in Messages) if (message.Contains(text)) return true; return false; }
}
sealed class Operation {
    readonly TaskCompletionSource<object> completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly Action action;
    internal Operation(Action action) { this.action = action; }
    public Task Task { get { return completion.Task; } }
    internal void Run() { try { action(); completion.SetResult(null); } catch (Exception ex) { completion.SetException(ex); } }
    internal void Cancel() { completion.SetCanceled(); }
}
sealed class QueueDispatcher {
    public Operation Pending;
    public bool ThrowOnSchedule;
    public DispatcherPriority Priority;
    public Operation InvokeAsync(Action action, DispatcherPriority priority) {
        if (ThrowOnSchedule) throw new InvalidOperationException("fixture schedule failure");
        Priority = priority; Pending = new Operation(action); return Pending;
    }
    public Operation BeginInvoke(DispatcherPriority priority, Action action) { return InvokeAsync(action, priority); }
}
sealed class Popup { public bool IsOpen = true; }
sealed class Button {
    public int Calls; public bool Result = true; public bool ThrowOnFocus;
    public bool Focus() { Calls++; if (ThrowOnFocus) throw new ArgumentException("fixture focus failure"); return Result; }
}
sealed class Subject {
    bool _disposed;
    public readonly QueueDispatcher Dispatcher = new QueueDispatcher();
    public readonly Popup InspectorOverlayPopup = new Popup();
    public readonly Button CollapseInspectorButton = new Button();
    public void Open() { InspectorOverlay_Opened(this, EventArgs.Empty); }
    public void DisposeBeforeExecution() { _disposed = true; }
'@

$suffix = @'
}
public sealed class CaseResult { public string Id; public bool Passed; public string Expected; public string Actual; }
sealed class ClosedUiContext : SynchronizationContext {
    public int Posts;
    public override void Post(SendOrPostCallback callback, object state) { Interlocked.Increment(ref Posts); }
}
public static class Checks {
    static readonly List<CaseResult> results = new List<CaseResult>();
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static void AwaitLog(string text) {
        Require(SpinWait.SpinUntil(() => RawBufferVisualizerPackageLog.Contains(text), TimeSpan.FromSeconds(2)), "No completion diagnostic: " + text);
    }
    static void Check(string id, string expected, Action action) {
        RawBufferVisualizerPackageLog.Reset();
        try { action(); results.Add(new CaseResult { Id=id, Passed=true, Expected=expected, Actual=expected }); }
        catch (Exception ex) { results.Add(new CaseResult { Id=id, Passed=false, Expected=expected, Actual=ex.ToString() }); }
    }
    public static CaseResult[] Run() {
        Check("deferred.input", "focus remains queued at Input priority", () => {
            var s = new Subject(); s.Open(); Require(s.Dispatcher.Pending != null && s.Dispatcher.Priority == DispatcherPriority.Input && s.CollapseInspectorButton.Calls == 0, "Focus did not remain deferred"); s.Dispatcher.Pending.Run();
        });
        Check("focus.true", "one Focus call and unchanged True diagnostic", () => {
            var s = new Subject(); s.Open(); s.Dispatcher.Pending.Run(); Require(s.CollapseInspectorButton.Calls == 1 && RawBufferVisualizerPackageLog.Contains("initial focus=True"), "True result changed");
        });
        Check("focus.false", "False remains a normal focus result", () => {
            var s = new Subject(); s.CollapseInspectorButton.Result=false; s.Open(); s.Dispatcher.Pending.Run(); Require(s.CollapseInspectorButton.Calls == 1 && RawBufferVisualizerPackageLog.Contains("initial focus=False") && !RawBufferVisualizerPackageLog.Contains("failed"), "False became an error");
        });
        Check("closed.before.execution", "closed popup receives no Focus call", () => {
            var s = new Subject(); s.Open(); s.InspectorOverlayPopup.IsOpen=false; s.Dispatcher.Pending.Run(); Require(s.CollapseInspectorButton.Calls == 0 && RawBufferVisualizerPackageLog.Messages.IsEmpty, "Closed popup was focused");
        });
        Check("disposed.before.execution", "disposed control receives no Focus call", () => {
            var s = new Subject(); s.Open(); s.DisposeBeforeExecution(); s.Dispatcher.Pending.Run(); Require(s.CollapseInspectorButton.Calls == 0 && RawBufferVisualizerPackageLog.Messages.IsEmpty, "Disposed control was focused");
        });
        Check("focus.fault.closed.ui.context", "full focus exception observed without posting to closed UI context", () => {
            var s = new Subject(); s.CollapseInspectorButton.ThrowOnFocus=true; var previous=SynchronizationContext.Current; var closed=new ClosedUiContext();
            try { SynchronizationContext.SetSynchronizationContext(closed); s.Open(); } finally { SynchronizationContext.SetSynchronizationContext(previous); }
            s.Dispatcher.Pending.Run(); AwaitLog("initial focus failed:"); Require(RawBufferVisualizerPackageLog.Contains("ArgumentException: fixture focus failure") && closed.Posts == 0, "Fault detail or context isolation lost");
        });
        Check("operation.cancel.closed.ui.context", "aborted operation observed with zero Focus calls and no closed-context post", () => {
            var s = new Subject(); var previous=SynchronizationContext.Current; var closed=new ClosedUiContext();
            try { SynchronizationContext.SetSynchronizationContext(closed); s.Open(); } finally { SynchronizationContext.SetSynchronizationContext(previous); }
            s.Dispatcher.Pending.Cancel(); AwaitLog("initial focus canceled before execution."); Require(s.CollapseInspectorButton.Calls == 0 && closed.Posts == 0, "Cancellation touched UI or lost observation");
        });
        Check("scheduling.fault", "scheduling exception observed without queueing work", () => {
            var s = new Subject(); s.Dispatcher.ThrowOnSchedule=true; s.Open(); Require(s.Dispatcher.Pending == null && s.CollapseInspectorButton.Calls == 0 && RawBufferVisualizerPackageLog.Contains("initial focus scheduling failed: System.InvalidOperationException: fixture schedule failure"), "Scheduling failure escaped or disappeared");
        });
        return results.ToArray();
    }
}
}
'@

Add-Type -TypeDefinition ($prefix + [Environment]::NewLine + $method[0].Value + [Environment]::NewLine + $suffix) -Language CSharp -ErrorAction Stop
$cases = @([RawBufferFocusChecks_20261031.Checks]::Run())
$report = [pscustomobject]@{
    RecordedUtc = [DateTime]::UtcNow.ToString('o')
    SourcePath = [IO.Path]::GetFullPath($SourcePath)
    SourceSha256 = (Get-FileHash -LiteralPath $SourcePath -Algorithm SHA256).Hash
    Cases = $cases
    Total = $cases.Count
    Passed = @($cases | Where-Object Passed).Count
    Failed = @($cases | Where-Object { -not $_.Passed }).Count
    Limits = 'Extracted actual production handler with a task-backed queue double; no WPF dispatcher/window, focus hardware, installed IDE, transfer, or GUI execution.'
}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($OutputPath))) | Out-Null
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding utf8
$report | Select-Object RecordedUtc, Total, Passed, Failed, Limits
if ($report.Failed -ne 0) { exit 1 }
