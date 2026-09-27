using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.DebuggerVisualizers;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.VisualStudio;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.Tests
{
    internal static class MappingSaveTests
    {
        public static void RunAll()
        {
            var root = Path.Combine(Path.GetTempPath(), "mapping-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                SolutionSwitchAndIdentity(root);
                EditScopeAndReopen(root);
                CorruptionAndStaleEdits(root);
                ConcurrentWriters(root);
                ConcurrentProcesses(root);
                SnapshotTransactions(root);
                StreamingAndCancellation(root);
                AsyncExportLifetime(root);
                CollectionContext(root);
                Console.WriteLine("Mapping/save: 9 focused groups passed.");
            }
            finally { Directory.Delete(root, true); }
        }

        private static void SolutionSwitchAndIdentity(string root)
        {
            var userPath = Path.Combine(root, "user.json");
            var a = TypeMappingStore.ForSolution(Path.Combine(root, "A", "A.sln"), userPath);
            var b = TypeMappingStore.ForSolution(Path.Combine(root, "B", "B.sln"), userPath);
            var noSolution = TypeMappingStore.ForSolution(null, userPath);
            Check(a.SolutionLocalPath == Path.Combine(root, "A", TypeMappingStore.SolutionLocalFileName), "Absent mapping must retain its solution destination.");
            Check(noSolution.SolutionLocalPath == null && TypeMappingStore.CreateDefault().SolutionLocalPath == null, "No process-directory solution fallback.");
            var user = a.LoadUserFile();
            user.Mappings.Add(Map("Frame", "A", "User"));
            user.Mappings.Add(Map("Other", "", "UserOther"));
            a.Save(user);
            var initialVersion = a.GetContentVersion();
            var local = a.LoadFile(TypeMappingScope.Solution);
            local.Mappings.Add(Map("Frame", "", "LocalWide"));
            local.Mappings.Add(Map("Frame", "A", "LocalA"));
            local.Mappings.Add(Map("Frame", "B", "LocalB"));
            a.Save(local, TypeMappingScope.Solution);
            Check(initialVersion != a.GetContentVersion(), "New mapping file invalidates the scan cache.");
            Check(a.FindMapping("Frame", "A")!.Members.Data == "LocalA", "Exact assembly must precede type-wide fallback.");
            Check(a.FindMapping("Frame", "B")!.Members.Data == "LocalB", "Assemblies must not cross-match.");
            Check(a.FindMappingByTypeNameOnly("Frame")!.Members.Data == "LocalWide", "Unknown assembly only uses an explicit type-wide entry.");
            Check(b.FindMapping("Frame", "A")!.Members.Data == "User", "Solution B must not retain A's local mappings.");
            Check(noSolution.FindMappingByTypeNameOnly("Frame") == null, "Unknown identity must not borrow a single assembly-specific entry.");
            var transferred = TypeMappingStore.FromSnapshot(a.ExportEffectiveMappings());
            Check(transferred.FindMapping("Frame", "C")!.Members.Data == "LocalWide", "Transferred scope precedence must match file-backed lookup.");
            Check(transferred.FindMapping("Frame", "A")!.Members.Data == "LocalA", "Transferred exact identity must match.");
            var detached = a.FindMapping("Frame", "A")!;
            detached.Members.Data = "Unsaved";
            Check(a.FindMapping("Frame", "A")!.Members.Data == "LocalA", "Read results must not mutate the cached file.");
            var stamp = File.GetLastWriteTimeUtc(a.SolutionLocalPath!);
            File.WriteAllText(a.SolutionLocalPath!, File.ReadAllText(a.SolutionLocalPath!).Replace("LocalA", "LocalX"));
            File.SetLastWriteTimeUtc(a.SolutionLocalPath!, stamp);
            Check(a.FindMapping("Frame", "A")!.Members.Data == "LocalX", "Equal timestamp edits must still be noticed.");
            var fake = new FakeDte { Solution = new FakeSolution { FullName = Path.Combine(root, "B", "B.sln") } };
            Check(VisualStudioInstance.GetSolutionPath(fake) == fake.Solution.FullName, "Host uses Solution.FullName.");
            fake.Solution.FullName = "";
            Check(VisualStudioInstance.GetSolutionPath(fake) == null, "Closing the solution clears host context.");
        }

        private static void CorruptionAndStaleEdits(string root)
        {
            var path = Path.Combine(root, "conflict.json");
            var store = new TypeMappingStore(null, path);
            var file = store.LoadUserFile();
            file.Mappings.Add(Map("Frame", "Assembly", "Original"));
            store.Save(file);
            var first = new TypeMappingStore(null, path).LoadUserFile();
            var second = new TypeMappingStore(null, path).LoadUserFile();
            first.Mappings[0].Members.Data = "First";
            store.Save(first);
            var original = File.ReadAllBytes(path);
            second.Mappings[0].Members.Data = "Second";
            Throws<IOException>(() => store.Save(second));
            Check(File.ReadAllBytes(path).SequenceEqual(original), "A stale edit must not overwrite the winner.");
            var beforeLock = store.LoadUserFile();
            beforeLock.Mappings[0].Members.Data = "Locked";
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Throws<IOException>(() => store.Save(beforeLock));
            Check(File.ReadAllBytes(path).SequenceEqual(original), "A locked destination must preserve old bytes.");
            Check(!Directory.GetFiles(root, "*.tmp").Any(), "Failed atomic replacement cleans staging files.");
            foreach (var damaged in new[] { "{broken", "{\"version\":99,\"mappings\":[]}", "{\"version\":1,\"mappings\":null}" })
            {
                File.WriteAllText(path, damaged);
                Check(store.FindMapping("Frame", "Assembly") == null && store.LastLoadError.Length > 0, "Malformed input remains visible as a load error.");
                Throws<Exception>(() => store.LoadUserFile());
                Throws<IOException>(() => store.Save(new TypeMappingFile()));
                Check(File.ReadAllText(path) == damaged, "Corrupt files must never become empty replacements.");
            }
            var goodPath = Path.Combine(root, "user.json");
            var fallback = new TypeMappingStore(path, goodPath);
            Check(fallback.FindMapping("Other", "") != null && fallback.LastLoadError.Contains(path), "A valid fallback must not erase the solution error.");
        }

        private static void EditScopeAndReopen(string root)
        {
            var store = TypeMappingStore.ForSolution(Path.Combine(root, "edit", "project.sln"), Path.Combine(root, "edit-user.json"));
            var session = new TypeMappingEditSession(store, "Frame", "Assembly");
            Check(session.PreferredScope == TypeMappingScope.Solution && session.HasSolutionScope, "New mappings default to the active solution.");
            Check(!File.Exists(store.SolutionLocalPath!) && !File.Exists(store.UserPath), "Opening an editor must not persist anything.");
            var savedMapping = Map("Frame", "Assembly", "SavedUser");
            session.Save(savedMapping, TypeMappingScope.User);
            savedMapping.TypeName = "ChangedAfterSave";
            Check(session.HasExistingMapping(TypeMappingScope.User), "The editor must retain a detached copy of the saved identity.");
            var reopened = new TypeMappingEditSession(store, "Frame", "Assembly");
            Check(reopened.PreferredScope == TypeMappingScope.User && reopened.ExistingMapping!.Members.Data == "SavedUser", "Reopen restores the actual scope and saved values.");
            Check(reopened.GetPath(TypeMappingScope.User) == store.UserPath && !File.Exists(store.SolutionLocalPath!), "Inspecting another scope must not copy or save a mapping.");
            var stale = new TypeMappingEditSession(store, "Frame", "Assembly");
            var newType = new TypeMappingEditSession(store, "NewFrame", "Assembly");
            reopened.Save(Map("Frame", "Assembly", "OtherEditor"), TypeMappingScope.User);
            Throws<IOException>(() => newType.Save(Map("NewFrame", "Assembly", "Failed"), TypeMappingScope.User));
            Check(!newType.HasExistingMapping(TypeMappingScope.User), "A failed save must not masquerade as an existing persisted mapping.");
            Throws<IOException>(() => stale.Save(Map("Frame", "Assembly", "Stale"), TypeMappingScope.User));
            Check(store.FindMapping("Frame", "Assembly")!.Members.Data == "OtherEditor", "Editor conflict compares with the version captured at open.");
            reopened.Save(Map("Frame", "Assembly", "Solution"), TypeMappingScope.Solution);
            var solutionReopen = new TypeMappingEditSession(store, "Frame", "Assembly");
            Check(solutionReopen.PreferredScope == TypeMappingScope.Solution && solutionReopen.ExistingMapping!.Members.Data == "Solution", "Solution scope and values survive save/reopen.");
            Throws<ArgumentException>(() => solutionReopen.Save(Map("Other", "Assembly", "WrongType"), TypeMappingScope.User));
            File.WriteAllText(store.SolutionLocalPath!, "{ broken");
            var damaged = new TypeMappingEditSession(store, "Frame", "Assembly");
            Check(damaged.GetLoadError(TypeMappingScope.Solution).Length > 0, "A damaged scope has an actionable load error.");
            Throws<InvalidOperationException>(() => damaged.Save(Map("Frame", "Assembly", "Wrong"), TypeMappingScope.Solution));
            Check(File.ReadAllText(store.SolutionLocalPath!) == "{ broken", "Editor cannot replace a damaged scope.");
        }

        private static void ConcurrentWriters(string root)
        {
            var path = Path.Combine(root, "concurrent.json");
            var one = new TypeMappingStore(null, path);
            var two = new TypeMappingStore(null, path);
            var first = one.LoadUserFile();
            var second = two.LoadUserFile();
            first.Mappings.Add(Map("Frame", "", "One"));
            second.Mappings.Add(Map("Frame", "", "Two"));
            var successes = 0;
            var conflicts = 0;
            using (var gate = new Barrier(2))
            {
                Action<TypeMappingStore, TypeMappingFile> save = (store, file) =>
                {
                    gate.SignalAndWait();
                    try { store.Save(file); Interlocked.Increment(ref successes); }
                    catch (IOException) { Interlocked.Increment(ref conflicts); }
                };
                Task.WaitAll(Task.Run(() => save(one, first)), Task.Run(() => save(two, second)));
            }
            Check(successes == 1 && conflicts == 1, "Exactly one concurrently created mapping file commits.");
            Check(one.FindMapping("Frame", "") != null, "Winning JSON is complete and readable.");
        }

        private static void SnapshotTransactions(string root)
        {
            var path = Path.Combine(root, "snapshot.rbuf.json");
            var descriptor = Descriptor(2);
            var first = RawBufferSnapshot.Save(path, new byte[] { 1, 2 }, descriptor);
            var originalMetadata = File.ReadAllBytes(path);
            var longPath = Path.Combine(root, new string('s', 235) + ".rbuf.json");
            var longName = RawBufferSnapshot.Save(longPath, new byte[] { 1, 2 }, descriptor);
            Check(Path.GetFileName(longName.RawPath!).Length <= 255 && RawBufferSnapshot.Load(longPath).Buffer.SequenceEqual(new byte[] { 1, 2 }), "Staging and raw names must not grow an accepted file name beyond the Windows component limit.");
            using (var source = RawImageSource.FromMemory(new byte[] { 3, 4 }, descriptor))
            {
                using (var cancellation = new CancellationTokenSource())
                {
                    var progress = new InlineProgress(value => { if (value > 0) cancellation.Cancel(); });
                    Throws<OperationCanceledException>(() => RawBufferSnapshot.SaveSource(path, source, descriptor, cancellation.Token, progress));
                }
                Check(File.ReadAllBytes(path).SequenceEqual(originalMetadata), "Cancellation after raw copy must not publish metadata.");
                Check(Directory.GetFiles(root, "snapshot.*.raw").Length == 1, "Unpublished cancelled payload is removed.");
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    Throws<IOException>(() => RawBufferSnapshot.SaveSource(path, source, descriptor));
                Check(File.ReadAllBytes(path).SequenceEqual(originalMetadata), "Commit failure preserves old metadata.");
                var replacement = RawBufferSnapshot.SaveSource(path, source, descriptor);
                Check(replacement.RawPath != first.RawPath, "Overwrite must not mutate the old payload.");
                Check(RawBufferSnapshot.Load(path).Buffer.SequenceEqual(new byte[] { 3, 4 }), "Committed metadata opens the complete new snapshot.");
                Check(File.ReadAllBytes(first.RawPath!).SequenceEqual(new byte[] { 1, 2 }), "Previous raw data is retained unchanged.");
                var snapshotCount = Directory.GetFiles(root, "snapshot.*.raw").Length;
                var concurrent = new InlineProgress(value => { if (value == 0) File.WriteAllText(path, "external edit"); });
                Throws<IOException>(() => RawBufferSnapshot.SaveSource(path, source, descriptor, progress: concurrent));
                Check(File.ReadAllText(path) == "external edit", "Snapshot commit detects a concurrent metadata edit.");
                Check(Directory.GetFiles(root, "snapshot.*.raw").Length == snapshotCount, "A conflicting snapshot leaves no unpublished payload.");
            }
        }

        private static void StreamingAndCancellation(string root)
        {
            var bytes = Enumerable.Range(0, 2 * 1024 * 1024 + 17).Select(i => (byte)i).ToArray();
            var descriptor = Descriptor(bytes.Length);
            var rawPath = Path.Combine(root, "stream.raw");
            File.WriteAllBytes(rawPath, bytes);
            using (var source = RawImageSource.FromFile(rawPath, descriptor))
            {
                long progressValue = -1;
                var saved = RawBufferSnapshot.SaveSource(Path.Combine(root, "stream.rbuf.json"), source, descriptor,
                    progress: new InlineProgress(value => { Check(value >= progressValue && value <= bytes.Length, "Monotonic bounded progress."); progressValue = value; }));
                Check(progressValue == bytes.Length && File.ReadAllBytes(saved.RawPath).SequenceEqual(bytes), "Stream every byte including the partial final block.");
            }
            using (var source = new VirtualSource(3L * 1024 * 1024 * 1024))
            using (var sink = new CountingStream())
            {
                source.CopyRawTo(sink, CancellationToken.None);
                Check(sink.Length == source.Length && source.MaximumRead <= 1024 * 1024, "Payloads above 2 GiB use bounded reads and 64-bit progress.");
            }
            var failurePath = Path.Combine(root, "fault.rbuf.json");
            RawBufferSnapshot.Save(failurePath, new byte[] { 5, 6 }, Descriptor(2));
            using (var source = new VirtualSource(4 * 1024 * 1024) { FailAt = 1024 * 1024 })
                Throws<IOException>(() => RawBufferSnapshot.SaveSource(failurePath, source, source.Descriptor));
            Check(RawBufferSnapshot.Load(failurePath).Buffer.SequenceEqual(new byte[] { 5, 6 }), "Unreadable source preserves previous snapshot.");
            Check(Directory.GetFiles(root, "fault.*.raw").Length == 1, "Failed source payload is removed.");
        }

        private static void CollectionContext(string root)
        {
            var store = TypeMappingStore.ForSolution(Path.Combine(root, "collection", "solution.sln"), Path.Combine(root, "collection-user.json"));
            var file = store.LoadFile(TypeMappingScope.Solution);
            var mapping = Map(typeof(MappedFrame).FullName!, typeof(MappedFrame).Assembly.GetName().Name!, "Pixels");
            mapping.Members.Width = "Columns";
            mapping.Members.Height = "Rows";
            file.Mappings.Add(mapping);
            store.Save(file, TypeMappingScope.Solution);
            var frozen = TypeMappingStore.FromSnapshot(store.ExportEffectiveMappings());
            file.Mappings.Clear();
            store.Save(file, TypeMappingScope.Solution);
            var view = ImageCollectionVisualizerTransfer.CreateView(new[] { new MappedFrame() }, frozen);
            var item = view.GetMetadata(0);
            Check(item.Metadata != null && item.Error.Length == 0, "Explicit host snapshot resolves the custom collection item independently of debuggee directories.");
            var chunk = view.GetChunk(0, new VisualizerSnapshotChunkRequest { Offset = 0, Count = 2 });
            Check(chunk.Buffer.SequenceEqual(new byte[] { 7, 8 }), "Transferred mappings read the intended fields.");
        }

        private static void AsyncExportLifetime(string root)
        {
            var path = Path.Combine(root, "async.rbuf.json");
            var bytes = new byte[] { 9, 10 };
            var descriptor = Descriptor(bytes.Length);
            var callerThread = Environment.CurrentManagedThreadId;
            using (var operation = new SnapshotExportOperation())
            using (var source = RawImageSource.FromMemory(bytes, descriptor))
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                var task = operation.SaveAsync(path, source, descriptor, new InlineProgress(value =>
                {
                    Check(Environment.CurrentManagedThreadId != callerThread, "Export copying must leave the calling thread.");
                    if (value == 0) { entered.Set(); Check(release.Wait(10000), "Export worker gate must be released."); }
                }));
                try
                {
                    Check(entered.Wait(10000) && operation.IsRunning, "Async export returns while the copy is still active.");
                    Throws<InvalidOperationException>(() => operation.SaveAsync(path, source, descriptor));
                    operation.Cancel();
                }
                finally { release.Set(); }
                Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
                Check(!operation.IsRunning && !File.Exists(path), "Cancellation ends the operation without publication.");
                operation.SaveAsync(path, source, descriptor).GetAwaiter().GetResult();
                Check(RawBufferSnapshot.Load(path).Buffer.SequenceEqual(bytes), "A cancelled export can be retried explicitly.");
                entered.Reset();
                release.Reset();
                var disposing = operation.SaveAsync(path, source, descriptor, new InlineProgress(value =>
                {
                    if (value == 0) { entered.Set(); Check(release.Wait(10000), "Dispose test worker gate must be released."); }
                }));
                try { Check(entered.Wait(10000), "Dispose test worker starts."); operation.Dispose(); }
                finally { release.Set(); }
                Throws<OperationCanceledException>(() => disposing.GetAwaiter().GetResult());
                Throws<ObjectDisposedException>(() => operation.SaveAsync(path, source, descriptor));
                Check(RawBufferSnapshot.Load(path).Buffer.SequenceEqual(bytes), "Owner disposal preserves the last successful snapshot.");
            }
            var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                using (var source = RawImageSource.FromProcessMemory(Process.GetCurrentProcess().Id, pinned.AddrOfPinnedObject().ToInt64(), bytes.Length, descriptor))
                using (var operation = new SnapshotExportOperation())
                using (var entered = new ManualResetEventSlim())
                using (var release = new ManualResetEventSlim())
                {
                    var live = operation.SaveAsync(path, source, descriptor, new InlineProgress(value =>
                    {
                        if (value == 0) { entered.Set(); Check(release.Wait(10000), "Live export gate must be released."); }
                    }));
                    try { Check(entered.Wait(10000), "Live export starts."); operation.CancelLiveExport(); }
                    finally { release.Set(); }
                    Throws<OperationCanceledException>(() => live.GetAwaiter().GetResult());
                    Check(RawBufferSnapshot.Load(path).Buffer.SequenceEqual(bytes), "Continue cancellation does not overwrite the previous snapshot.");
                }
            }
            finally { pinned.Free(); }
        }

        public static void RunWire()
        {
            var mappings = new TypeMappingSnapshot();
            var mapping = Map(typeof(MappedFrame).FullName!, typeof(MappedFrame).Assembly.GetName().Name!, "Pixels");
            mapping.Members.Width = "Columns";
            mapping.Members.Height = "Rows";
            mappings.Solution.Mappings.Add(mapping);
            var frozen = TypeMappingStore.FromSnapshot(mappings);
            var target = new[] { new MappedFrame() };
            var objectSource = new ImageCollectionVisualizerObjectSource();
            var summary = JsonWire.Send<VisualizerCollectionSummary>(objectSource, target,
                new VisualizerCollectionItemRequest { Operation = VisualizerCollectionOperation.ConfigureMappings, Mappings = frozen.ExportEffectiveMappings() });
            Check(summary.ItemCount == 1 && summary.ExpressionIdentityHash != 0, "Configuration request returns the collection summary through the actual JSON protocol.");
            var metadata = JsonWire.Send<VisualizerCollectionItemMetadata>(objectSource, target,
                new VisualizerCollectionItemRequest { Operation = VisualizerCollectionOperation.Metadata, Index = 0 });
            Check(metadata.Metadata != null && metadata.Error.Length == 0, "Configured context survives the next metadata request.");
            JsonWire.Send<VisualizerCollectionSummary>(objectSource, target,
                new VisualizerCollectionItemRequest { Operation = VisualizerCollectionOperation.ConfigureMappings, Mappings = new TypeMappingSnapshot() });
            var cleared = JsonWire.Send<VisualizerCollectionItemMetadata>(objectSource, target,
                new VisualizerCollectionItemRequest { Operation = VisualizerCollectionOperation.Metadata, Index = 0 });
            Check(cleared.Metadata == null && cleared.Error.Length > 0, "A new context must not reuse the previous collection view or mapping.");
            Console.WriteLine("Mapping JSON protocol configure / metadata / reconfigure passed.");
        }

        private static void ConcurrentProcesses(string root)
        {
            var directory = Path.Combine(root, "processes");
            Directory.CreateDirectory(directory);
            Process Start(string identity)
            {
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true };
                foreach (var argument in new[] { typeof(MappingSaveTests).Assembly.Location, "--mapping-save-writer", directory, identity })
                    start.ArgumentList.Add(argument);
                return Process.Start(start)!;
            }
            using (var one = Start("one"))
            using (var two = Start("two"))
            {
                try
                {
                    var watch = Stopwatch.StartNew();
                    while (!File.Exists(Path.Combine(directory, "one.ready")) || !File.Exists(Path.Combine(directory, "two.ready")))
                    {
                        if (watch.ElapsedMilliseconds > 10000) throw new TimeoutException("Mapping writers did not become ready.");
                        Thread.Sleep(5);
                    }
                    File.WriteAllText(Path.Combine(directory, "start"), "go");
                    Check(one.WaitForExit(10000) && two.WaitForExit(10000), "Mapping processes terminate deterministically.");
                    Check(new[] { one.ExitCode, two.ExitCode }.OrderBy(code => code).SequenceEqual(new[] { 0, 3 }), "Independent processes must produce one commit and one conflict.");
                    Check(new TypeMappingStore(null, Path.Combine(directory, "mappings.json")).FindMapping("Frame", "") != null, "Cross-process winner remains readable.");
                }
                finally
                {
                    if (!one.HasExited) { one.Kill(); one.WaitForExit(); }
                    if (!two.HasExited) { two.Kill(); two.WaitForExit(); }
                }
            }
        }

        public static int RunWriter(string directory, string identity)
        {
            var store = new TypeMappingStore(null, Path.Combine(directory, "mappings.json"));
            var file = store.LoadUserFile();
            file.Mappings.Add(Map("Frame", "", identity));
            File.WriteAllText(Path.Combine(directory, identity + ".ready"), "ready");
            var watch = Stopwatch.StartNew();
            while (!File.Exists(Path.Combine(directory, "start")))
            {
                if (watch.ElapsedMilliseconds > 10000) throw new TimeoutException("Mapping writer gate did not open.");
                Thread.Sleep(5);
            }
            try { store.Save(file); return 0; }
            catch (IOException) { return 3; }
        }

        private sealed class JsonWire : VisualizerObjectSource
        {
            public static T Send<T>(ImageCollectionVisualizerObjectSource source, object target, VisualizerCollectionItemRequest request)
            {
                using (var input = new MemoryStream())
                using (var output = new MemoryStream())
                {
                    SerializeAsJson(input, request);
                    input.Position = 0;
                    source.TransferData(target, input, output);
                    output.Position = 0;
                    return DeserializeFromJson<T>(output)!;
                }
            }
        }

        private static TypeMapping Map(string type, string assembly, string data) => new TypeMapping
        { TypeName = type, AssemblyName = assembly, Members = new TypeMappingMembers { Data = data, Width = "Width", Height = "Height" } };
        private static RawImageDescriptor Descriptor(int length) => new RawImageDescriptor { Width = length, Height = 1, Stride = length, PixelFormat = RawPixelFormat.Mono8, ValidBits = 8 };
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name);
        }

        public sealed class FakeDte { public FakeSolution Solution { get; set; } = new FakeSolution(); }
        public sealed class FakeSolution { public string FullName { get; set; } = ""; }
        private sealed class MappedFrame { public byte[] Pixels = { 7, 8 }; public int Columns = 2; public int Rows = 1; }
        private sealed class InlineProgress : IProgress<long>
        {
            private readonly Action<long> _report;
            public InlineProgress(Action<long> report) { _report = report; }
            public void Report(long value) => _report(value);
        }
        private sealed class VirtualSource : RawImageSource
        {
            public int MaximumRead { get; private set; }
            public long FailAt { get; set; } = long.MaxValue;
            public VirtualSource(long length) : base(new RawImageDescriptor { Width = 1, Height = 1, Stride = 1, PixelFormat = RawPixelFormat.Mono8, ValidBits = 8 }, length, null) { }
            public override bool IsFileBacked => false;
            public override bool TryReadRange(long offset, byte[] destination, int start, int count) { MaximumRead = Math.Max(MaximumRead, count); return offset < FailAt; }
            public override RawImageSource WithDescriptor(RawImageDescriptor descriptor) => throw new NotSupportedException();
            public override RawRenderOptions CreateRenderOptions() => throw new NotSupportedException();
            public override RenderedImage RenderTile(int x, int y, int width, int height, RawRenderOptions? options) => throw new NotSupportedException();
            public override string DescribePixel(int x, int y) => throw new NotSupportedException();
            public override byte[] ReadAllBytes() => throw new InvalidOperationException("Export must not allocate the entire payload.");
            public override void CopyRawTo(string path) => throw new InvalidOperationException("Export must use cancellable copying.");
        }
        private sealed class CountingStream : Stream
        {
            private long _length;
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => _length;
            public override long Position { get => _length; set => throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count) => _length += count;
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
