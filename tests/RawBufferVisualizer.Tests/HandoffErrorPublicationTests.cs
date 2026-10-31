using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RawBufferVisualizer.VisualStudio;

namespace RawBufferVisualizer.Tests
{
    internal static class HandoffErrorPublicationTests
    {
        public static void RunAll()
        {
            var passed = 0;
            Check("successful publication retains earlier requests", () =>
            {
                var paths = new List<string> { "retained-snapshot" };
                var attempts = 0;
                var published = VisualizerHandoffInbox.TryPublishErrorRequest(paths, () =>
                {
                    attempts++;
                    return "error-card";
                });
                Require(published && attempts == 1 && paths.Count == 2
                    && paths[0] == "retained-snapshot" && paths[1] == "error-card",
                    "Successful publication must append exactly one request.");
            });

            Check("failed publication preserves requests and both errors", () =>
            {
                var paths = new List<string> { "retained-snapshot" };
                var attempts = 0;
                using (var output = new StringWriter())
                using (var listener = new TextWriterTraceListener(output))
                {
                    Trace.Listeners.Add(listener);
                    try
                    {
                        var published = VisualizerHandoffInbox.TryPublishErrorRequest(paths, () =>
                        {
                            attempts++;
                            throw new IOException("synthetic publication failure");
                        }, new InvalidOperationException("synthetic original failure"));
                        listener.Flush();
                        Require(!published && attempts == 1 && paths.Count == 1
                            && paths[0] == "retained-snapshot",
                            "A failed publication must not append, retry or remove requests.");
                        Require(output.ToString().Contains("synthetic original failure")
                            && output.ToString().Contains("synthetic publication failure"),
                            "Fallback diagnostics must retain the original and reporting errors.");
                    }
                    finally { Trace.Listeners.Remove(listener); }
                }
            });

            Check("a failed notice without an original error is contained", () =>
            {
                var paths = new List<string>();
                Require(!VisualizerHandoffInbox.TryPublishErrorRequest(paths,
                    () => throw new UnauthorizedAccessException("synthetic notice failure"))
                    && paths.Count == 0, "A notice publication failure must return false.");
            });

            Check("a failed trace listener cannot escape the reporting boundary", () =>
            {
                var paths = new List<string> { "retained-snapshot" };
                using (var listener = new ThrowingTraceListener())
                {
                    Trace.Listeners.Add(listener);
                    try
                    {
                        Require(!VisualizerHandoffInbox.TryPublishErrorRequest(paths,
                            () => throw new IOException("synthetic publication failure")),
                            "Publication failure must remain contained when diagnostics fail.");
                        Require(listener.Calls > 0 && paths.Count == 1,
                            "The failing listener must be exercised without losing earlier requests.");
                    }
                    finally { Trace.Listeners.Remove(listener); }
                }
            });

            Check("a later invocation can publish after a failed invocation", () =>
            {
                var paths = new List<string>();
                Require(!VisualizerHandoffInbox.TryPublishErrorRequest(paths,
                    () => throw new InvalidOperationException("synthetic expired session")),
                    "A failed session must not be reported as a published request.");
                Require(VisualizerHandoffInbox.TryPublishErrorRequest(paths, () => "new-session-error")
                    && paths.Count == 1 && paths[0] == "new-session-error",
                    "Failure must not poison a later independent invocation.");
            });
            Console.WriteLine("Handoff error publication: " + passed + "/5 checks passed.");

            void Check(string name, Action action)
            {
                action();
                passed++;
                Console.WriteLine("PASS " + name);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class ThrowingTraceListener : TraceListener
        {
            public int Calls { get; private set; }
            public override void Write(string? message) => Fail();
            public override void WriteLine(string? message) => Fail();
            private void Fail()
            {
                Calls++;
                throw new IOException("synthetic trace failure");
            }
        }
    }
}
