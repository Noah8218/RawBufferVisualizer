using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using OpenCvSharp;
using RawBufferVisualizer.BitmapAdapter;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.OpenCvSharpAdapter;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.VisualStudio;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.Tests
{
    internal static class Program
    {
        private static readonly Type LegacyOpenCvSharpMatRuntimeType = CreateLegacyOpenCvSharpMatRuntimeType();

        private static int Main()
        {
            try
            {
                Mono8RendersToBgra();
                Mono10PackedLsbRenders();
                Mono12PackedLsbInspects();
                Rgb24KeepsChannelOrder();
                Bgr24KeepsChannelOrder();
                Bgra32KeepsChannelOrder();
                BayerRggbRendersColor();
                AllSupportedFormatsRender();
                TilePlannerSplitsLargeImage();
                TilePlannerHandles100kImage();
                TilePlannerHandles200kImage();
                TileRenderMatchesFullRender();
                FileBackedSourceRendersLikeMemory();
                ProcessMemorySourceRendersLikeMemory();
                ProcessMemorySourceReportsUnavailableAfterProcessExit();
                FileBackedSampledRenderHonorsCancellation();
                SampledMemorySourceMatchesFileBackedSourceForAllFormats();
                FileBackedPackedSourceRendersLikeMemory();
                FileBackedSourceHandles100kSampledTile();
                FileBackedSourceHandles200kSampledTile();
                FileBackedPackedSourceHandles100kSampledTile();
                DifferenceSourceRendersAbsDiff();
                SplitSourceRendersLeftAndRight();
                InvalidStrideIsReported();
                DiagnosticsReportPaddingAndExpectedBytes();
                PixelInspectorReportsRawBytes();
                SnapshotRoundTrips();
                SnapshotReferenceLoadsMetadata();
                SnapshotReferenceLoadsUtf8BomPrettyMetadata();
                VisualizerTransferRoundTrips();
                VisualizerChunkedTransferCreatesChunks();
                VisualizerSampledPreviewSamplesByteAndPointerSources();
                VisualizerSnapshotStoreWritesChunkedSnapshot();
                VisualizerSnapshotStoreWritesCollection();
                RawBufferViewCreatesDescriptorAndChunks();
                ImagePtrVisualizerObjectSourceCreatesChunks();
                BitmapVisualizerObjectSourceCreatesTransfer();
                MatVisualizerObjectSourceCreatesChunks();
                OpenCvSharpMatVisualizerObjectSourceSupportsLegacyMatWithoutDims();
                EmguCvMatVisualizerObjectSourceCreatesChunks();
                ImageCollectionVisualizerHandlesListArrayAndDictionary();
                VisualizerBridgeWritesLaunchSnapshot();
                VisualizerBridgePreparesChunkedLaunchSnapshot();
                VisualizerBridgePreparesMultiLaunchSnapshots();
                ViewerPathResolverFindsConfiguredViewer();
                VisualizerHandoffInboxRoutesRequestsByVisualStudioInstance();
                VisualizerSupportReportContainsActionableContextWithoutImageData();
                VisualStudioTempStoreDeletesOwnedSnapshotDirectories();
                VisualStudioTempStoreReportsRootByteCount();
                BitmapAdapterCreatesSnapshot();
                MatAdapterCreatesSnapshot();
                Console.WriteLine("RawBufferVisualizer self-tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void Mono8RendersToBgra()
        {
            var descriptor = new RawImageDescriptor
            {
                Width = 2,
                Height = 1,
                Stride = 2,
                PixelFormat = RawPixelFormat.Mono8,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };

            var rendered = RawBufferRenderer.Render(new byte[] { 10, 250 }, descriptor);
            Assert(rendered.Bgra32[0] == 10 && rendered.Bgra32[1] == 10 && rendered.Bgra32[2] == 10 && rendered.Bgra32[3] == 255, "Mono8 first pixel failed.");
            Assert(rendered.Bgra32[4] == 250 && rendered.Bgra32[5] == 250 && rendered.Bgra32[6] == 250 && rendered.Bgra32[7] == 255, "Mono8 second pixel failed.");
        }

        private static void Bgr24KeepsChannelOrder()
        {
            var descriptor = new RawImageDescriptor
            {
                Width = 1,
                Height = 1,
                Stride = 3,
                PixelFormat = RawPixelFormat.BGR24,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };

            var rendered = RawBufferRenderer.Render(new byte[] { 1, 2, 3 }, descriptor);
            Assert(rendered.Bgra32[0] == 1 && rendered.Bgra32[1] == 2 && rendered.Bgra32[2] == 3 && rendered.Bgra32[3] == 255, "BGR24 channel order failed.");
        }

        private static void Rgb24KeepsChannelOrder()
        {
            var descriptor = new RawImageDescriptor
            {
                Width = 1,
                Height = 1,
                Stride = 3,
                PixelFormat = RawPixelFormat.RGB24,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };

            var rendered = RawBufferRenderer.Render(new byte[] { 3, 2, 1 }, descriptor);
            Assert(rendered.Bgra32[0] == 1 && rendered.Bgra32[1] == 2 && rendered.Bgra32[2] == 3 && rendered.Bgra32[3] == 255, "RGB24 channel order failed.");
        }

        private static void Bgra32KeepsChannelOrder()
        {
            var descriptor = new RawImageDescriptor
            {
                Width = 1,
                Height = 1,
                Stride = 4,
                PixelFormat = RawPixelFormat.BGRA32,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };

            var rendered = RawBufferRenderer.Render(new byte[] { 1, 2, 3, 4 }, descriptor);
            Assert(rendered.Bgra32[0] == 1 && rendered.Bgra32[1] == 2 && rendered.Bgra32[2] == 3 && rendered.Bgra32[3] == 4, "BGRA32 channel order failed.");
        }

        private static void BayerRggbRendersColor()
        {
            var descriptor = new RawImageDescriptor
            {
                Width = 3,
                Height = 3,
                Stride = 3,
                PixelFormat = RawPixelFormat.BayerRGGB8,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };
            var buffer = new byte[]
            {
                240, 120, 240,
                120, 40, 120,
                240, 120, 240
            };

            var rendered = RawBufferRenderer.Render(buffer, descriptor);
            var center = ((1 * descriptor.Width) + 1) * 4;
            Assert(rendered.Bgra32[center] == 40, "Bayer blue channel failed.");
            Assert(rendered.Bgra32[center + 1] == 120, "Bayer green channel failed.");
            Assert(rendered.Bgra32[center + 2] == 240, "Bayer red channel failed.");
            Assert(rendered.Bgra32[center + 3] == 255, "Bayer alpha channel failed.");
        }

        private static void AllSupportedFormatsRender()
        {
            foreach (RawPixelFormat format in Enum.GetValues(typeof(RawPixelFormat)))
            {
                var sample = CreateTinySample(format);
                var rendered = RawBufferRenderer.Render(sample.Buffer, sample.Descriptor);
                Assert(rendered.Width == sample.Descriptor.Width && rendered.Height == sample.Descriptor.Height, format + " render dimensions failed.");
                Assert(rendered.Bgra32.Length == sample.Descriptor.Width * sample.Descriptor.Height * 4, format + " render size failed.");
            }
        }

        private static void TilePlannerSplitsLargeImage()
        {
            var tiles = RawImageTilePlanner.CreateTiles(16384, 16384);
            Assert(tiles.Count == 16, "16K image should split into 16 display tiles.");
            Assert(tiles[0].X == 0 && tiles[0].Y == 0 && tiles[0].Width == 5000 && tiles[0].Height == 5000, "First tile bounds failed.");
            Assert(tiles[15].X == 15000 && tiles[15].Y == 15000 && tiles[15].Width == 1384 && tiles[15].Height == 1384, "Last tile bounds failed.");

            var descriptor = new RawImageDescriptor
            {
                Width = 16384,
                Height = 16384,
                Stride = 16384,
                PixelFormat = RawPixelFormat.Mono8,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };
            Assert(RawImageTilePlanner.EstimateBgraByteCount(descriptor) == 1073741824L, "BGRA memory estimate failed.");
        }

        private static void TilePlannerHandles100kImage()
        {
            var tiles = RawImageTilePlanner.CreateTiles(100000, 100000);
            Assert(tiles.Count == 400, "100K image should split into 400 display tiles.");
            Assert(tiles[0].X == 0 && tiles[0].Y == 0 && tiles[0].Width == 5000 && tiles[0].Height == 5000, "100K first tile bounds failed.");
            Assert(tiles[399].X == 95000 && tiles[399].Y == 95000 && tiles[399].Width == 5000 && tiles[399].Height == 5000, "100K last tile bounds failed.");

            var descriptor = new RawImageDescriptor
            {
                Width = 100000,
                Height = 100000,
                Stride = 100000,
                PixelFormat = RawPixelFormat.Mono8,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };

            Assert(descriptor.GetRequiredByteCount() == 10000000000L, "100K Mono8 source byte estimate failed.");
            Assert(RawImageTilePlanner.EstimateBgraByteCount(descriptor) == 40000000000L, "100K BGRA memory estimate failed.");
        }

        private static void TilePlannerHandles200kImage()
        {
            var tiles = RawImageTilePlanner.CreateTiles(200000, 200000);
            Assert(tiles.Count == 1600, "200K image should split into 1600 display tiles.");
            Assert(tiles[0].X == 0 && tiles[0].Y == 0 && tiles[0].Width == 5000 && tiles[0].Height == 5000, "200K first tile bounds failed.");
            Assert(tiles[1599].X == 195000 && tiles[1599].Y == 195000 && tiles[1599].Width == 5000 && tiles[1599].Height == 5000, "200K last tile bounds failed.");

            var descriptor = new RawImageDescriptor
            {
                Width = 200000,
                Height = 200000,
                Stride = 200000,
                PixelFormat = RawPixelFormat.Mono8,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };

            Assert(descriptor.GetRequiredByteCount() == 40000000000L, "200K Mono8 source byte estimate failed.");
            Assert(RawImageTilePlanner.EstimateBgraByteCount(descriptor) == 160000000000L, "200K BGRA memory estimate failed.");
        }

        private static void TileRenderMatchesFullRender()
        {
            var descriptor = new RawImageDescriptor
            {
                Width = 4,
                Height = 3,
                Stride = 4,
                PixelFormat = RawPixelFormat.Mono8,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };
            var buffer = new byte[]
            {
                1, 2, 3, 4,
                5, 6, 7, 8,
                9, 10, 11, 12
            };

            var full = RawBufferRenderer.Render(buffer, descriptor);
            var tile = RawBufferRenderer.RenderTile(buffer, descriptor, 1, 1, 2, 2);
            Assert(tile.Width == 2 && tile.Height == 2, "Tile render dimensions failed.");
            Assert(tile.Bgra32[0] == full.Bgra32[((1 * descriptor.Width) + 1) * 4], "Tile render first pixel failed.");
            Assert(tile.Bgra32[12] == full.Bgra32[((2 * descriptor.Width) + 2) * 4], "Tile render last pixel failed.");
        }

        private static void FileBackedSourceRendersLikeMemory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var rawPath = Path.Combine(directory, "bgr24-padding.raw");
                var descriptor = CreateDescriptor(4, 3, 14, RawPixelFormat.BGR24, 8);
                var buffer = new byte[descriptor.GetRequiredByteCount()];
                for (var y = 0; y < descriptor.Height; y++)
                {
                    var row = y * descriptor.Stride;
                    for (var x = 0; x < descriptor.Width; x++)
                    {
                        var offset = row + (x * 3);
                        buffer[offset] = (byte)x;
                        buffer[offset + 1] = (byte)y;
                        buffer[offset + 2] = (byte)(x + y);
                    }
                }

                File.WriteAllBytes(rawPath, buffer);

                using (var memorySource = RawImageSource.FromMemory(buffer, descriptor))
                using (var fileSource = RawImageSource.FromFile(rawPath, descriptor))
                {
                    var memoryTile = memorySource.RenderTile(1, 1, 2, 2, null);
                    var fileTile = fileSource.RenderTile(1, 1, 2, 2, null);
                    AssertBytesEqual(memoryTile.Bgra32, fileTile.Bgra32, "File-backed tile render should match memory render.");

                    var memorySampled = memorySource.RenderTileSampled(0, 0, 4, 3, 2, null);
                    var fileSampled = fileSource.RenderTileSampled(0, 0, 4, 3, 2, null);
                    AssertBytesEqual(memorySampled.Bgra32, fileSampled.Bgra32, "File-backed sampled render should match memory sampled render.");

                    var pixel = fileSource.DescribePixel(2, 1);
                    Assert(pixel.Contains("X=2, Y=1") && pixel.Contains("B=2") && pixel.Contains("G=1") && pixel.Contains("R=3"), "File-backed pixel inspector failed.");
                }
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void FileBackedSampledRenderHonorsCancellation()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var rawPath = Path.Combine(directory, "cancel.raw");
                var descriptor = CreateDescriptor(16, 16, 16, RawPixelFormat.Mono8, 8);
                File.WriteAllBytes(rawPath, new byte[descriptor.GetRequiredByteCount()]);

                using (var source = RawImageSource.FromFile(rawPath, descriptor))
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel();
                    var canceled = false;
                    try
                    {
                        source.RenderTileSampled(0, 0, 16, 16, 2, null, cancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        canceled = true;
                    }

                    Assert(canceled, "File-backed sampled render did not honor cancellation.");
                }
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void ProcessMemorySourceRendersLikeMemory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            var descriptor = CreateDescriptor(4, 3, 14, RawPixelFormat.BGR24, 8);
            var buffer = new byte[descriptor.GetRequiredByteCount()];
            for (var y = 0; y < descriptor.Height; y++)
            {
                var row = y * descriptor.Stride;
                for (var x = 0; x < descriptor.Width; x++)
                {
                    var offset = row + (x * 3);
                    buffer[offset] = (byte)(x + 10);
                    buffer[offset + 1] = (byte)(y + 20);
                    buffer[offset + 2] = (byte)(x + y + 30);
                }
            }

            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                Directory.CreateDirectory(directory);
                var copiedPath = Path.Combine(directory, "live-copy.raw");
                using (var memorySource = RawImageSource.FromMemory(buffer, descriptor))
                using (var processSource = RawImageSource.FromProcessMemory(
                    Process.GetCurrentProcess().Id,
                    handle.AddrOfPinnedObject().ToInt64(),
                    buffer.LongLength,
                    descriptor))
                {
                    Assert(processSource.IsLiveProcessBacked, "Process source should report live debugger memory.");
                    var memoryTile = memorySource.RenderTile(1, 1, 2, 2, null);
                    var processTile = processSource.RenderTile(1, 1, 2, 2, null);
                    AssertBytesEqual(memoryTile.Bgra32, processTile.Bgra32, "Process-memory tile render should match memory render.");

                    var memorySampled = memorySource.RenderTileSampled(0, 0, 4, 3, 2, null);
                    var processSampled = processSource.RenderTileSampled(0, 0, 4, 3, 2, null);
                    AssertBytesEqual(memorySampled.Bgra32, processSampled.Bgra32, "Process-memory sampled render should match memory render.");

                    var pixel = processSource.DescribePixel(2, 1);
                    Assert(pixel.Contains("X=2, Y=1") && pixel.Contains("B=12") && pixel.Contains("G=21") && pixel.Contains("R=33"), "Process-memory pixel inspector failed.");

                    processSource.CopyRawTo(copiedPath);
                    AssertBytesEqual(buffer, File.ReadAllBytes(copiedPath), "Process-memory raw export failed.");
                }
            }
            finally
            {
                handle.Free();
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void ProcessMemorySourceReportsUnavailableAfterProcessExit()
        {
            var commandProcessor = Environment.GetEnvironmentVariable("ComSpec");
            if (string.IsNullOrWhiteSpace(commandProcessor))
            {
                throw new InvalidOperationException("ComSpec is unavailable for the process-memory lifetime test.");
            }

            int processId;
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = commandProcessor,
                Arguments = "/c exit 0",
                CreateNoWindow = true,
                UseShellExecute = false
            }) ?? throw new InvalidOperationException("Lifetime-test process could not be started."))
            {
                processId = process.Id;
                process.WaitForExit();
            }

            var descriptor = CreateDescriptor(1, 1, 1, RawPixelFormat.Mono8, 8);
            using (var source = RawImageSource.FromProcessMemory(processId, 1, 1, descriptor))
            {
                RawImageSourceUnavailableException? failure = null;
                try
                {
                    source.RenderTile(0, 0, 1, 1, null);
                }
                catch (RawImageSourceUnavailableException ex)
                {
                    failure = ex;
                }

                Assert(failure != null, "Exited process memory should report a source-unavailable failure.");
                Assert(
                    failure!.Message.IndexOf("debuggee", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Source-unavailable failure should identify the debuggee source.");
            }
        }

        private static void SampledMemorySourceMatchesFileBackedSourceForAllFormats()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                foreach (RawPixelFormat format in Enum.GetValues(typeof(RawPixelFormat)))
                {
                    var sample = CreateTinySample(format);
                    var rawPath = Path.Combine(directory, format + ".raw");
                    File.WriteAllBytes(rawPath, sample.Buffer);

                    using (var memorySource = RawImageSource.FromMemory(sample.Buffer, sample.Descriptor))
                    using (var fileSource = RawImageSource.FromFile(rawPath, sample.Descriptor))
                    {
                        var options = memorySource.CreateRenderOptions();
                        var memorySampled = memorySource.RenderTileSampled(
                            0,
                            0,
                            sample.Descriptor.Width,
                            sample.Descriptor.Height,
                            2,
                            options);
                        var fileSampled = fileSource.RenderTileSampled(
                            0,
                            0,
                            sample.Descriptor.Width,
                            sample.Descriptor.Height,
                            2,
                            options);

                        Assert(memorySampled.Width == fileSampled.Width && memorySampled.Height == fileSampled.Height, format + " sampled dimensions failed.");
                        AssertBytesEqual(memorySampled.Bgra32, fileSampled.Bgra32, format + " sampled memory render should match file-backed render.");
                    }
                }
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void FileBackedPackedSourceRendersLikeMemory()
        {
            foreach (var format in new[] { RawPixelFormat.Mono10PackedLsb, RawPixelFormat.Mono12PackedLsb })
            {
                var bitsPerPixel = format == RawPixelFormat.Mono10PackedLsb ? 10 : 12;
                var maxValue = (1 << bitsPerPixel) - 1;
                var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
                try
                {
                    Directory.CreateDirectory(directory);
                    var rawPath = Path.Combine(directory, format + ".raw");
                    var descriptor = CreateDescriptor(17, 3, (17 * bitsPerPixel + 7) / 8, format, bitsPerPixel);
                    var buffer = CreatePackedRows(descriptor.Width, descriptor.Height, bitsPerPixel, maxValue);
                    File.WriteAllBytes(rawPath, buffer);

                    using (var memorySource = RawImageSource.FromMemory(buffer, descriptor))
                    using (var fileSource = RawImageSource.FromFile(rawPath, descriptor))
                    {
                        var options = fileSource.CreateRenderOptions();
                        var memoryTile = memorySource.RenderTile(3, 1, 9, 2, options);
                        var fileTile = fileSource.RenderTile(3, 1, 9, 2, options);
                        AssertBytesEqual(memoryTile.Bgra32, fileTile.Bgra32, format + " file-backed tile render should match memory render.");

                        var memorySampled = memorySource.RenderTileSampled(0, 0, 17, 3, 3, options);
                        var fileSampled = fileSource.RenderTileSampled(0, 0, 17, 3, 3, options);
                        AssertBytesEqual(memorySampled.Bgra32, fileSampled.Bgra32, format + " file-backed sampled render should match memory render.");

                        var pixel = fileSource.DescribePixel(16, 2);
                        Assert(pixel.Contains("X=16, Y=2") && pixel.Contains("Value=" + maxValue), format + " file-backed pixel inspector failed.");
                    }
                }
                finally
                {
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, true);
                    }
                }
            }
        }

        private static void FileBackedSourceHandles100kSampledTile()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var metadataPath = Path.Combine(directory, "huge-mono8.rbuf.json");
                var descriptor = CreateDescriptor(100000, 100000, 100000, RawPixelFormat.Mono8, 8);
                var rawPath = RawBufferSnapshot.SaveMetadata(metadataPath, descriptor);
                if (!CreateSparseFile(rawPath, descriptor.GetRequiredByteCount()))
                {
                    Console.WriteLine("Skipped 100K file-backed sparse raw test because this filesystem does not support sparse files.");
                    return;
                }

                var reference = RawBufferSnapshot.LoadReference(metadataPath);
                Assert(reference.RawByteLength == 10000000000L, "100K reference raw length failed.");

                using (var source = RawImageSource.FromFile(reference.RawPath, reference.Descriptor))
                {
                    var diagnostics = source.Analyze();
                    Assert(!RawBufferDiagnostics.HasErrors(diagnostics), "100K file-backed source diagnostics failed.");

                    var sampled = source.RenderTileSampled(95000, 95000, 5000, 5000, 64, source.CreateRenderOptions());
                    Assert(sampled.Width == 79 && sampled.Height == 79, "100K sampled tile dimensions failed.");
                    Assert(sampled.Bgra32.Length == 79 * 79 * 4, "100K sampled tile byte length failed.");

                    var pixel = source.DescribePixel(99999, 99999);
                    Assert(pixel.Contains("X=99999, Y=99999") && pixel.Contains("Value=33"), "100K file-backed pixel read failed.");
                }
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void FileBackedPackedSourceHandles100kSampledTile()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var metadataPath = Path.Combine(directory, "huge-mono10.rbuf.json");
                var descriptor = CreateDescriptor(100000, 100000, 125000, RawPixelFormat.Mono10PackedLsb, 10);
                var rawPath = RawBufferSnapshot.SaveMetadata(metadataPath, descriptor);
                if (!CreateSparseFile(rawPath, descriptor.GetRequiredByteCount()))
                {
                    Console.WriteLine("Skipped 100K packed file-backed sparse raw test because this filesystem does not support sparse files.");
                    return;
                }

                var reference = RawBufferSnapshot.LoadReference(metadataPath);
                Assert(reference.RawByteLength == 12500000000L, "100K packed reference raw length failed.");

                using (var source = RawImageSource.FromFile(reference.RawPath, reference.Descriptor))
                {
                    var diagnostics = source.Analyze();
                    Assert(!RawBufferDiagnostics.HasErrors(diagnostics), "100K packed file-backed source diagnostics failed.");

                    var sampled = source.RenderTileSampled(95000, 95000, 5000, 5000, 64, source.CreateRenderOptions());
                    Assert(sampled.Width == 79 && sampled.Height == 79, "100K packed sampled tile dimensions failed.");
                    Assert(sampled.Bgra32.Length == 79 * 79 * 4, "100K packed sampled tile byte length failed.");

                    var pixel = source.DescribePixel(99999, 99999);
                    Assert(pixel.Contains("X=99999, Y=99999") && pixel.Contains("Value="), "100K packed file-backed pixel read failed.");
                }
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void FileBackedSourceHandles200kSampledTile()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var metadataPath = Path.Combine(directory, "huge-200k-mono8.rbuf.json");
                var descriptor = CreateDescriptor(200000, 200000, 200000, RawPixelFormat.Mono8, 8);
                var rawPath = RawBufferSnapshot.SaveMetadata(metadataPath, descriptor);
                if (!CreateSparseFile(rawPath, descriptor.GetRequiredByteCount()))
                {
                    Console.WriteLine("Skipped 200K file-backed sparse raw test because this filesystem does not support sparse files.");
                    return;
                }

                var reference = RawBufferSnapshot.LoadReference(metadataPath);
                Assert(reference.RawByteLength == 40000000000L, "200K reference raw length failed.");

                using (var source = RawImageSource.FromFile(reference.RawPath, reference.Descriptor))
                {
                    var diagnostics = source.Analyze();
                    Assert(!RawBufferDiagnostics.HasErrors(diagnostics), "200K file-backed source diagnostics failed.");

                    var sampled = source.RenderTileSampled(195000, 195000, 5000, 5000, 64, source.CreateRenderOptions());
                    Assert(sampled.Width == 79 && sampled.Height == 79, "200K sampled tile dimensions failed.");
                    Assert(sampled.Bgra32.Length == 79 * 79 * 4, "200K sampled tile byte length failed.");

                    var pixel = source.DescribePixel(199999, 199999);
                    Assert(pixel.Contains("X=199999, Y=199999") && pixel.Contains("Value=33"), "200K file-backed pixel read failed.");
                }
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void Mono10PackedLsbRenders()
        {
            var descriptor = new RawImageDescriptor
            {
                Width = 4,
                Height = 1,
                Stride = 5,
                PixelFormat = RawPixelFormat.Mono10PackedLsb,
                ValidBits = 10,
                ByteOrder = RawByteOrder.LittleEndian
            };

            Assert(descriptor.GetMinimumStride() == 5, "Mono10 packed stride failed.");
            var rendered = RawBufferRenderer.Render(PackLsb(new[] { 0, 128, 512, 1023 }, 10), descriptor);
            Assert(rendered.Bgra32[0] == 0, "Mono10 packed black failed.");
            Assert(rendered.Bgra32[12] == 255, "Mono10 packed white failed.");
        }

        private static void Mono12PackedLsbInspects()
        {
            var descriptor = new RawImageDescriptor
            {
                Width = 2,
                Height = 1,
                Stride = 3,
                PixelFormat = RawPixelFormat.Mono12PackedLsb,
                ValidBits = 12,
                ByteOrder = RawByteOrder.LittleEndian
            };

            var text = RawPixelInspector.Describe(PackLsb(new[] { 7, 4095 }, 12), descriptor, 1, 0);
            Assert(text.Contains("Value=4095"), "Mono12 packed inspector failed.");
        }

        private static void DifferenceSourceRendersAbsDiff()
        {
            var descriptor = CreateDescriptor(2, 1, 2, RawPixelFormat.Mono8, 8);
            using (var a = RawImageSource.FromMemory(new byte[] { 10, 100 }, descriptor))
            using (var b = RawImageSource.FromMemory(new byte[] { 40, 90 }, descriptor))
            using (var diff = new RawImageDifferenceSource(a, b))
            {
                var rendered = diff.RenderTile(0, 0, 2, 1, null);
                Assert(rendered.Bgra32[0] == 30 && rendered.Bgra32[1] == 30 && rendered.Bgra32[2] == 30, "Diff first pixel failed.");
                Assert(rendered.Bgra32[4] == 10 && rendered.Bgra32[5] == 10 && rendered.Bgra32[6] == 10, "Diff second pixel failed.");
                Assert(diff.DescribePixel(0, 0).Contains("A=[") && diff.DescribePixel(0, 0).Contains("B=["), "Diff pixel describe failed.");
            }
        }

        private static void SplitSourceRendersLeftAndRight()
        {
            var descriptor = CreateDescriptor(4, 1, 4, RawPixelFormat.Mono8, 8);
            using (var a = RawImageSource.FromMemory(new byte[] { 10, 20, 30, 40 }, descriptor))
            using (var b = RawImageSource.FromMemory(new byte[] { 100, 110, 120, 130 }, descriptor))
            using (var split = new RawImageSplitSource(a, b))
            {
                var rendered = split.RenderTile(0, 0, 4, 1, null);
                Assert(rendered.Bgra32[0] == 10, "Split first A pixel failed.");
                Assert(rendered.Bgra32[4] == 20, "Split second A pixel failed.");
                Assert(rendered.Bgra32[8] == 120, "Split first B pixel failed.");
                Assert(rendered.Bgra32[12] == 130, "Split second B pixel failed.");
                Assert(split.DescribePixel(0, 0).Contains("A=[") && split.DescribePixel(3, 0).Contains("B=["), "Split pixel describe failed.");
            }
        }

        private static void InvalidStrideIsReported()
        {
            var descriptor = new RawImageDescriptor
            {
                Width = 2,
                Height = 1,
                Stride = 1,
                PixelFormat = RawPixelFormat.Mono8,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };

            var diagnostics = RawBufferDiagnostics.Analyze(new byte[] { 1, 2 }, descriptor);
            Assert(RawBufferDiagnostics.HasErrors(diagnostics), "Invalid stride should be an error.");
        }

        private static void DiagnosticsReportPaddingAndExpectedBytes()
        {
            var descriptor = CreateDescriptor(3, 2, 12, RawPixelFormat.BGR24, 8);
            var diagnostics = RawBufferDiagnostics.Analyze(new byte[24], descriptor);
            var text = string.Join("\n", diagnostics);
            Assert(text.Contains("padding 3 bytes/row"), "Padding diagnostic failed.");
            Assert(text.Contains("Expected image byte range: 21 bytes"), "Expected byte diagnostic failed.");
            Assert(text.Contains("trailing bytes"), "Trailing byte diagnostic failed.");
        }

        private static void PixelInspectorReportsRawBytes()
        {
            var descriptor = CreateDescriptor(1, 1, 3, RawPixelFormat.BGR24, 8);
            var text = RawPixelInspector.Describe(new byte[] { 3, 2, 1 }, descriptor, 0, 0);
            Assert(text.Contains("B=3") && text.Contains("G=2") && text.Contains("R=1"), "Pixel channel diagnostic failed.");
            Assert(text.Contains("Raw=03 02 01"), "Pixel raw byte diagnostic failed.");
        }

        private static void SnapshotRoundTrips()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            var metadataPath = Path.Combine(directory, "roundtrip.rbuf.json");
            var descriptor = new RawImageDescriptor
            {
                Width = 2,
                Height = 2,
                Stride = 2,
                PixelFormat = RawPixelFormat.Mono8,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };

            RawBufferSnapshot.Save(metadataPath, new byte[] { 1, 2, 3, 4 }, descriptor);
            var loaded = RawBufferSnapshot.Load(metadataPath);
            Assert(loaded.Descriptor.Width == 2 && loaded.Descriptor.Height == 2, "Snapshot descriptor roundtrip failed.");
            Assert(loaded.Buffer.Length == 4 && loaded.Buffer[3] == 4, "Snapshot buffer roundtrip failed.");
            Directory.Delete(directory, true);
        }

        private static void SnapshotReferenceLoadsMetadata()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            var metadataPath = Path.Combine(directory, "reference.rbuf.json");
            var descriptor = CreateDescriptor(2, 2, 2, RawPixelFormat.Mono8, 8);

            RawBufferSnapshot.Save(metadataPath, new byte[] { 1, 2, 3, 4 }, descriptor);
            var reference = RawBufferSnapshot.LoadReference(metadataPath);
            Assert(reference.Descriptor.Width == 2 && reference.Descriptor.Height == 2, "Snapshot reference descriptor failed.");
            Assert(reference.RawByteLength == 4, "Snapshot reference raw length failed.");
            Assert(Path.GetFullPath(metadataPath) == reference.MetadataPath, "Snapshot reference metadata path failed.");
            Assert(File.Exists(reference.RawPath), "Snapshot reference raw path failed.");
            Directory.Delete(directory, true);
        }

        private static void SnapshotReferenceLoadsUtf8BomPrettyMetadata()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var metadataPath = Path.Combine(directory, "pretty.rbuf.json");
                var rawPath = Path.Combine(directory, "pretty.raw");
                File.WriteAllBytes(rawPath, new byte[] { 1, 2, 3, 4 });

                var json = "{\r\n" +
                    "  \"rawFile\": \"pretty.raw\",\r\n" +
                    "  \"width\": 2,\r\n" +
                    "  \"height\": 2,\r\n" +
                    "  \"stride\": 2,\r\n" +
                    "  \"pixelFormat\": \"Mono8\",\r\n" +
                    "  \"validBits\": 8,\r\n" +
                    "  \"byteOrder\": \"LittleEndian\"\r\n" +
                    "}\r\n";
                var preamble = Encoding.UTF8.GetPreamble();
                var content = Encoding.UTF8.GetBytes(json);
                var bytes = new byte[preamble.Length + content.Length];
                Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
                Buffer.BlockCopy(content, 0, bytes, preamble.Length, content.Length);
                File.WriteAllBytes(metadataPath, bytes);

                var reference = RawBufferSnapshot.LoadReference(metadataPath);
                Assert(reference.RawByteLength == 4, "UTF-8 BOM snapshot reference raw length failed.");
                Assert(reference.Descriptor.Width == 2 && reference.Descriptor.PixelFormat == RawPixelFormat.Mono8, "UTF-8 BOM snapshot reference descriptor failed.");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void BitmapAdapterCreatesSnapshot()
        {
            using (var bitmap = new Bitmap(2, 1, PixelFormat.Format24bppRgb))
            {
                bitmap.SetPixel(0, 0, Color.FromArgb(10, 20, 30));
                bitmap.SetPixel(1, 0, Color.FromArgb(40, 50, 60));

                var snapshot = BitmapSnapshot.FromBitmap(bitmap);
                Assert(snapshot.Descriptor.Width == 2 && snapshot.Descriptor.Height == 1, "Bitmap dimensions failed.");
                Assert(snapshot.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Bitmap pixel format failed.");
                Assert(snapshot.Buffer.Length >= 6, "Bitmap buffer length failed.");
            }
        }

        private static void VisualizerTransferRoundTrips()
        {
            var descriptor = CreateDescriptor(2, 2, 2, RawPixelFormat.Mono8, 8);
            var snapshot = RawBufferSnapshot.FromByteArray(new byte[] { 1, 2, 3, 4 }, descriptor);
            var transfer = RawBufferSnapshotObjectSource.CreateTransfer(snapshot, "camera0");
            snapshot.Buffer[0] = 99;

            Assert(transfer.DisplayName == "camera0", "Visualizer transfer display name failed.");
            Assert(transfer.SourceType == typeof(RawBufferSnapshot).FullName, "Visualizer transfer source type failed.");
            Assert(transfer.Buffer[0] == 1, "Visualizer transfer should clone the source buffer.");
            Assert(transfer.Descriptor.Width == 2 && transfer.Descriptor.Height == 2, "Visualizer transfer descriptor failed.");

            var restored = transfer.ToSnapshot();
            Assert(restored.Buffer[3] == 4, "Visualizer transfer buffer roundtrip failed.");
            Assert(restored.Descriptor.PixelFormat == RawPixelFormat.Mono8, "Visualizer transfer descriptor roundtrip failed.");
        }

        private static void VisualizerChunkedTransferCreatesChunks()
        {
            var descriptor = CreateDescriptor(6, 1, 6, RawPixelFormat.Mono8, 8);
            var snapshot = RawBufferSnapshot.FromByteArray(new byte[] { 1, 2, 3, 4, 5, 6 }, descriptor);
            var transfer = RawBufferSnapshotObjectSource.CreateTransfer(snapshot, "chunked");
            var metadata = VisualizerChunkedTransfer.CreateMetadata(transfer);
            var chunk = VisualizerChunkedTransfer.CreateChunk(
                transfer,
                new VisualizerSnapshotChunkRequest
                {
                    Offset = 2,
                    Count = 3
                });

            Assert(metadata.BufferLength == 6, "Chunk metadata buffer length failed.");
            Assert(metadata.ChunkSize == VisualizerChunkedTransfer.DefaultChunkSize, "Chunk metadata size failed.");
            Assert(chunk.Offset == 2, "Chunk offset failed.");
            Assert(chunk.Buffer.Length == 3 && chunk.Buffer[0] == 3 && chunk.Buffer[2] == 5, "Chunk data failed.");
            Assert(!chunk.IsLastChunk, "Chunk last flag failed.");
        }

        private static void VisualizerSampledPreviewSamplesByteAndPointerSources()
        {
            var descriptor = CreateDescriptor(4, 2, 4, RawPixelFormat.Mono8, 8);
            var buffer = new byte[]
            {
                10, 20, 30, 40,
                50, 60, 70, 80
            };
            var preview = VisualizerSampledPreview.Create(
                buffer,
                descriptor,
                "Test.Mono8",
                "preview",
                2,
                1);

            Assert(preview.Descriptor.Width == 2 && preview.Descriptor.Height == 1, "Sampled byte preview dimensions failed.");
            Assert(preview.Descriptor.PixelFormat == RawPixelFormat.BGRA32, "Sampled byte preview format failed.");
            Assert(preview.Buffer[0] == 10 && preview.Buffer[4] == 30, "Sampled byte preview pixels failed.");

            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var pointerPreview = VisualizerSampledPreview.Create(
                    handle.AddrOfPinnedObject(),
                    buffer.LongLength,
                    descriptor,
                    "Test.Pointer",
                    "pointer",
                    2,
                    1);
                Assert(pointerPreview.Buffer.Length == preview.Buffer.Length, "Sampled pointer preview length failed.");
                Assert(pointerPreview.Buffer[0] == 10 && pointerPreview.Buffer[4] == 30, "Sampled pointer preview pixels failed.");
            }
            finally
            {
                handle.Free();
            }

            var packedDescriptor = CreateDescriptor(2, 1, 3, RawPixelFormat.Mono12PackedLsb, 12);
            var packed = PackLsb(new[] { 0, 4095 }, 12);
            var packedPreview = VisualizerSampledPreview.Create(
                packed,
                packedDescriptor,
                "Test.Packed",
                "packed",
                2,
                1);
            Assert(packedPreview.Buffer[0] == 0 && packedPreview.Buffer[4] == 255, "Sampled packed preview scaling failed.");
        }

        private static void VisualizerSnapshotStoreWritesChunkedSnapshot()
        {
            var buffer = new byte[] { 1, 2, 3, 4, 5, 6 };
            var metadata = new VisualizerSnapshotMetadata
            {
                Descriptor = CreateDescriptor(6, 1, 6, RawPixelFormat.Mono8, 8),
                BufferLength = buffer.Length,
                ChunkSize = 2,
                SourceType = "Test.Image",
                DisplayName = "chunked"
            };
            string? metadataPath = null;
            try
            {
                metadataPath = VisualizerSnapshotStore.WriteSnapshot(
                    metadata,
                    request =>
                    {
                        var length = Math.Min(request.Count, buffer.Length - checked((int)request.Offset));
                        var chunk = new byte[length];
                        Buffer.BlockCopy(buffer, checked((int)request.Offset), chunk, 0, length);
                        return new VisualizerSnapshotChunk
                        {
                            Offset = request.Offset,
                            Buffer = chunk,
                            TotalLength = buffer.Length,
                            IsLastChunk = request.Offset + length >= buffer.Length
                        };
                    });

                var restored = RawBufferSnapshot.Load(metadataPath);
                Assert(restored.Buffer.Length == buffer.Length, "Stored visualizer snapshot length failed.");
                Assert(restored.Buffer[0] == 1 && restored.Buffer[5] == 6, "Stored visualizer snapshot chunks failed.");
            }
            finally
            {
                if (metadataPath != null)
                {
                    VisualStudioTempStore.TryDeleteSnapshotDirectoryForMetadata(metadataPath);
                }
            }
        }

        private static void VisualizerSnapshotStoreWritesCollection()
        {
            var buffer = new byte[] { 7, 8, 9, 10 };
            var metadata = new VisualizerSnapshotMetadata
            {
                Descriptor = CreateDescriptor(4, 1, 4, RawPixelFormat.Mono8, 8),
                BufferLength = buffer.Length,
                ChunkSize = 2,
                SourceType = "Test.Image",
                DisplayName = "[0]"
            };
            var results = VisualizerSnapshotStore.WriteCollection(
                new VisualizerCollectionSummary
                {
                    TotalCount = 2,
                    ItemCount = 2,
                    SourceType = "Test.Collection"
                },
                index => index == 0
                    ? new VisualizerCollectionItemMetadata
                    {
                        Index = index,
                        DisplayName = "[0]",
                        Metadata = metadata
                    }
                    : new VisualizerCollectionItemMetadata
                    {
                        Index = index,
                        DisplayName = "[1]",
                        Error = "Unsupported collection image type."
                    },
                (index, request) =>
                {
                    Assert(index == 0, "Collection chunk requested for an error item.");
                    var length = Math.Min(request.Count, buffer.Length - checked((int)request.Offset));
                    var chunk = new byte[length];
                    Buffer.BlockCopy(buffer, checked((int)request.Offset), chunk, 0, length);
                    return new VisualizerSnapshotChunk
                    {
                        Offset = request.Offset,
                        Buffer = chunk,
                        TotalLength = buffer.Length,
                        IsLastChunk = request.Offset + length >= buffer.Length
                    };
                });

            try
            {
                Assert(results.Count == 2, "Collection snapshot result count failed.");
                Assert(!results[0].IsError && File.Exists(results[0].MetadataPath), "Collection success item was not stored.");
                Assert(results[1].IsError && results[1].ErrorMessage.Contains("Unsupported"), "Collection error item was not preserved.");
                var restored = RawBufferSnapshot.Load(results[0].MetadataPath);
                Assert(restored.Buffer[0] == 7 && restored.Buffer[3] == 10, "Collection stored snapshot data failed.");
            }
            finally
            {
                foreach (var result in results)
                {
                    if (!result.IsError)
                    {
                        VisualStudioTempStore.TryDeleteSnapshotDirectoryForMetadata(result.MetadataPath);
                    }
                }
            }
        }

        private static void RawBufferViewCreatesDescriptorAndChunks()
        {
            var buffer = new byte[] { 1, 2, 3, 4, 5, 6 };
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var view = new RawBufferView
                {
                    Buffer = handle.AddrOfPinnedObject(),
                    BufferLength = buffer.Length,
                    Width = 2,
                    Height = 1,
                    Stride = 6,
                    PixelFormat = RawPixelFormat.BGR24,
                    Channels = 3,
                    BitDepth = 8,
                    Name = "camera0"
                };

                var metadata = RawBufferViewVisualizerTransfer.CreateMetadata(view);
                Assert(metadata.DisplayName == "camera0", "RawBufferView display name failed.");
                Assert(metadata.BufferLength == 6, "RawBufferView buffer length failed.");
                Assert(metadata.Descriptor.PixelFormat == RawPixelFormat.BGR24, "RawBufferView pixel format failed.");
                Assert(metadata.SupportsDirectMemory, "RawBufferView should advertise direct debugger memory.");
                Assert(metadata.ProcessId == Process.GetCurrentProcess().Id, "RawBufferView direct-memory process ID failed.");
                Assert(metadata.BufferAddress == view.Buffer.ToInt64(), "RawBufferView direct-memory address failed.");

                var chunk = RawBufferViewVisualizerTransfer.CreateChunk(
                    view,
                    new VisualizerSnapshotChunkRequest
                    {
                        Offset = 2,
                        Count = 3
                    });
                Assert(chunk.Buffer.Length == 3 && chunk.Buffer[0] == 3 && chunk.Buffer[2] == 5, "RawBufferView chunk failed.");

                var snapshot = view.ToSnapshot();
                Assert(snapshot.Buffer[5] == 6, "RawBufferView snapshot copy failed.");
            }
            finally
            {
                handle.Free();
            }
        }

        private static void BitmapVisualizerObjectSourceCreatesTransfer()
        {
            using (var bitmap = new Bitmap(2, 1, PixelFormat.Format8bppIndexed))
            {
                var transfer = BitmapVisualizerTransfer.CreateTransfer((object)bitmap, "bitmapMono8");

                Assert(transfer.DisplayName == "bitmapMono8", "Bitmap visualizer Mono8 display name failed.");
                Assert(transfer.SourceType == typeof(Bitmap).FullName, "Bitmap visualizer Mono8 source type failed.");
                Assert(transfer.Descriptor.Width == 2 && transfer.Descriptor.Height == 1, "Bitmap visualizer Mono8 dimensions failed.");
                Assert(transfer.Descriptor.PixelFormat == RawPixelFormat.Mono8, "Bitmap visualizer Mono8 pixel format failed.");
                Assert(transfer.Buffer.Length >= 2, "Bitmap visualizer Mono8 buffer length failed.");
            }

            using (var bitmap = new Bitmap(2, 1, PixelFormat.Format24bppRgb))
            {
                bitmap.SetPixel(0, 0, Color.FromArgb(10, 20, 30));
                bitmap.SetPixel(1, 0, Color.FromArgb(40, 50, 60));

                var view = BitmapVisualizerTransfer.CreateView((object)bitmap, "bitmap-view");
                var metadata = BitmapVisualizerTransfer.CreateMetadata(view);
                var preview = BitmapVisualizerTransfer.CreatePreview(
                    view,
                    new VisualizerSnapshotChunkRequest
                    {
                        Operation = VisualizerSnapshotOperation.Preview,
                        MaximumWidth = 2,
                        MaximumHeight = 1
                    });
                var chunk = BitmapVisualizerTransfer.CreateChunk(
                    view,
                    new VisualizerSnapshotChunkRequest
                    {
                        Offset = 0,
                        Count = 6
                    });
                var transfer = BitmapVisualizerTransfer.CreateTransfer((object)bitmap, "bitmap0");

                Assert(metadata.BufferLength >= 6, "Bitmap visualizer lazy metadata length failed.");
                Assert(preview.Buffer[0] == 30 && preview.Buffer[1] == 20 && preview.Buffer[2] == 10, "Bitmap sampled preview channel order failed.");
                Assert(chunk.Buffer[0] == 30 && chunk.Buffer[1] == 20 && chunk.Buffer[2] == 10, "Bitmap lazy chunk channel order failed.");
                Assert(transfer.DisplayName == "bitmap0", "Bitmap visualizer display name failed.");
                Assert(transfer.SourceType == typeof(Bitmap).FullName, "Bitmap visualizer source type failed.");
                Assert(transfer.Descriptor.Width == 2 && transfer.Descriptor.Height == 1, "Bitmap visualizer dimensions failed.");
                Assert(transfer.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Bitmap visualizer pixel format failed.");
                Assert(transfer.Buffer.Length >= 6, "Bitmap visualizer buffer length failed.");
            }

            using (var bitmap = new Bitmap(2, 1, PixelFormat.Format32bppArgb))
            {
                var transfer = BitmapVisualizerTransfer.CreateTransfer((object)bitmap, "bitmapBgra32");

                Assert(transfer.DisplayName == "bitmapBgra32", "Bitmap visualizer BGRA32 display name failed.");
                Assert(transfer.SourceType == typeof(Bitmap).FullName, "Bitmap visualizer BGRA32 source type failed.");
                Assert(transfer.Descriptor.Width == 2 && transfer.Descriptor.Height == 1, "Bitmap visualizer BGRA32 dimensions failed.");
                Assert(transfer.Descriptor.PixelFormat == RawPixelFormat.BGRA32, "Bitmap visualizer BGRA32 pixel format failed.");
                Assert(transfer.Buffer.Length >= 8, "Bitmap visualizer BGRA32 buffer length failed.");
            }
        }

        private static void ImagePtrVisualizerObjectSourceCreatesChunks()
        {
            var buffer = new byte[] { 3, 2, 1, 6, 5, 4 };
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var image = new ImagePtrLike(handle.AddrOfPinnedObject(), buffer.Length, 2, 1, 6, 3);
                var transferType = Type.GetType(
                    "RawBufferVisualizer.VisualStudio.ObjectSource.ImagePtrVisualizerTransfer, RawBufferVisualizer.VisualStudio.ObjectSource",
                    true) ?? throw new InvalidOperationException("ImagePtr visualizer transfer type was not found.");
                var view = InvokeStatic(transferType, "CreateView", image);
                var descriptor = GetProperty<RawImageDescriptor>(view, "Descriptor");

                Assert(GetProperty<long>(view, "BufferLength") == 6, "ImagePtr buffer length failed.");
                Assert(descriptor.Width == 2 && descriptor.Height == 1, "ImagePtr dimensions failed.");
                Assert(descriptor.Stride == 6, "ImagePtr stride failed.");
                Assert(descriptor.PixelFormat == RawPixelFormat.BGR24, "ImagePtr Bpp=3 should map to BGR24.");

                var metadata = (VisualizerSnapshotMetadata)InvokeStatic(transferType, "CreateMetadata", view);
                Assert(metadata.SourceType == typeof(ImagePtrLike).FullName, "ImagePtr metadata source type failed.");
                Assert(metadata.SupportsDirectMemory
                    && metadata.ProcessId == Process.GetCurrentProcess().Id
                    && metadata.BufferAddress == handle.AddrOfPinnedObject().ToInt64(),
                    "ImagePtr direct-memory metadata failed.");

                var chunk = (VisualizerSnapshotChunk)InvokeStatic(
                    transferType,
                    "CreateChunk",
                    view,
                    new VisualizerSnapshotChunkRequest
                    {
                        Offset = 1,
                        Count = 3
                    });

                var preview = (VisualizerSnapshotTransfer)InvokeStatic(
                    transferType,
                    "CreatePreview",
                    view,
                    new VisualizerSnapshotChunkRequest
                    {
                        Operation = VisualizerSnapshotOperation.Preview,
                        MaximumWidth = 2,
                        MaximumHeight = 1
                    });

                Assert(chunk.Buffer.Length == 3 && chunk.Buffer[0] == 2 && chunk.Buffer[2] == 6, "ImagePtr chunk failed.");
                Assert(preview.Buffer[0] == 3 && preview.Buffer[1] == 2 && preview.Buffer[2] == 1, "ImagePtr sampled preview failed.");
            }
            finally
            {
                handle.Free();
            }
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            var method = type.GetMethod(methodName);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, methodName);
            }

            return method.Invoke(null, arguments) ?? throw new InvalidOperationException(methodName + " returned null.");
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName);
            if (property == null)
            {
                throw new MissingMemberException(target.GetType().FullName, propertyName);
            }

            return (T)(property.GetValue(target) ?? throw new InvalidOperationException(propertyName + " returned null."));
        }

        private static void MatVisualizerObjectSourceCreatesChunks()
        {
            using (var mat = new Mat(1, 2, MatType.CV_8UC3, new Scalar(3, 2, 1)))
            {
                var view = OpenCvSharpMatVisualizerTransfer.CreateView(mat, "mat0");
                var metadata = OpenCvSharpMatVisualizerTransfer.CreateMetadata(view);
                var chunk = OpenCvSharpMatVisualizerTransfer.CreateChunk(
                    view,
                    new VisualizerSnapshotChunkRequest
                    {
                        Offset = 1,
                        Count = 4
                    });
                var preview = OpenCvSharpMatVisualizerTransfer.CreatePreview(
                    view,
                    new VisualizerSnapshotChunkRequest
                    {
                        Operation = VisualizerSnapshotOperation.Preview,
                        MaximumWidth = 2,
                        MaximumHeight = 1
                    });

                Assert(metadata.DisplayName == "mat0", "Mat visualizer display name failed.");
                Assert(metadata.SourceType == typeof(Mat).FullName, "Mat visualizer source type failed.");
                Assert(metadata.Descriptor.Width == 2 && metadata.Descriptor.Height == 1, "Mat visualizer dimensions failed.");
                Assert(metadata.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Mat visualizer pixel format failed.");
                Assert(metadata.BufferLength >= 6, "Mat visualizer buffer length failed.");
                Assert(chunk.Buffer.Length == 4 && chunk.Buffer[0] == 2, "Mat visualizer chunk failed.");
                Assert(preview.Buffer[0] == 3 && preview.Buffer[1] == 2 && preview.Buffer[2] == 1, "Mat sampled preview failed.");
            }
        }

        private static void OpenCvSharpMatVisualizerObjectSourceSupportsLegacyMatWithoutDims()
        {
            var buffer = new byte[] { 3, 2, 1, 6, 5, 4 };
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var mat = CreateLegacyOpenCvSharpMat(
                    rows: 1,
                    cols: 2,
                    step: 6,
                    data: handle.AddrOfPinnedObject(),
                    depth: 0,
                    channels: 3);
                var view = OpenCvSharpMatVisualizerTransfer.CreateView(mat, "legacyMat");
                var metadata = OpenCvSharpMatVisualizerTransfer.CreateMetadata(view);
                var chunk = OpenCvSharpMatVisualizerTransfer.CreateChunk(
                    view,
                    new VisualizerSnapshotChunkRequest
                    {
                        Offset = 1,
                        Count = 4
                    });

                Assert(metadata.DisplayName == "legacyMat", "Legacy Mat visualizer display name failed.");
                Assert(metadata.SourceType == "OpenCvSharp.Mat", "Legacy Mat visualizer source type failed.");
                Assert(metadata.Descriptor.Width == 2 && metadata.Descriptor.Height == 1, "Legacy Mat dimensions failed.");
                Assert(metadata.Descriptor.Stride == 6, "Legacy Mat stride failed.");
                Assert(metadata.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Legacy Mat pixel format failed.");
                Assert(metadata.BufferLength == 6, "Legacy Mat buffer length failed.");
                Assert(chunk.Buffer.Length == 4 && chunk.Buffer[0] == 2 && chunk.Buffer[3] == 5, "Legacy Mat chunk failed.");
            }
            finally
            {
                handle.Free();
            }
        }

        private static object CreateLegacyOpenCvSharpMat(int rows, int cols, long step, IntPtr data, int depth, int channels)
        {
            var mat = Activator.CreateInstance(LegacyOpenCvSharpMatRuntimeType)
                ?? throw new InvalidOperationException("Legacy OpenCvSharp.Mat test type was not created.");
            SetProperty(mat, "Rows", rows);
            SetProperty(mat, "Cols", cols);
            SetProperty(mat, "StepValue", step);
            SetProperty(mat, "Data", data);
            SetProperty(mat, "TypeValue", new LegacyOpenCvSharpMatType
            {
                Depth = depth,
                Channels = channels
            });
            return mat;
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName);
            if (property == null)
            {
                throw new MissingMemberException(target.GetType().FullName, propertyName);
            }

            property.SetValue(target, value);
        }

        private static Type CreateLegacyOpenCvSharpMatRuntimeType()
        {
            var assemblyName = new AssemblyName("RawBufferVisualizer.LegacyOpenCvSharpTest");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name ?? "LegacyOpenCvSharpTest");
            var typeBuilder = moduleBuilder.DefineType(
                "OpenCvSharp.Mat",
                TypeAttributes.Public | TypeAttributes.Class);

            DefineAutoProperty(typeBuilder, "Rows", typeof(int));
            DefineAutoProperty(typeBuilder, "Cols", typeof(int));
            DefineAutoProperty(typeBuilder, "Data", typeof(IntPtr));
            var stepGetter = DefineAutoProperty(typeBuilder, "StepValue", typeof(long));
            var typeGetter = DefineAutoProperty(typeBuilder, "TypeValue", typeof(object));

            var emptyMethod = typeBuilder.DefineMethod(
                "Empty",
                MethodAttributes.Public,
                typeof(bool),
                Type.EmptyTypes);
            var emptyIl = emptyMethod.GetILGenerator();
            emptyIl.Emit(OpCodes.Ldc_I4_0);
            emptyIl.Emit(OpCodes.Ret);

            var stepMethod = typeBuilder.DefineMethod(
                "Step",
                MethodAttributes.Public,
                typeof(long),
                Type.EmptyTypes);
            var stepIl = stepMethod.GetILGenerator();
            stepIl.Emit(OpCodes.Ldarg_0);
            stepIl.Emit(OpCodes.Call, stepGetter);
            stepIl.Emit(OpCodes.Ret);

            var typeMethod = typeBuilder.DefineMethod(
                "Type",
                MethodAttributes.Public,
                typeof(object),
                Type.EmptyTypes);
            var typeIl = typeMethod.GetILGenerator();
            typeIl.Emit(OpCodes.Ldarg_0);
            typeIl.Emit(OpCodes.Call, typeGetter);
            typeIl.Emit(OpCodes.Ret);

            return typeBuilder.CreateTypeInfo()?.AsType()
                ?? throw new InvalidOperationException("Legacy OpenCvSharp.Mat test type was not created.");
        }

        private static MethodBuilder DefineAutoProperty(TypeBuilder typeBuilder, string propertyName, Type propertyType)
        {
            var fieldBuilder = typeBuilder.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);
            var propertyBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.None, propertyType, null);

            var getter = typeBuilder.DefineMethod(
                "get_" + propertyName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                propertyType,
                Type.EmptyTypes);
            var getterIl = getter.GetILGenerator();
            getterIl.Emit(OpCodes.Ldarg_0);
            getterIl.Emit(OpCodes.Ldfld, fieldBuilder);
            getterIl.Emit(OpCodes.Ret);

            var setter = typeBuilder.DefineMethod(
                "set_" + propertyName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                null,
                new[] { propertyType });
            var setterIl = setter.GetILGenerator();
            setterIl.Emit(OpCodes.Ldarg_0);
            setterIl.Emit(OpCodes.Ldarg_1);
            setterIl.Emit(OpCodes.Stfld, fieldBuilder);
            setterIl.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getter);
            propertyBuilder.SetSetMethod(setter);
            return getter;
        }

        private sealed class LegacyOpenCvSharpMatType
        {
            public int Depth { get; set; }
            public int Channels { get; set; }
        }

        private static void EmguCvMatVisualizerObjectSourceCreatesChunks()
        {
            using (var mat = new Emgu.CV.Mat(
                1,
                2,
                Emgu.CV.DepthType.Cv8U,
                3,
                new byte[] { 3, 2, 1, 6, 5, 4 },
                6))
            {
                var view = EmguCvMatVisualizerTransfer.CreateView(mat, "emgu0");
                var metadata = EmguCvMatVisualizerTransfer.CreateMetadata(view);
                var chunk = EmguCvMatVisualizerTransfer.CreateChunk(
                    view,
                    new VisualizerSnapshotChunkRequest
                    {
                        Offset = 2,
                        Count = 3
                    });
                var preview = EmguCvMatVisualizerTransfer.CreatePreview(
                    view,
                    new VisualizerSnapshotChunkRequest
                    {
                        Operation = VisualizerSnapshotOperation.Preview,
                        MaximumWidth = 2,
                        MaximumHeight = 1
                    });

                Assert(metadata.DisplayName == "emgu0", "Emgu Mat visualizer display name failed.");
                Assert(metadata.SourceType == "Emgu.CV.Mat", "Emgu Mat visualizer source type failed.");
                Assert(metadata.Descriptor.Width == 2 && metadata.Descriptor.Height == 1, "Emgu Mat visualizer dimensions failed.");
                Assert(metadata.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Emgu Mat visualizer pixel format failed.");
                Assert(metadata.BufferLength == 6, "Emgu Mat visualizer buffer length failed.");
                Assert(chunk.Buffer.Length == 3 && chunk.Buffer[0] == 1 && chunk.Buffer[2] == 5, "Emgu Mat visualizer chunk failed.");
                Assert(preview.Buffer[0] == 3 && preview.Buffer[1] == 2 && preview.Buffer[2] == 1, "Emgu Mat sampled preview failed.");
            }
        }

        private static void ImageCollectionVisualizerHandlesListArrayAndDictionary()
        {
            var snapshot = RawBufferSnapshot.FromByteArray(
                new byte[] { 10, 20 },
                CreateDescriptor(2, 1, 2, RawPixelFormat.Mono8, 8));

            using (var bitmap = new Bitmap(2, 1, PixelFormat.Format24bppRgb))
            using (var openCvMat = new Mat(1, 2, MatType.CV_8UC3, new Scalar(3, 2, 1)))
            using (var emguMat = new Emgu.CV.Mat(
                1,
                2,
                Emgu.CV.DepthType.Cv8U,
                3,
                new byte[] { 3, 2, 1, 6, 5, 4 },
                6))
            {
                var list = new List<object>
                {
                    bitmap,
                    openCvMat,
                    emguMat,
                    snapshot,
                    42,
                    null!
                };
                var listView = ImageCollectionVisualizerTransfer.CreateView(list);

                Assert(listView.Summary.TotalCount == 6 && listView.Summary.ItemCount == 6, "Collection list count failed.");
                Assert(listView.GetMetadata(0).DisplayName == "[0]", "Collection list display name failed.");
                Assert(listView.GetMetadata(0).Metadata?.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Collection Bitmap format failed.");
                Assert(listView.GetMetadata(1).Metadata?.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Collection OpenCvSharp Mat format failed.");
                Assert(listView.GetMetadata(2).Metadata?.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Collection Emgu Mat format failed.");
                Assert(listView.GetMetadata(3).Metadata?.Descriptor.PixelFormat == RawPixelFormat.Mono8, "Collection snapshot format failed.");
                Assert(!string.IsNullOrWhiteSpace(listView.GetMetadata(4).Error), "Unsupported collection item should report an error.");
                Assert(!string.IsNullOrWhiteSpace(listView.GetMetadata(5).Error), "Null collection item should report an error.");

                var chunk = listView.GetChunk(
                    3,
                    new VisualizerSnapshotChunkRequest
                    {
                        Offset = 1,
                        Count = 1
                    });
                Assert(chunk.Buffer.Length == 1 && chunk.Buffer[0] == 20, "Collection snapshot chunk failed.");
                Assert(listView.GetMetadata(3).Metadata?.BufferLength == 2, "Collection transfer should be reusable after final chunk release.");

                var openCvListView = ImageCollectionVisualizerTransfer.CreateView(new List<Mat> { openCvMat });
                Assert(openCvListView.Summary.TotalCount == 1, "Typed OpenCvSharp list count failed.");
                Assert(openCvListView.GetMetadata(0).Metadata?.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Typed OpenCvSharp list transfer failed.");
                var openCvPreview = openCvListView.GetPreview(
                    0,
                    new VisualizerSnapshotChunkRequest
                    {
                        Operation = VisualizerSnapshotOperation.Preview,
                        MaximumWidth = 2,
                        MaximumHeight = 1
                    });
                Assert(openCvPreview.Buffer[0] == 3 && openCvPreview.Buffer[2] == 1, "Typed OpenCvSharp list preview failed.");

                var emguListView = ImageCollectionVisualizerTransfer.CreateView(new List<Emgu.CV.Mat> { emguMat });
                Assert(emguListView.Summary.TotalCount == 1, "Typed Emgu CV list count failed.");
                Assert(emguListView.GetMetadata(0).Metadata?.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Typed Emgu CV list transfer failed.");

                var bitmapListView = ImageCollectionVisualizerTransfer.CreateView(new List<Bitmap> { bitmap });
                Assert(bitmapListView.Summary.TotalCount == 1, "Typed Bitmap list count failed.");
                Assert(bitmapListView.GetMetadata(0).Metadata?.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Typed Bitmap list transfer failed.");

                var openCvDictionaryView = ImageCollectionVisualizerTransfer.CreateView(
                    new Dictionary<string, Mat> { ["opencv"] = openCvMat });
                Assert(openCvDictionaryView.Summary.TotalCount == 1, "Typed OpenCvSharp dictionary count failed.");
                Assert(openCvDictionaryView.GetMetadata(0).DisplayName == "[opencv]", "Typed OpenCvSharp dictionary key failed.");
                Assert(openCvDictionaryView.GetMetadata(0).Metadata?.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Typed OpenCvSharp dictionary transfer failed.");

                var emguDictionaryView = ImageCollectionVisualizerTransfer.CreateView(
                    new Dictionary<string, Emgu.CV.Mat> { ["emgu"] = emguMat });
                Assert(emguDictionaryView.Summary.TotalCount == 1, "Typed Emgu CV dictionary count failed.");
                Assert(emguDictionaryView.GetMetadata(0).Metadata?.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Typed Emgu CV dictionary transfer failed.");

                var bitmapDictionaryView = ImageCollectionVisualizerTransfer.CreateView(
                    new Dictionary<string, Bitmap> { ["bitmap"] = bitmap });
                Assert(bitmapDictionaryView.Summary.TotalCount == 1, "Typed Bitmap dictionary count failed.");
                Assert(bitmapDictionaryView.GetMetadata(0).Metadata?.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Typed Bitmap dictionary transfer failed.");

                var arrayView = ImageCollectionVisualizerTransfer.CreateView(new object[] { snapshot, bitmap });
                Assert(arrayView.Summary.TotalCount == 2, "Collection array count failed.");
                Assert(arrayView.GetMetadata(1).Metadata?.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Collection array Bitmap failed.");

                var typedArrayView = ImageCollectionVisualizerTransfer.CreateView(new[] { openCvMat });
                Assert(typedArrayView.Summary.TotalCount == 1, "Typed OpenCvSharp array count failed.");
                Assert(typedArrayView.GetMetadata(0).Metadata?.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Typed OpenCvSharp array transfer failed.");

                var dictionaryView = ImageCollectionVisualizerTransfer.CreateView(
                    new Dictionary<string, object>
                    {
                        ["input"] = snapshot,
                        ["invalid"] = 42
                    });
                Assert(dictionaryView.Summary.TotalCount == 2, "Collection dictionary count failed.");
                Assert(dictionaryView.GetMetadata(0).DisplayName == "[input]", "Collection dictionary key name failed.");
                Assert(dictionaryView.GetMetadata(0).Metadata?.Descriptor.PixelFormat == RawPixelFormat.Mono8, "Collection dictionary snapshot failed.");
                Assert(!string.IsNullOrWhiteSpace(dictionaryView.GetMetadata(1).Error), "Unsupported dictionary item should report an error.");

                var many = new object[ImageCollectionVisualizerTransfer.MaximumItemsPerOpen + 5];
                for (var index = 0; index < many.Length; index++)
                {
                    many[index] = snapshot;
                }

                var limitedView = ImageCollectionVisualizerTransfer.CreateView(many);
                Assert(limitedView.Summary.TotalCount == many.Length, "Collection total count limit failed.");
                Assert(limitedView.Summary.ItemCount == ImageCollectionVisualizerTransfer.MaximumItemsPerOpen, "Collection item limit failed.");
            }
        }

        private static void VisualizerBridgeWritesLaunchSnapshot()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var viewerPath = Path.Combine(directory, "RawBufferVisualizer.Wpf.exe");
                File.WriteAllBytes(viewerPath, new byte[] { 0 });

                var descriptor = CreateDescriptor(2, 1, 2, RawPixelFormat.Mono8, 8);
                var snapshot = RawBufferSnapshot.FromByteArray(new byte[] { 7, 8 }, descriptor);
                var transfer = RawBufferSnapshotObjectSource.CreateTransfer(snapshot, "camera:0");
                var request = StandaloneViewerBridge.PrepareLaunch(transfer, viewerPath, directory);

                Assert(File.Exists(request.MetadataPath), "Visualizer bridge metadata file was not created.");
                Assert(Path.GetFileName(request.MetadataPath).StartsWith("camera_0", StringComparison.Ordinal), "Visualizer bridge should sanitize snapshot names.");

                var loaded = RawBufferSnapshot.Load(request.MetadataPath);
                Assert(loaded.Buffer.Length == 2 && loaded.Buffer[1] == 8, "Visualizer bridge snapshot buffer failed.");
                Assert(loaded.Descriptor.Width == 2 && loaded.Descriptor.Height == 1, "Visualizer bridge snapshot descriptor failed.");

                var startInfo = request.CreateStartInfo();
                Assert(startInfo.FileName == Path.GetFullPath(viewerPath), "Visualizer launch file path failed.");
                Assert(startInfo.Arguments.Contains(request.MetadataPath), "Visualizer launch argument failed.");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void VisualizerBridgePreparesChunkedLaunchSnapshot()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var viewerPath = Path.Combine(directory, "RawBufferVisualizer.Wpf.exe");
                File.WriteAllBytes(viewerPath, new byte[] { 0 });

                var descriptor = CreateDescriptor(4, 1, 4, RawPixelFormat.Mono8, 8);
                var metadata = VisualizerChunkedTransfer.CreateMetadata(
                    descriptor,
                    4,
                    typeof(RawBufferSnapshot).FullName ?? nameof(RawBufferSnapshot),
                    "chunked:0");
                var request = StandaloneViewerBridge.PrepareLaunch(metadata, viewerPath, directory);
                File.WriteAllBytes(request.RawPath, new byte[] { 1, 2, 3, 4 });

                Assert(File.Exists(request.MetadataPath), "Chunked bridge metadata file was not created.");
                Assert(File.Exists(request.RawPath), "Chunked bridge raw file was not created.");
                Assert(Path.GetFileName(request.MetadataPath).StartsWith("chunked_0", StringComparison.Ordinal), "Chunked bridge should sanitize snapshot names.");

                var loaded = RawBufferSnapshot.Load(request.MetadataPath);
                Assert(loaded.Buffer.Length == 4 && loaded.Buffer[3] == 4, "Chunked bridge buffer failed.");
                Assert(loaded.Descriptor.Width == 4 && loaded.Descriptor.Height == 1, "Chunked bridge descriptor failed.");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void VisualizerBridgePreparesMultiLaunchSnapshots()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var viewerPath = Path.Combine(directory, "RawBufferVisualizer.Wpf.exe");
                File.WriteAllBytes(viewerPath, new byte[] { 0 });

                var descriptor = CreateDescriptor(2, 1, 2, RawPixelFormat.Mono8, 8);
                var first = RawBufferSnapshotObjectSource.CreateTransfer(
                    RawBufferSnapshot.FromByteArray(new byte[] { 1, 2 }, descriptor),
                    "left:image");
                var second = RawBufferSnapshotObjectSource.CreateTransfer(
                    RawBufferSnapshot.FromByteArray(new byte[] { 3, 4 }, descriptor),
                    "right:image");

                var request = StandaloneViewerBridge.PrepareLaunch(new[] { first, second }, viewerPath, directory);

                Assert(request.MetadataPaths.Count == 2, "Multi launch metadata count failed.");
                Assert(request.RawPaths.Count == 2, "Multi launch raw count failed.");
                Assert(Path.GetFileName(request.MetadataPaths[0]).StartsWith("left_image_0", StringComparison.Ordinal), "First multi launch name failed.");
                Assert(Path.GetFileName(request.MetadataPaths[1]).StartsWith("right_image_1", StringComparison.Ordinal), "Second multi launch name failed.");

                var firstLoaded = RawBufferSnapshot.Load(request.MetadataPaths[0]);
                var secondLoaded = RawBufferSnapshot.Load(request.MetadataPaths[1]);
                Assert(firstLoaded.Buffer[0] == 1 && secondLoaded.Buffer[1] == 4, "Multi launch buffers failed.");

                var arguments = request.CreateStartInfo().Arguments;
                Assert(arguments.Contains(request.MetadataPaths[0]) && arguments.Contains(request.MetadataPaths[1]), "Multi launch arguments failed.");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void ViewerPathResolverFindsConfiguredViewer()
        {
            var original = Environment.GetEnvironmentVariable(ViewerPathResolver.ViewerPathEnvironmentVariable);
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var viewerPath = Path.Combine(directory, "RawBufferVisualizer.Wpf.exe");
                File.WriteAllBytes(viewerPath, new byte[] { 0 });
                Environment.SetEnvironmentVariable(ViewerPathResolver.ViewerPathEnvironmentVariable, viewerPath);

                var resolved = ViewerPathResolver.ResolveViewerExecutablePath();
                Assert(resolved == Path.GetFullPath(viewerPath), "Viewer path resolver should use the configured viewer path.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ViewerPathResolver.ViewerPathEnvironmentVariable, original);
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void VisualizerHandoffInboxRoutesRequestsByVisualStudioInstance()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            const int firstVisualStudioProcessId = int.MaxValue;
            const int secondVisualStudioProcessId = int.MaxValue - 1;
            var firstInbox = VisualizerHandoffInbox.GetInboxDirectory(firstVisualStudioProcessId);
            var secondInbox = VisualizerHandoffInbox.GetInboxDirectory(secondVisualStudioProcessId);
            try
            {
                Directory.CreateDirectory(directory);
                var metadataPath = Path.Combine(directory, "camera.rbuf.json");
                File.WriteAllText(metadataPath, "{}");

                var requestPath = VisualizerHandoffInbox.WriteSnapshotRequest(firstVisualStudioProcessId, metadataPath);
                var restored = VisualizerHandoffInbox.ReadSnapshotRequest(requestPath);
                var request = VisualizerHandoffInbox.ReadSnapshotRequestInfo(requestPath);

                var typedRequestPath = VisualizerHandoffInbox.WriteSnapshotRequest(
                    secondVisualStudioProcessId,
                    metadataPath,
                    "bitmapBgr24",
                    "System.Drawing.Bitmap",
                    "handoff-42",
                    isPreview: true);
                var typedRequest = VisualizerHandoffInbox.ReadSnapshotRequestInfo(typedRequestPath);
                var errorRequestPath = VisualizerHandoffInbox.WriteErrorRequest(
                    secondVisualStudioProcessId,
                    "unsupportedMat",
                    "OpenCvSharp.Mat",
                    "The matrix format is not supported.",
                    "System.NotSupportedException",
                    "System.NotSupportedException: The matrix format is not supported.",
                    "handoff-error");
                var errorRequest = VisualizerHandoffInbox.ReadSnapshotRequestInfo(errorRequestPath);
                var liveDescriptor = CreateDescriptor(8, 4, 8, RawPixelFormat.Mono8, 8);
                var liveRequestPath = VisualizerHandoffInbox.WriteLiveMemoryRequest(
                    secondVisualStudioProcessId,
                    4242,
                    0x12345678,
                    32,
                    liveDescriptor,
                    "cameraLive",
                    "OpenCvSharp.Mat",
                    "handoff-live");
                var liveRequest = VisualizerHandoffInbox.ReadSnapshotRequestInfo(liveRequestPath);

                Assert(File.Exists(requestPath), "Handoff request file was not created.");
                Assert(Path.GetDirectoryName(requestPath) == firstInbox, "Handoff request was not routed to the first Visual Studio inbox.");
                Assert(Path.GetDirectoryName(typedRequestPath) == secondInbox, "Handoff request was not routed to the second Visual Studio inbox.");
                Assert(!string.Equals(firstInbox, secondInbox, StringComparison.OrdinalIgnoreCase), "Visual Studio inboxes must be isolated.");
                Assert(restored == Path.GetFullPath(metadataPath), "Handoff metadata path roundtrip failed.");
                Assert(request.MetadataPath == Path.GetFullPath(metadataPath), "Handoff request info metadata path failed.");
                Assert(typedRequest.DisplayName == "bitmapBgr24", "Handoff display name roundtrip failed.");
                Assert(typedRequest.SourceType == "System.Drawing.Bitmap", "Handoff source type roundtrip failed.");
                Assert(typedRequest.HandoffId == "handoff-42" && typedRequest.IsPreview, "Preview handoff identity roundtrip failed.");
                Assert(Path.GetDirectoryName(errorRequestPath) == secondInbox, "Error handoff was not routed to the target Visual Studio inbox.");
                Assert(errorRequest.IsError, "Error handoff was not identified as an error.");
                Assert(errorRequest.MetadataPath == string.Empty, "Error handoff should not contain a metadata path.");
                Assert(errorRequest.DisplayName == "unsupportedMat", "Error handoff display name roundtrip failed.");
                Assert(errorRequest.SourceType == "OpenCvSharp.Mat", "Error handoff source type roundtrip failed.");
                Assert(errorRequest.ErrorMessage == "The matrix format is not supported.", "Error handoff message roundtrip failed.");
                Assert(errorRequest.ErrorType == "System.NotSupportedException", "Error handoff type roundtrip failed.");
                Assert(errorRequest.ErrorDetails.StartsWith("System.NotSupportedException:", StringComparison.Ordinal), "Error handoff details roundtrip failed.");
                Assert(errorRequest.HandoffId == "handoff-error" && !errorRequest.IsPreview, "Error handoff identity roundtrip failed.");
                Assert(liveRequest.IsLiveMemory && !liveRequest.IsError, "Live-memory handoff type roundtrip failed.");
                Assert(liveRequest.MetadataPath == string.Empty, "Live-memory handoff should not contain a metadata path.");
                Assert(liveRequest.LiveProcessId == 4242, "Live-memory process ID roundtrip failed.");
                Assert(liveRequest.LiveBufferAddress == 0x12345678 && liveRequest.LiveBufferLength == 32, "Live-memory buffer roundtrip failed.");
                Assert(liveRequest.LiveDescriptor != null
                    && liveRequest.LiveDescriptor.Width == 8
                    && liveRequest.LiveDescriptor.Height == 4
                    && liveRequest.LiveDescriptor.PixelFormat == RawPixelFormat.Mono8,
                    "Live-memory descriptor roundtrip failed.");
                Assert(liveRequest.HandoffId == "handoff-live" && liveRequest.DisplayName == "cameraLive", "Live-memory identity roundtrip failed.");
                VisualizerHandoffInbox.TryDeleteRequest(errorRequestPath);
                VisualizerHandoffInbox.TryDeleteRequest(liveRequestPath);
                Assert(!File.Exists(errorRequestPath), "Handled error handoff request was not deleted.");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }

                if (Directory.Exists(firstInbox))
                {
                    Directory.Delete(firstInbox, true);
                }

                if (Directory.Exists(secondInbox))
                {
                    Directory.Delete(secondInbox, true);
                }
            }
        }

        private static void VisualizerSupportReportContainsActionableContextWithoutImageData()
        {
            var data = new VisualizerSupportReportData
            {
                ReportType = "Visualization error",
                ErrorId = "RBV-TEST-1234",
                TimestampUtc = new DateTime(2026, 7, 14, 8, 30, 0, DateTimeKind.Utc),
                ExtensionVersion = "1.0.41.0",
                VisualStudioVersion = "17.14.0",
                OperatingSystem = "Windows",
                ProcessArchitecture = "x64",
                SourceName = "cameraFrame",
                SourceType = "OpenCvSharp.Mat",
                ErrorType = "System.NotSupportedException",
                ErrorMessage = "Unsupported depth.",
                ErrorDetails = "System.NotSupportedException: Unsupported depth.",
                Descriptor = "640x480 BGR24 stride 1920",
                DisplayPath = @"C:\Temp\camera.rbuf.json",
                PackageLogPath = @"C:\Temp\package.log",
                ActivityLogPath = @"C:\Temp\ActivityLog.xml"
            };
            data.Diagnostics.Add("Error: Unsupported depth.");

            var report = VisualizerSupportReport.Create(data);

            Assert(report.Contains("Error ID: RBV-TEST-1234"), "Support report error ID failed.");
            Assert(report.Contains("Extension version: 1.0.41.0"), "Support report extension version failed.");
            Assert(report.Contains("Source type: OpenCvSharp.Mat"), "Support report source type failed.");
            Assert(report.Contains("Diagnostics:"), "Support report diagnostics failed.");
            Assert(report.Contains("Image payload included: No"), "Support report image privacy declaration failed.");
        }

        private static void VisualStudioTempStoreDeletesOwnedSnapshotDirectories()
        {
            var snapshotDirectory = VisualStudioTempStore.CreateSnapshotDirectory();
            var externalDirectory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(externalDirectory);
                var metadataPath = Path.Combine(snapshotDirectory, "sample.rbuf.json");
                var externalMetadataPath = Path.Combine(externalDirectory, "sample.rbuf.json");
                File.WriteAllText(metadataPath, "{}");
                File.WriteAllText(externalMetadataPath, "{}");

                string ownedDirectory;
                Assert(VisualStudioTempStore.TryGetOwnedSnapshotDirectory(metadataPath, out ownedDirectory), "Temp store should identify owned snapshot metadata.");
                Assert(!VisualStudioTempStore.TryGetOwnedSnapshotDirectory(externalMetadataPath, out ownedDirectory), "Temp store must not claim external metadata.");
                Assert(!VisualStudioTempStore.TryDeleteSnapshotDirectoryForMetadata(externalMetadataPath), "Temp store must not delete external metadata directories.");
                Assert(Directory.Exists(externalDirectory), "External directory should remain after temp cleanup request.");
                Assert(VisualStudioTempStore.TryDeleteSnapshotDirectoryForMetadata(metadataPath), "Temp store should delete owned snapshot metadata directory.");
                Assert(!Directory.Exists(snapshotDirectory), "Owned snapshot directory should be deleted.");
            }
            finally
            {
                if (Directory.Exists(snapshotDirectory))
                {
                    Directory.Delete(snapshotDirectory, true);
                }

                if (Directory.Exists(externalDirectory))
                {
                    Directory.Delete(externalDirectory, true);
                }
            }
        }

        private static void VisualStudioTempStoreReportsRootByteCount()
        {
            var snapshotDirectory = VisualStudioTempStore.CreateSnapshotDirectory();
            try
            {
                Directory.CreateDirectory(snapshotDirectory);
                File.WriteAllBytes(Path.Combine(snapshotDirectory, "a.raw"), new byte[] { 1, 2, 3 });
                File.WriteAllBytes(Path.Combine(snapshotDirectory, "b.rbuf.json"), Encoding.UTF8.GetBytes("{}"));

                long byteCount;
                Assert(VisualStudioTempStore.TryGetRootByteCount(out byteCount), "Temp store byte count should be available.");
                Assert(byteCount >= 5, "Temp store byte count should include owned snapshot files.");
            }
            finally
            {
                if (Directory.Exists(snapshotDirectory))
                {
                    Directory.Delete(snapshotDirectory, true);
                }
            }
        }

        private static void MatAdapterCreatesSnapshot()
        {
            using (var mat = new Mat(1, 2, MatType.CV_8UC3, new Scalar(3, 2, 1)))
            {
                var snapshot = MatSnapshot.FromMat(mat);
                Assert(snapshot.Descriptor.Width == 2 && snapshot.Descriptor.Height == 1, "Mat dimensions failed.");
                Assert(snapshot.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Mat pixel format failed.");
                Assert(snapshot.Buffer.Length >= 6, "Mat buffer length failed.");
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertBytesEqual(byte[] expected, byte[] actual, string message)
        {
            Assert(expected.Length == actual.Length, message + " Length mismatch.");
            for (var i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                {
                    throw new InvalidOperationException(message + " Byte mismatch at " + i + ".");
                }
            }
        }

        private static bool CreateSparseFile(string path, long length)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                if (!TryMarkSparse(stream))
                {
                    return false;
                }

                stream.SetLength(length);
                stream.Position = 0;
                stream.WriteByte(17);
                stream.Position = length - 1;
                stream.WriteByte(33);
            }

            return true;
        }

        private static bool TryMarkSparse(FileStream stream)
        {
            int bytesReturned;
            return DeviceIoControl(
                stream.SafeFileHandle.DangerousGetHandle(),
                0x000900C4,
                IntPtr.Zero,
                0,
                IntPtr.Zero,
                0,
                out bytesReturned,
                IntPtr.Zero);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            int nInBufferSize,
            IntPtr lpOutBuffer,
            int nOutBufferSize,
            out int lpBytesReturned,
            IntPtr lpOverlapped);

        private static TinySample CreateTinySample(RawPixelFormat format)
        {
            switch (format)
            {
                case RawPixelFormat.Mono8:
                    return new TinySample(new byte[] { 0, 255, 128, 64 }, CreateDescriptor(2, 2, 2, format, 8));
                case RawPixelFormat.Mono16:
                    return new TinySample(new byte[] { 0, 0, 255, 255, 0, 128, 0, 64 }, CreateDescriptor(2, 2, 4, format, 16));
                case RawPixelFormat.Mono10PackedLsb:
                    return new TinySample(PackLsb(new[] { 0, 1023, 512, 256 }, 10), CreateDescriptor(4, 1, 5, format, 10));
                case RawPixelFormat.Mono12PackedLsb:
                    return new TinySample(PackLsb(new[] { 0, 4095, 2048, 1024 }, 12), CreateDescriptor(4, 1, 6, format, 12));
                case RawPixelFormat.Binary:
                    return new TinySample(new byte[] { 0, 1, 0, 255 }, CreateDescriptor(2, 2, 2, format, 1));
                case RawPixelFormat.RGB24:
                    return new TinySample(new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255 }, CreateDescriptor(2, 2, 6, format, 8));
                case RawPixelFormat.BGR24:
                    return new TinySample(new byte[] { 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255 }, CreateDescriptor(2, 2, 6, format, 8));
                case RawPixelFormat.BGRA32:
                    return new TinySample(new byte[] { 0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255, 255, 255, 255, 255 }, CreateDescriptor(2, 2, 8, format, 8));
                case RawPixelFormat.Float32:
                    return new TinySample(CreateFloatBytes(new[] { 0f, 1f, 2f, 3f }), CreateDescriptor(2, 2, 8, format, 32));
                case RawPixelFormat.BayerRGGB8:
                case RawPixelFormat.BayerGRBG8:
                case RawPixelFormat.BayerGBRG8:
                case RawPixelFormat.BayerBGGR8:
                    return new TinySample(new byte[] { 240, 120, 240, 120, 40, 120, 240, 120, 240 }, CreateDescriptor(3, 3, 3, format, 8));
                default:
                    throw new NotSupportedException(format.ToString());
            }
        }

        private static RawImageDescriptor CreateDescriptor(int width, int height, int stride, RawPixelFormat format, int validBits)
        {
            return new RawImageDescriptor
            {
                Width = width,
                Height = height,
                Stride = stride,
                PixelFormat = format,
                ValidBits = validBits,
                ByteOrder = RawByteOrder.LittleEndian
            };
        }

        private static byte[] CreateFloatBytes(float[] values)
        {
            var buffer = new byte[values.Length * 4];
            for (var i = 0; i < values.Length; i++)
            {
                var bytes = BitConverter.GetBytes(values[i]);
                Buffer.BlockCopy(bytes, 0, buffer, i * 4, 4);
            }

            return buffer;
        }

        private static byte[] CreatePackedRows(int width, int height, int bitsPerPixel, int maxValue)
        {
            var stride = (width * bitsPerPixel + 7) / 8;
            var buffer = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                var values = new int[width];
                for (var x = 0; x < width; x++)
                {
                    values[x] = ((y * width) + x) % (maxValue + 1);
                }

                values[0] = 0;
                values[width - 1] = maxValue;
                Buffer.BlockCopy(PackLsb(values, bitsPerPixel), 0, buffer, y * stride, stride);
            }

            return buffer;
        }

        private sealed class TinySample
        {
            public byte[] Buffer { get; private set; }
            public RawImageDescriptor Descriptor { get; private set; }

            public TinySample(byte[] buffer, RawImageDescriptor descriptor)
            {
                Buffer = buffer;
                Descriptor = descriptor;
            }
        }

        private sealed class ImagePtrLike
        {
            public IntPtr Ptr { get; private set; }
            public long Length { get; private set; }
            public int Width { get; private set; }
            public int Height { get; private set; }
            public int Step { get; private set; }
            public int Bpp { get; private set; }

            public ImagePtrLike(IntPtr ptr, long length, int width, int height, int step, int bpp)
            {
                Ptr = ptr;
                Length = length;
                Width = width;
                Height = height;
                Step = step;
                Bpp = bpp;
            }
        }

        private static byte[] PackLsb(int[] values, int bitsPerPixel)
        {
            var buffer = new byte[((values.Length * bitsPerPixel) + 7) / 8];
            for (var x = 0; x < values.Length; x++)
            {
                var value = values[x];
                for (var bit = 0; bit < bitsPerPixel; bit++)
                {
                    if (((value >> bit) & 1) == 0)
                    {
                        continue;
                    }

                    var bitIndex = (x * bitsPerPixel) + bit;
                    buffer[bitIndex / 8] |= (byte)(1 << (bitIndex % 8));
                }
            }

            return buffer;
        }
    }
}

namespace Emgu.CV
{
    public enum DepthType
    {
        Cv8U = 0,
        Cv16U = 2,
        Cv32F = 5
    }

    public sealed class Mat : IDisposable
    {
        private readonly byte[] _buffer;
        private readonly GCHandle _handle;

        public bool IsEmpty { get; private set; }
        public int Dims { get; private set; }
        public DepthType Depth { get; private set; }
        public int NumberOfChannels { get; private set; }
        public int Step { get; private set; }
        public int Rows { get; private set; }
        public int Cols { get; private set; }

        public IntPtr DataPointer
        {
            get { return _handle.AddrOfPinnedObject(); }
        }

        public Mat(int rows, int cols, DepthType depth, int numberOfChannels, byte[] buffer, int step)
        {
            Rows = rows;
            Cols = cols;
            Depth = depth;
            NumberOfChannels = numberOfChannels;
            Step = step;
            Dims = 2;
            _buffer = buffer;
            _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        }

        public void Dispose()
        {
            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
        }
    }
}
