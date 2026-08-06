using System;
using System.IO;
using RawBufferVisualizer.VisualStudio;

namespace RawBufferVisualizer.Tests
{
    internal static class WorkspaceAndHandoffCoordinatorTests
    {
        public static void RunAll()
        {
            WorkspaceOwnsActivationRemovalAndDispose();
            DebuggerHandoffSessionRejectsRunModeAndPreviousBreakWork();
            SnapshotLeaseSurvivesSweepAndMovesOnPreviewReplacement();
            ClaimedHandoffPublishesAckAndNackFromOpenOutcome();
        }

        private static void DebuggerHandoffSessionRejectsRunModeAndPreviousBreakWork()
        {
            var gate = new DebuggerHandoffSessionGate();
            Assert(!gate.IsCurrent(gate.Capture()), "Handoff gate accepted work before Break Mode.");

            gate.EnterBreakMode();
            var firstBreak = gate.Capture();
            Assert(gate.IsCurrent(firstBreak), "Handoff gate rejected current Break Mode work.");

            gate.EnterRunMode();
            Assert(!gate.IsCurrent(firstBreak), "Handoff gate accepted previous-break work after Continue.");
            var capturedWhileRunning = gate.Capture();
            Assert(!gate.IsCurrent(capturedWhileRunning), "Handoff gate accepted work captured in Run Mode.");

            gate.EnterBreakMode();
            Assert(!gate.IsCurrent(firstBreak), "Previous-break work became current in the next Break Mode.");
            Assert(!gate.IsCurrent(capturedWhileRunning), "Run Mode work became current in the next Break Mode.");
            Assert(gate.IsCurrent(gate.Capture()), "Handoff gate rejected work from the new Break Mode.");
        }

        private static void WorkspaceOwnsActivationRemovalAndDispose()
        {
            var first = new DisposableDocument("first");
            var second = new DisposableDocument("second");
            var third = new DisposableDocument("third");
            var workspace = new RawBufferDocumentWorkspace<DisposableDocument>();
            workspace.Add(first);
            workspace.Add(second);
            workspace.Add(third);
            Assert(workspace.Activate(second), "Workspace did not activate the selected document.");

            var replacement = workspace.RemoveAt(1, true);
            Assert(second.DisposeCount == 1, "Removed document was not disposed exactly once.");
            Assert(workspace.ActiveDocument == null, "Removing the active document did not clear activation.");
            Assert(ReferenceEquals(replacement, third), "Workspace did not suggest the adjacent replacement.");
            workspace.Activate(replacement!);

            workspace.RemoveAt(0, true);
            Assert(first.DisposeCount == 1, "Removing an inactive document did not dispose it.");
            Assert(ReferenceEquals(workspace.ActiveDocument, third), "Inactive removal changed the active document.");

            workspace.Dispose();
            workspace.Dispose();
            Assert(third.DisposeCount == 1, "Workspace disposal was not idempotent.");
            Assert(workspace.Documents.Count == 0, "Workspace disposal retained documents.");
        }

        private static void SnapshotLeaseSurvivesSweepAndMovesOnPreviewReplacement()
        {
            var previewDirectory = VisualStudioTempStore.CreateSnapshotDirectory();
            var fullDirectory = VisualStudioTempStore.CreateSnapshotDirectory();
            var previewMetadata = Path.Combine(previewDirectory, "preview.rbuf.json");
            var fullMetadata = Path.Combine(fullDirectory, "full.rbuf.json");
            File.WriteAllText(previewMetadata, "{}");
            File.WriteAllText(fullMetadata, "{}");
            var owner = new VisualStudioSnapshotLeaseOwner();
            try
            {
                owner.Replace(previewMetadata, true);
                Directory.SetLastWriteTimeUtc(previewDirectory, DateTime.UtcNow.AddDays(-2));
                VisualStudioTempStore.TryCleanupStaleSnapshotDirectories(TimeSpan.FromHours(24));
                Assert(Directory.Exists(previewDirectory), "A stale sweep deleted a leased preview directory.");

                owner.Replace(fullMetadata, true);
                Assert(!Directory.Exists(previewDirectory), "Preview replacement stranded the previous snapshot directory.");
                Directory.SetLastWriteTimeUtc(fullDirectory, DateTime.UtcNow.AddDays(-2));
                VisualStudioTempStore.TryCleanupStaleSnapshotDirectories(TimeSpan.FromHours(24));
                Assert(Directory.Exists(fullDirectory), "A stale sweep deleted the replacement document directory.");

                owner.Dispose();
                Assert(!Directory.Exists(fullDirectory), "Document disposal did not release and delete the replacement directory.");
            }
            finally
            {
                owner.Dispose();
                VisualStudioTempStore.TryDeleteDirectory(previewDirectory);
                VisualStudioTempStore.TryDeleteDirectory(fullDirectory);
            }
        }

        private static void ClaimedHandoffPublishesAckAndNackFromOpenOutcome()
        {
            var request = new VisualizerHandoffRequest(
                Path.Combine(Path.GetTempPath(), "coordinator.rbuf.json"),
                "coordinator",
                "test");
            var ackCount = 0;
            var nackCount = 0;
            var coordinator = new ClaimedHandoffOpenCoordinator(
                delegate { return request; },
                delegate
                {
                    ackCount++;
                    return true;
                },
                delegate
                {
                    nackCount++;
                    return true;
                },
                delegate { return VisualizerHandoffRequestState.Processing; },
                delegate { });

            var success = coordinator.Open(
                "success.rbuf-handoff",
                "success.processing",
                delegate { return ClaimedHandoffOpenResult.Success(); });
            Assert(success.Succeeded, "Successful claimed handoff did not publish ACK.");
            Assert(ackCount == 1 && nackCount == 0, "Successful handoff published an unexpected terminal marker.");

            var failure = coordinator.Open(
                "failure.rbuf-handoff",
                "failure.processing",
                delegate { return ClaimedHandoffOpenResult.Failure("open failed"); });
            Assert(!failure.Succeeded, "Failed claimed handoff was reported as successful.");
            Assert(ackCount == 1 && nackCount == 1, "Failed handoff did not publish exactly one NACK.");
            Assert(failure.FailureReason == "open failed", "NACK did not preserve the opener failure reason.");

            var thrown = coordinator.Open(
                "exception.rbuf-handoff",
                "exception.processing",
                delegate { throw new InvalidOperationException("boom"); });
            Assert(!thrown.Succeeded && thrown.Exception is InvalidOperationException,
                "Thrown opener failure was not returned as a failed outcome.");
            Assert(ackCount == 1 && nackCount == 2, "Thrown opener failure did not publish NACK.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class DisposableDocument : IDisposable
        {
            public DisposableDocument(string name)
            {
                Name = name;
            }

            public string Name { get; private set; }
            public int DisposeCount { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
            }
        }
    }
}
