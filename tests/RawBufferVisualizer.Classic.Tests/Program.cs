using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.DebuggerVisualizers;
using Newtonsoft.Json;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.VisualStudio;
using RawBufferVisualizer.VisualStudio.Classic;
using RawBufferVisualizer.VisualStudio.Debugger;
using RawBufferVisualizer.VisualStudio.ObjectSource;

internal static class Program
{
    private static int _cases;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--debugger-results")
            {
                DebuggerResultProviderTests.RunAll();
                return 0;
            }

            if (args.Length == 3 && args[0] == "--debugger-bytes")
            {
                VerifyDebuggerBytes(args[1], args[2]);
                return 0;
            }

            if (args.Contains("--require-netstandard-payload"))
            {
                foreach (var type in new[] { typeof(RawImageDescriptor), typeof(RawBufferSnapshot), typeof(RawBufferSnapshotVisualizerObjectSource) })
                {
                    Require(type.Assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName == ".NETStandard,Version=v2.0",
                        "Expected netstandard2.0 debugger payload for " + type.Assembly.GetName().Name);
                }
                Console.WriteLine("PASS: running with the netstandard2.0 Core/SDK/ObjectSource payload.");
            }

            // The fake host must establish the same paused-session admission as the docked IDE.
            VisualizerHandoffInbox.PublishDebuggerSession(Process.GetCurrentProcess().Id, Guid.NewGuid().ToString("N"));

            if (args.Contains("--collection"))
            {
                VerifyCollections();
                return 0;
            }
            if (args.Contains("--large-handoff"))
            {
                VerifyLargeHandoff(8L * 1024 * 1024, false);
                VerifyLargeHandoff(64L * 1024 * 1024, true);
                VerifyLargeCollectionHandoff();
                Console.WriteLine("PASS: direct-memory and preview-first handoff requests, shared identity, no chunk transport or helper UI.");
                Console.WriteLine("Boundary: fake provider and pointer metadata; no full native read or installed IDE transfer is claimed.");
                return 0;
            }
            if (args.Contains("--supported-images"))
            {
                VerifySupportedImages();
                return 0;
            }
            DebuggerResultProviderTests.RunAll();
            VerifyInstallPathRegistrations();
            VerifyNativeSnapshotCapture();
            VerifySupportedImages();

            var mono8 = new RawBufferSnapshot(new byte[] { 1, 2, 3, 99, 4, 5, 6, 98 }, new RawImageDescriptor
            {
                Width = 3, Height = 2, Stride = 4, PixelFormat = RawPixelFormat.Mono8, ValidBits = 8
            });
            Verify(mono8, false, false);
            var packed = new RawBufferSnapshot(new byte[] { 0, 4, 32, 192, 0 }, new RawImageDescriptor
            {
                Width = 4, Height = 1, Stride = 5, PixelFormat = RawPixelFormat.Mono10PackedLsb, ValidBits = 10
            });
            Verify(packed, false, false);
            Verify(mono8, true, true);
            Verify(mono8, false, false); // Recovery after a failed chunk, using the same Show entry.
            Verify(new RawBufferSnapshot(new byte[1], mono8.Descriptor), false, true);
            Verify(packed, false, false);
            Console.WriteLine("PASS: " + _cases + " Show cases; exact bytes/layout, error/recovery, repeated handoff, zero dialog-service calls; self-targeted install-path registrations only.");
            Console.WriteLine("Boundary: fake provider transport invokes the real ObjectSource and Show. This is not an installed Visual Studio host test.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void VerifyDebuggerBytes(string adapterPath, string enginePath)
    {
        Assembly.LoadFrom(Path.GetFullPath(enginePath));
        var adapter = Assembly.LoadFrom(Path.GetFullPath(adapterPath));
        var copy = adapter.GetType("RawBufferVisualizer.VisualStudio.Debugger.ImageVisualizerResultProvider", true)!
            .GetMethod("CopyVisualizerBytes", BindingFlags.Static | BindingFlags.NonPublic)!;
        var original = new byte[] { 0, 1, 0, 255, 128 };
        var wrapper = new TestByteCollection(original);
        var result = (ReadOnlyCollection<byte>)copy.Invoke(null, new object[] { wrapper })!;
        Require(result.GetType() == typeof(ReadOnlyCollection<byte>), "Engine-specific subclass must not cross the managed return boundary.");
        Require(result.SequenceEqual(original), "All bytes must remain exact, including zero and unsigned values.");
        original[1] = 99;
        Require(result[1] == 1, "Forwarded bytes must own their storage.");
        var empty = (ReadOnlyCollection<byte>)copy.Invoke(null, new object[] { new TestByteCollection(Array.Empty<byte>()) })!;
        Require(empty.Count == 0 && empty.GetType() == typeof(ReadOnlyCollection<byte>), "Empty payload preserves its contract.");
        Require(copy.Invoke(null, new object?[] { null }) == null, "Optional null remains null; do not invent payload bytes.");
        Console.WriteLine("PASS: managed byte forwarding preserves exact payload, owns storage, removes collection subclass and retains empty/null semantics.");
        Console.WriteLine("Boundary: real adapter helper; subclass fixture does not emulate a native COM buffer or installed Concord dispatch.");
    }

    private sealed class TestByteCollection : ReadOnlyCollection<byte>
    {
        public TestByteCollection(byte[] bytes) : base(bytes) { }
    }

    private static void VerifyInstallPathRegistrations()
    {
        var registrations = typeof(RawBufferClassicDebuggerVisualizer).Assembly
            .GetCustomAttributes(typeof(DebuggerVisualizerAttribute), false).Cast<DebuggerVisualizerAttribute>().ToArray();
        Require(registrations.Length == 2, "Both Classic UI IDs must be registered for extension install-path lookup.");
        foreach (var type in new[] { typeof(RawBufferClassicDebuggerVisualizer), typeof(ImageCollectionClassicDebuggerVisualizer) })
        {
            Require(registrations.Count(registration => registration.VisualizerTypeName == type.AssemblyQualifiedName
                && registration.TargetTypeName == type.AssemblyQualifiedName) == 1,
                "Install-path registrations must target only their own Classic UI class, never broad image or collection types.");
        }
    }

    private static void VerifyCollections()
    {
        var type = typeof(RawBufferClassicDebuggerVisualizer).Assembly.GetType("RawBufferVisualizer.VisualStudio.Classic.ImageCollectionClassicDebuggerVisualizer", true)!;
        VerifyInstallPathRegistrations();
        var snapshot = new RawBufferSnapshot(new byte[] { 1, 2, 3 }, new RawImageDescriptor
        {
            Width = 3, Height = 1, Stride = 3, PixelFormat = RawPixelFormat.Mono8, ValidBits = 8
        });
        VerifyCollection(type, new object[] { snapshot, 42, null! }, _ => snapshot, false, 1, 2);
        VerifyCollection(type, new object[] { snapshot }, _ => snapshot, true, 0, 1);
        VerifyCollection(type, new object[] { snapshot }, _ => snapshot, false, 1, 0);
        VerifyCollection(type, Array.Empty<object>(), _ => snapshot, false, 0, 1);
        VerifyCollection(type, new object[] { snapshot, snapshot }, _ => snapshot, false, 2, 0);
        Console.WriteLine("PASS: 5 collection Show cases; mixed/null/error retention, empty collection, chunk failure/recovery, repeated handoff, exact bytes, zero dialog-service calls; self-targeted install-path registrations only.");
        Console.WriteLine("Boundary: real ObjectSource and Show with fake transport/context; installed IDE qualification remains required.");
    }

    private static void VerifyCollection(Type type, object target, Func<string, RawBufferSnapshot> expectedByName, bool failChunk, int successes, int errors, VisualizerObjectSource? source = null)
    {
        var dialogs = new RejectingDialogService();
        int wakes = 0;
        Func<int, TypeMappingSnapshot> mappings = _ => new TypeMappingSnapshot();
        Action<int> wake = processId =>
        {
            wakes++;
            Require(processId == Process.GetCurrentProcess().Id, "Collection must target the current host.");
            var paths = Directory.GetFiles(VisualizerHandoffInbox.GetInboxDirectory(processId), "*.rbuf-handoff");
            Require(paths.Length == successes + errors, "Collection did not retain each success/error result.");
            int actualSuccesses = 0, actualErrors = 0;
            foreach (string path in paths)
            {
                var request = VisualizerHandoffInbox.ReadSnapshotRequestInfo(path);
                try
                {
                    Require(request.OriginSessionToken == VisualizerHandoffInbox.CaptureDebuggerSession(processId),
                        "Collection must retain the admitted debugger session.");
                    if (request.IsError)
                    {
                        actualErrors++;
                        Require(!string.IsNullOrWhiteSpace(request.ErrorMessage), "Empty collection error.");
                    }
                    else
                    {
                        actualSuccesses++;
                        var stored = RawBufferSnapshot.Load(request.MetadataPath);
                        var expected = expectedByName(request.DisplayName);
                        Require(stored.Buffer.SequenceEqual(expected.Buffer), "Collection bytes changed.");
                        VerifyLayout(stored.Descriptor, expected.Descriptor);
                    }
                }
                finally
                {
                    if (!request.IsError) VisualStudioTempStore.TryDeleteSnapshotDirectoryForMetadata(request.MetadataPath);
                    VisualizerHandoffInbox.TryDeleteRequest(path);
                }
            }
            Require(actualSuccesses == successes && actualErrors == errors, "Unexpected collection success/error count.");
        };
        var visualizer = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { mappings, wake }, null)!;
        type.GetMethod("Show", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(visualizer, new object[] { dialogs, new SnapshotProvider(target, failChunk, source) });
        Require(wakes == 1 && dialogs.Calls == 0, "Collection must wake the docked viewer once without any dialog call.");
        Require(!Directory.GetDirectories(VisualStudioTempStore.RootDirectory).Any(p => Path.GetFileName(p) != "Inbox"), "Collection artifacts leaked after terminal cleanup.");
    }

    private static void Verify(RawBufferSnapshot snapshot, bool failChunk, bool expectError)
    {
        VerifySingle(snapshot, snapshot, failChunk, expectError);
        _cases++;
    }

    private static void VerifySingle(object target, RawBufferSnapshot snapshot, bool failChunk, bool expectError, VisualizerObjectSource? source = null)
    {
        var dialogs = new RejectingDialogService();
        var wakes = 0;
        Action<int> wake = processId =>
        {
            wakes++;
            Require(processId == Process.GetCurrentProcess().Id, "Handoff must target the host process.");
            var paths = Directory.GetFiles(VisualizerHandoffInbox.GetInboxDirectory(processId), "*.rbuf-handoff");
            Require(paths.Length == 1, "Each invocation must publish exactly one result.");
            var path = paths[0];
            var request = VisualizerHandoffInbox.ReadSnapshotRequestInfo(path);
            try
            {
                Require(request.OriginSessionToken == VisualizerHandoffInbox.CaptureDebuggerSession(processId),
                    "Handoff must retain the admitted debugger session.");
                Require(request.IsError == expectError, "Success/error result mismatch: " + request.ErrorMessage);
                if (!expectError)
                {
                    var stored = RawBufferSnapshot.Load(request.MetadataPath);
                    Require(stored.Buffer.SequenceEqual(snapshot.Buffer), "Captured bytes changed.");
                    VerifyLayout(stored.Descriptor, snapshot.Descriptor);
                }
                else
                {
                    Require(!string.IsNullOrWhiteSpace(request.ErrorMessage), "Failure must reach the existing viewer.");
                }
            }
            finally
            {
                if (!request.IsError) VisualStudioTempStore.TryDeleteSnapshotDirectoryForMetadata(request.MetadataPath);
                VisualizerHandoffInbox.TryDeleteRequest(path);
            }
        };
        var visualizer = (RawBufferClassicDebuggerVisualizer)Activator.CreateInstance(
            typeof(RawBufferClassicDebuggerVisualizer), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { wake }, null)!;
        typeof(RawBufferClassicDebuggerVisualizer).GetMethod("Show", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(visualizer, new object[] { dialogs, new SnapshotProvider(target, failChunk, source) });
        Require(wakes == 1, "Existing docked viewer must be activated once.");
        Require(dialogs.Calls == 0, "A helper window was requested.");
        Require(!Directory.GetDirectories(VisualStudioTempStore.RootDirectory).Any(p => Path.GetFileName(p) != "Inbox"), "Unowned snapshot artifacts remain after transfer/failure.");
    }

    private static void VerifyLayout(RawImageDescriptor actual, RawImageDescriptor expected)
    {
        Require(actual.Width == expected.Width && actual.Height == expected.Height && actual.Stride == expected.Stride
            && actual.PixelFormat == expected.PixelFormat && actual.ValidBits == expected.ValidBits
            && actual.ByteOrder == expected.ByteOrder, "Captured layout changed.");
    }

    private static void VerifySupportedImages()
    {
        var tested = new HashSet<(string? TypeName, string ObjectSource)>();
        void Verify(string name, object target, object collection, string objectSource, RawBufferSnapshot expected)
        {
            VerifyImageCase(name, target, collection, objectSource, expected);
            var type = target.GetType();
            tested.Add(((type.IsGenericType ? type.GetGenericTypeDefinition() : type).FullName, objectSource));
        }

        var address = Marshal.AllocHGlobal(8);
        try
        {
            var padded = new byte[] { 1, 2, 3, 91, 4, 5, 6 };
            var mono = Expected(padded, 3, 2, 4, RawPixelFormat.Mono8, 8);
            Marshal.Copy(padded, 0, address, padded.Length);
            var view = new RawBufferView { Buffer = address, BufferLength = padded.Length, Width = 3, Height = 2, Stride = 4,
                PixelFormat = RawPixelFormat.Mono8, BitDepth = 8, Channels = 1 };
            var pointer = new Cressem.ImageModel.ImagePtr(address, padded.Length, 3, 2, 4, 1);
            var packed = new Cressem.ImageModel.ImagePtr(address, 6, 2, 2, 0, 0) { PixelFormat = "Mono12PackedLsb" };
            var nonZeroArray = Array.CreateInstance(typeof(Cressem.ImageModel.ImagePtr), new[] { 1 }, new[] { 3 });
            nonZeroArray.SetValue(pointer, 3);

            using (var bitmap = new Bitmap(4, 2, PixelFormat.Format24bppRgb))
            using (var openCv = new OpenCvSharp.Mat(2, 3, OpenCvSharp.MatType.CV_8UC3, new OpenCvSharp.Scalar(13, 37, 99)))
            using (var typedMat = new OpenCvSharp.Mat<byte>(2, 3, OpenCvSharp.Scalar.All(23)))
            using (var emgu = new Emgu.CV.Mat(2, 3, Emgu.CV.CvEnum.DepthType.Cv8U, 3))
            {
                using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.FromArgb(51, 39, 17));
                emgu.SetTo(new Emgu.CV.Structure.MCvScalar(13, 37, 99));
                var color = Expected(Enumerable.Repeat(new byte[] { 13, 37, 99 }, 6).SelectMany(pixel => pixel).ToArray(), 3, 2, 9, RawPixelFormat.BGR24, 8);
                var bitmapExpected = Expected(Enumerable.Repeat(new byte[] { 17, 39, 51 }, 8).SelectMany(pixel => pixel).ToArray(), 4, 2, 12, RawPixelFormat.BGR24, 8);
                Verify("Bitmap", bitmap, new[] { bitmap }, nameof(BitmapVisualizerObjectSource), bitmapExpected);
                Verify("OpenCvSharp.Mat", openCv, new System.Collections.ArrayList { openCv }, nameof(OpenCvSharpMatVisualizerObjectSource), color);
                Verify("Emgu.CV.Mat", emgu, new System.Collections.Hashtable { ["frame"] = emgu }, nameof(EmguCvMatVisualizerObjectSource), color);
                Verify("RawBufferView", view, new List<RawBufferView> { view }, nameof(RawBufferViewVisualizerObjectSource), mono);
                Verify("RawBufferSnapshot", mono, new Dictionary<string, object> { ["frame"] = mono }, nameof(RawBufferSnapshotVisualizerObjectSource), mono);
                var concurrent = new System.Collections.Concurrent.ConcurrentDictionary<string, object>();
                concurrent["frame"] = pointer;
                Verify("ImagePtr", pointer, concurrent, nameof(ImagePtrVisualizerObjectSource), mono);
                Verify("Mat<T>", typedMat, new[] { typedMat }, nameof(OpenCvSharpMatVisualizerObjectSource),
                    Expected(Enumerable.Repeat((byte)23, 6).ToArray(), 3, 2, 3, RawPixelFormat.Mono8, 8));
                Verify("packed ImagePtr", packed, new object[] { packed }, nameof(ImagePtrVisualizerObjectSource),
                    Expected(padded.Take(6).ToArray(), 2, 2, 3, RawPixelFormat.Mono12PackedLsb, 12));
                Verify("nonzero array ImagePtr", pointer, nonZeroArray, nameof(ImagePtrVisualizerObjectSource), mono);

                var mixed = new object[] { bitmap, openCv, emgu, view, mono, pointer };
                var collectionType = typeof(ImageCollectionClassicDebuggerVisualizer);
                var collectionSource = ResolveObjectSource(mixed, collectionType, nameof(ImageCollectionVisualizerObjectSource));
                var expected = new Dictionary<string, RawBufferSnapshot>
                {
                    ["[0]"] = bitmapExpected, ["[1]"] = color, ["[2]"] = color, ["[3]"] = mono, ["[4]"] = mono, ["[5]"] = mono
                };
                VerifyCollection(collectionType, mixed, name => expected[name], false, mixed.Length, 0, collectionSource);
                Console.WriteLine("PASS: six supported image types in one object[]; every item captured with its expected index, layout and bytes.");
            }
        }
        finally { Marshal.FreeHGlobal(address); }
        var missing = ImageVisualizerTypePolicy.Registrations.Select(item => ((string?)item.TypeName, item.ObjectSource))
            .Except(tested).Select(item => item.Item1).ToArray();
        Require(missing.Length == 0, "Missing real transfer fixture for: " + string.Join(", ", missing));
        Console.WriteLine("Boundary: actual native and managed image objects (ImagePtr shape fixture), real Classic Show and ObjectSource, simulated Concord callbacks/transport.");
    }

    private static RawBufferSnapshot Expected(byte[] bytes, int width, int height, int stride, RawPixelFormat format, int validBits)
    {
        return new RawBufferSnapshot(bytes, new RawImageDescriptor
        {
            Width = width, Height = height, Stride = stride, PixelFormat = format, ValidBits = validBits, ByteOrder = RawByteOrder.LittleEndian
        });
    }

    private static void VerifyImageCase(string name, object target, object collection, string objectSource, RawBufferSnapshot expected)
    {
        var single = ResolveObjectSource(target, typeof(RawBufferClassicDebuggerVisualizer), objectSource);
        var collectionSource = ResolveObjectSource(collection, typeof(ImageCollectionClassicDebuggerVisualizer), nameof(ImageCollectionVisualizerObjectSource));
        VerifyWireTransfer(new SnapshotProvider(target, false, single), new SnapshotProvider(collection, false, collectionSource), expected);
        VerifySingle(target, expected, false, false, single);
        VerifyCollection(typeof(ImageCollectionClassicDebuggerVisualizer), collection, _ => expected, false, 1, 0, collectionSource);
        Console.WriteLine("PASS: " + name + " / " + collection.GetType().Name + "; root/child/page, wire chunk and preview, single/collection Show, exact layout and bytes.");
    }

    private static void VerifyWireTransfer(SnapshotProvider single, SnapshotProvider collection, RawBufferSnapshot expected)
    {
        collection.TransferDeserializableObject(new VisualizerCollectionItemRequest
        {
            Operation = VisualizerCollectionOperation.ConfigureMappings, Mappings = new TypeMappingSnapshot()
        });
        var singleMetadata = single.GetDeserializableObject().ToObject<VisualizerSnapshotMetadata>();
        var item = collection.TransferDeserializableObject(new VisualizerCollectionItemRequest { Operation = VisualizerCollectionOperation.Metadata })
            .ToObject<VisualizerCollectionItemMetadata>();
        var collectionMetadata = item.Metadata ?? throw new InvalidOperationException(item.Error);
        VerifyLayout(singleMetadata.Descriptor, expected.Descriptor);
        VerifyLayout(collectionMetadata.Descriptor, expected.Descriptor);
        Require(singleMetadata.BufferLength == expected.Buffer.LongLength && collectionMetadata.BufferLength == expected.Buffer.LongLength
            && singleMetadata.SourceType == collectionMetadata.SourceType && singleMetadata.SupportsDirectMemory == collectionMetadata.SupportsDirectMemory,
            "Single and collection metadata disagree.");

        var directPreview = single.TransferDeserializableObject(new VisualizerSnapshotChunkRequest
        {
            Operation = VisualizerSnapshotOperation.Preview, MaximumWidth = 2, MaximumHeight = 2
        }).ToObject<VisualizerSnapshotTransfer>();
        var itemPreview = collection.TransferDeserializableObject(new VisualizerCollectionItemRequest
        {
            Operation = VisualizerCollectionOperation.Preview, MaximumWidth = 2, MaximumHeight = 2
        }).ToObject<VisualizerSnapshotTransfer>();
        VerifyLayout(directPreview.Descriptor, itemPreview.Descriptor);
        Require(directPreview.Buffer.Length > 0 && directPreview.Buffer.SequenceEqual(itemPreview.Buffer), "Single and collection preview pixels disagree.");

        var directChunk = single.TransferDeserializableObject(new VisualizerSnapshotChunkRequest { Count = expected.Buffer.Length })
            .ToObject<VisualizerSnapshotChunk>();
        var itemChunk = collection.TransferDeserializableObject(new VisualizerCollectionItemRequest
        {
            Operation = VisualizerCollectionOperation.Chunk, Count = expected.Buffer.Length
        }).ToObject<VisualizerSnapshotChunk>();
        Require(directChunk.IsLastChunk && itemChunk.IsLastChunk && directChunk.Buffer.SequenceEqual(expected.Buffer)
            && itemChunk.Buffer.SequenceEqual(expected.Buffer), "Single or collection chunk changed the input bytes.");
    }

    private static VisualizerObjectSource ResolveObjectSource(object value, Type uiType, string expectedSource)
    {
        var assembly = typeof(RawBufferSnapshotVisualizerObjectSource).Assembly;
        var expectedTypeName = "RawBufferVisualizer.VisualStudio.ObjectSource." + expectedSource;
        foreach (var entry in new[] { "root", "children", "items" })
        {
            var candidate = DebuggerResultProviderTests.GetCandidate(value, entry);
            Require(candidate.DebuggeeSideVisualizerTypeName == expectedTypeName && candidate.DebuggeeSideVisualizerAssemblyName == assembly.GetName().Name
                && candidate.UISideVisualizerTypeName == uiType.FullName, "Wrong visualizer for " + value.GetType() + " via " + entry);
        }
        return (VisualizerObjectSource)Activator.CreateInstance(assembly.GetType(expectedTypeName, true)!)!;
    }

    private static void VerifyNativeSnapshotCapture()
    {
        var descriptor = new RawImageDescriptor { Width = 3, Height = 262146, Stride = 4, PixelFormat = RawPixelFormat.Mono8, ValidBits = 8 };
        var expected = Enumerable.Range(0, descriptor.Stride * descriptor.Height).Select(index => (byte)(index % 251)).ToArray();
        var address = System.Runtime.InteropServices.Marshal.AllocHGlobal(expected.Length);
        var directoriesBefore = Directory.GetDirectories(VisualStudioTempStore.RootDirectory).OrderBy(path => path).ToArray();
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(expected, 0, address, expected.Length);
            var metadata = VisualizerChunkedTransfer.CreatePointerMetadata(descriptor, expected.Length, address, "native-fixture");
            Func<VisualizerSnapshotChunkRequest, VisualizerSnapshotChunk> rejectChunk = _ => throw new InvalidOperationException("Native capture must not call the paused-target chunk transport.");
            var path = VisualizerSnapshotStore.WriteSnapshot(metadata, rejectChunk);
            try
            {
                var captured = RawBufferSnapshot.Load(path);
                Require(captured.Buffer.SequenceEqual(expected), "Native bytes, padding or the 1 MiB copy boundary changed.");
                Require(captured.Descriptor.Width == descriptor.Width && captured.Descriptor.Height == descriptor.Height
                    && captured.Descriptor.Stride == descriptor.Stride, "Native snapshot descriptor changed.");
            }
            finally { VisualStudioTempStore.TryDeleteSnapshotDirectoryForMetadata(path); }

            metadata.BufferAddress = 1;
            metadata.SupportsDirectMemory = false;
            int chunkCalls = 0;
            path = VisualizerSnapshotStore.WriteSnapshot(metadata, request =>
            {
                chunkCalls++;
                return VisualizerChunkedTransfer.CreateChunk(expected, request);
            });
            try { Require(chunkCalls == 1 && RawBufferSnapshot.Load(path).Buffer.SequenceEqual(expected), "A captured pointer label must not enable native reads without direct-memory capability."); }
            finally { VisualStudioTempStore.TryDeleteSnapshotDirectoryForMetadata(path); }

            metadata.SupportsDirectMemory = true;
            bool failed = false;
            try { VisualizerSnapshotStore.WriteSnapshot(metadata, rejectChunk); }
            catch (RawImageSourceUnavailableException) { failed = true; }
            Require(failed, "Unreadable native memory must fail instead of using target-side fallback.");
            Require(Directory.GetDirectories(VisualStudioTempStore.RootDirectory).OrderBy(item => item).SequenceEqual(directoriesBefore), "Failed native capture leaked snapshot artifacts.");

            metadata.BufferAddress = address.ToInt64();
            path = VisualizerSnapshotStore.WriteSnapshot(metadata, rejectChunk);
            try { Require(RawBufferSnapshot.Load(path).Buffer.SequenceEqual(expected), "Valid capture did not recover after an unreadable pointer."); }
            finally { VisualStudioTempStore.TryDeleteSnapshotDirectoryForMetadata(path); }
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(address); }
        Console.WriteLine("PASS: native snapshot exact bytes/padding across 1 MiB, zero target chunk calls, invalid-pointer cleanup and recovery.");
    }

    private static void VerifyLargeHandoff(long bytes, bool previewExpected)
    {
        var address = System.Runtime.InteropServices.Marshal.AllocHGlobal(1);
        try
        {
            var metadata = new VisualizerSnapshotMetadata
            {
                Descriptor = new RawImageDescriptor { Width = (int)(bytes / 1024), Height = 1024, Stride = (int)(bytes / 1024), PixelFormat = RawPixelFormat.Mono8, ValidBits = 8 },
                BufferLength = bytes,
                ChunkSize = 1024,
                SourceType = typeof(RawBufferView).FullName!,
                DisplayName = "large-image",
                SupportsDirectMemory = true,
                ProcessId = Process.GetCurrentProcess().Id,
                BufferAddress = address.ToInt64()
            };
            var preview = new VisualizerSnapshotTransfer
            {
                Descriptor = new RawImageDescriptor { Width = 2, Height = 2, Stride = 2, PixelFormat = RawPixelFormat.Mono8, ValidBits = 8 },
                Buffer = new byte[] { 1, 2, 3, 4 }
            };
            var dialogs = new RejectingDialogService();
            int wakes = 0;
            Action<int> wake = processId =>
            {
                wakes++;
                var paths = Directory.GetFiles(VisualizerHandoffInbox.GetInboxDirectory(processId), "*.rbuf-handoff");
                Require(paths.Length == (previewExpected ? 2 : 1), "Expected preview/full handoff count.");
                string? id = null;
                int previews = 0, lives = 0;
                foreach (var path in paths)
                {
                    var request = VisualizerHandoffInbox.ReadSnapshotRequestInfo(path);
                    try
                    {
                        Require(!request.IsError && !string.IsNullOrEmpty(request.HandoffId), "Large handoff must be successful and identifiable.");
                        if (id == null) id = request.HandoffId;
                        Require(id == request.HandoffId, "Preview and full source must replace one row.");
                        if (request.IsPreview)
                        {
                            previews++;
                            var stored = RawBufferSnapshot.Load(request.MetadataPath);
                            Require(stored.Buffer.SequenceEqual(preview.Buffer), "Sampled preview bytes changed.");
                        }
                        else
                        {
                            lives++;
                            Require(request.IsLiveMemory && request.LiveProcessId == processId && request.LiveBufferAddress == address.ToInt64()
                                && request.LiveBufferLength == bytes, "Large source did not preserve its direct-memory descriptor.");
                        }
                    }
                    finally
                    {
                        if (request.IsPreview) VisualStudioTempStore.TryDeleteSnapshotDirectoryForMetadata(request.MetadataPath);
                        VisualizerHandoffInbox.TryDeleteRequest(path);
                    }
                }
                Require(previews == (previewExpected ? 1 : 0) && lives == 1, "Unexpected preview/direct source sequence.");
            };
            var visualizer = (RawBufferClassicDebuggerVisualizer)Activator.CreateInstance(
                typeof(RawBufferClassicDebuggerVisualizer), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { wake }, null)!;
            typeof(RawBufferClassicDebuggerVisualizer).GetMethod("Show", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(visualizer, new object[] { dialogs, new SnapshotProvider(metadata, preview) });
            Require(wakes == 1 && dialogs.Calls == 0, "Large handoff must wake only the docked viewer.");
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(address);
        }
    }

    private static void VerifyLargeCollectionHandoff()
    {
        const long bytes = 64L * 1024 * 1024;
        var address = System.Runtime.InteropServices.Marshal.AllocHGlobal(1);
        try
        {
            var metadata = new VisualizerSnapshotMetadata
            {
                Descriptor = new RawImageDescriptor { Width = 65536, Height = 1024, Stride = 65536, PixelFormat = RawPixelFormat.Mono8, ValidBits = 8 },
                BufferLength = bytes, ChunkSize = 1024, SourceType = typeof(RawBufferView).FullName!, DisplayName = "[0]",
                SupportsDirectMemory = true, ProcessId = Process.GetCurrentProcess().Id, BufferAddress = address.ToInt64()
            };
            var preview = new VisualizerSnapshotTransfer
            {
                Descriptor = new RawImageDescriptor { Width = 2, Height = 2, Stride = 2, PixelFormat = RawPixelFormat.Mono8, ValidBits = 8 },
                Buffer = new byte[] { 1, 2, 3, 4 }
            };
            var type = typeof(RawBufferClassicDebuggerVisualizer).Assembly.GetType("RawBufferVisualizer.VisualStudio.Classic.ImageCollectionClassicDebuggerVisualizer", true)!;
            var dialogs = new RejectingDialogService();
            var wakes = 0;
            Action<int> wake = processId =>
            {
                wakes++;
                var paths = Directory.GetFiles(VisualizerHandoffInbox.GetInboxDirectory(processId), "*.rbuf-handoff");
                Require(paths.Length == 2, "Large collection must publish preview and live-memory requests.");
                string? id = null;
                var previews = 0;
                var lives = 0;
                foreach (var path in paths)
                {
                    var request = VisualizerHandoffInbox.ReadSnapshotRequestInfo(path);
                    try
                    {
                        Require(!request.IsError && !string.IsNullOrEmpty(request.HandoffId), "Large collection item needs one identity.");
                        if (id == null) id = request.HandoffId;
                        Require(id == request.HandoffId, "Collection preview and live source must share one row.");
                        if (request.IsPreview)
                        {
                            previews++;
                            Require(RawBufferSnapshot.Load(request.MetadataPath).Buffer.SequenceEqual(preview.Buffer), "Collection preview bytes changed.");
                        }
                        else
                        {
                            lives++;
                            Require(request.IsLiveMemory && request.LiveBufferAddress == address.ToInt64()
                                && request.LiveBufferLength == bytes, "Collection direct-memory descriptor changed.");
                        }
                    }
                    finally
                    {
                        if (request.IsPreview) VisualStudioTempStore.TryDeleteSnapshotDirectoryForMetadata(request.MetadataPath);
                        VisualizerHandoffInbox.TryDeleteRequest(path);
                    }
                }
                Require(previews == 1 && lives == 1, "Large collection handoff kinds changed.");
            };
            var visualizer = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { (Func<int, TypeMappingSnapshot>)(_ => new TypeMappingSnapshot()), wake }, null)!;
            type.GetMethod("Show", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(visualizer,
                new object[] { dialogs, new SnapshotProvider(metadata, preview, true) });
            Require(wakes == 1 && dialogs.Calls == 0, "Large collection must wake only the docked viewer.");
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(address);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class RejectingDialogService : IDialogVisualizerService
    {
        public int Calls { get; private set; }
        private DialogResult Reject() { Calls++; throw new InvalidOperationException("Helper UI is forbidden."); }
        public DialogResult ShowDialog(Form dialog) => Reject();
        public DialogResult ShowDialog(CommonDialog dialog) => Reject();
        public DialogResult ShowDialog(Control control) => Reject();
    }

    private sealed class SnapshotProvider : IVisualizerObjectProvider2
    {
        private readonly object _snapshot;
        private readonly bool _failChunk;
        private readonly VisualizerObjectSource _source;
        private readonly VisualizerSnapshotMetadata? _largeMetadata;
        private readonly VisualizerSnapshotTransfer? _preview;
        private readonly bool _largeCollection;

        public SnapshotProvider(object snapshot, bool failChunk, VisualizerObjectSource? source = null)
        {
            _snapshot = snapshot;
            _failChunk = failChunk;
            _source = source ?? (snapshot is RawBufferSnapshot ? (VisualizerObjectSource)new RawBufferSnapshotVisualizerObjectSource() : new ImageCollectionVisualizerObjectSource());
        }
        public SnapshotProvider(VisualizerSnapshotMetadata metadata, VisualizerSnapshotTransfer preview, bool collection = false)
        {
            _snapshot = new object();
            _source = new RawBufferSnapshotVisualizerObjectSource();
            _largeMetadata = metadata;
            _preview = preview;
            _largeCollection = collection;
        }
        public bool IsObjectReplaceable => false;
        public bool IsBinaryFormatterSupported => false;
        public IDeserializableObject GetDeserializableObject()
        {
            if (_largeMetadata != null) return new JsonObject(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
                _largeCollection ? (object)new VisualizerCollectionSummary { TotalCount = 1, ItemCount = 1, SourceType = "image-list" } : _largeMetadata)));
            using (var stream = new MemoryStream())
            {
                _source.GetData(_snapshot, stream);
                return new JsonObject(stream.ToArray());
            }
        }
        public IDeserializableObject TransferDeserializableObject(object request)
        {
            if (_largeMetadata != null)
            {
                if (_largeCollection && request is VisualizerCollectionItemRequest collectionItem)
                {
                    if (collectionItem.Operation == VisualizerCollectionOperation.ConfigureMappings)
                        return new JsonObject(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
                            new VisualizerCollectionSummary { TotalCount = 1, ItemCount = 1, SourceType = "image-list", ExpressionIdentityHash = 5 })));
                    if (collectionItem.Operation == VisualizerCollectionOperation.Metadata)
                        return new JsonObject(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
                            new VisualizerCollectionItemMetadata { Index = 0, DisplayName = "[0]", Metadata = _largeMetadata })));
                    if (collectionItem.Operation == VisualizerCollectionOperation.Preview)
                        return new JsonObject(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_preview)));
                }
                if (request is VisualizerSnapshotChunkRequest chunk && chunk.Operation == VisualizerSnapshotOperation.Preview)
                    return new JsonObject(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_preview)));
                throw new InvalidOperationException("Large direct-memory path requested a copied chunk.");
            }
            if (_failChunk && (!(request is VisualizerCollectionItemRequest item) || item.Operation == VisualizerCollectionOperation.Chunk))
                throw new IOException("Injected chunk transfer failure.");
            using (var input = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request))))
            using (var output = new MemoryStream())
            {
                _source.TransferData(_snapshot, input, output);
                return new JsonObject(output.ToArray());
            }
        }
        public Stream GetData() => throw new NotSupportedException();
        public object GetObject() => throw new NotSupportedException();
        public void ReplaceData(Stream data) => throw new NotSupportedException();
        public void ReplaceObject(object data) => throw new NotSupportedException();
        public Stream TransferData(Stream data) => throw new NotSupportedException();
        public object TransferObject(object data) => throw new NotSupportedException();
        public void Serialize(object value, Stream stream) => throw new NotSupportedException();
        public object Deserialize(Stream stream) => throw new NotSupportedException();
        public IDeserializableObject GetDeserializableObjectFrom(Stream stream) => throw new NotSupportedException();
    }

    private sealed class JsonObject : IDeserializableObject
    {
        private readonly byte[] _json;
        public JsonObject(byte[] json) { _json = json; }
        public bool IsBinaryFormat => false;
        public string GetJsonStringPropertyValue(string name) => throw new NotSupportedException();
        public object ToObject(Type type)
        {
            return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(_json), type)!;
        }
        public T ToObject<T>() => (T)ToObject(typeof(T));
    }
}
