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
using System.Threading.Tasks;
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
                VisualizerHandoffInboxPublishesRequestsAtomically();
                VisualizerHandoffInboxClaimsRequestExactlyOnce();
                VisualizerHandoffInboxTracksExplicitCompletion();
                VisualizerSupportReportContainsActionableContextWithoutImageData();
                VisualStudioTempStoreDeletesOwnedSnapshotDirectories();
                VisualStudioTempStoreReportsRootByteCount();
                BitmapAdapterCreatesSnapshot();
                MatAdapterCreatesSnapshot();
                BufferDoctorFindsPaddedMono8Descriptor();
                BufferDoctorCorrectStrideWinsOnRowContinuity();
                BufferDoctorPrefersCorrectEndianness();
                BufferDoctorPrefersMatchingValidBits();
                BufferDoctorFindsPackedMono12Candidate();
                BufferDoctorSamplingStaysWithinCaps();
                BufferDoctorMarksRgbBgrAsAmbiguousTieGroup();
                BufferDoctorAcceptsTrailingRowFit();
                TypeMappingFileRoundTrips();
                TypeMappingResolutionPrefersSolutionLocal();
                TypeMappingExtractsMappedCompanyFrame();
                TypeMappingAppliesEnumPixelFormatMap();
                TypeMappingFailureIncludesMemberInventory();
                TypeMappingMissingMemberFailsVisibly();
                VisionInferenceAutoOpensDirectPointerShape();
                VisionInferenceSupportsOneLevelNestedMembers();
                VisionInferenceRequestsOnlyAmbiguousPixelFormat();
                VisionInferenceHidesLowConfidenceShape();
                TypeMappingReadsOneLevelNestedMemberPaths();
                AutomaticInspectionPreferencesTests.RunAll();
                AutomaticImageCollectionPolicyTests.RunAll();
                ReleaseAnnouncementTests.RunAll();
                VisualizerEnvironmentCheckTests.RunAll();
                IndustrialCameraContractTests.RunAll();
                WorkspaceAndHandoffCoordinatorTests.RunAll();
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

                var bitmapArrayView = ImageCollectionVisualizerTransfer.CreateView(new[] { bitmap });
                Assert(bitmapArrayView.Summary.TotalCount == 1, "Typed Bitmap array count failed.");
                Assert(bitmapArrayView.GetMetadata(0).Metadata?.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Typed Bitmap array transfer failed.");

                var emguArrayView = ImageCollectionVisualizerTransfer.CreateView(new[] { emguMat });
                Assert(emguArrayView.Summary.TotalCount == 1, "Typed Emgu CV array count failed.");
                Assert(emguArrayView.GetMetadata(0).Metadata?.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Typed Emgu CV array transfer failed.");

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
                Assert(
                    Path.GetFileName(requestPath).Length <= 48,
                    "Handoff request names must stay short enough for long Visual Studio TEMP paths.");
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

        private static void VisualizerHandoffInboxPublishesRequestsAtomically()
        {
            var visualStudioProcessId = CreateVisualizerTestProcessId();
            var inboxDirectory = VisualizerHandoffInbox.GetInboxDirectory(visualStudioProcessId);
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            var requestPaths = new List<string>();
            var observed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var observerErrors = new List<Exception>();
            var stopObserver = 0;
            var observerReady = new ManualResetEventSlim(false);
            try
            {
                Directory.CreateDirectory(directory);
                Directory.CreateDirectory(inboxDirectory);
                var metadataPath = Path.Combine(directory, "atomic.rbuf.json");
                File.WriteAllText(metadataPath, "{}");

                var observer = Task.Run(() =>
                {
                    observerReady.Set();
                    while (Volatile.Read(ref stopObserver) == 0)
                    {
                        try
                        {
                            foreach (var path in Directory.GetFiles(
                                inboxDirectory,
                                "*.rbuf-handoff",
                                SearchOption.TopDirectoryOnly))
                            {
                                if (observed.Add(path))
                                {
                                    ReadPublishedHandoffWithRetry(path);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (observerErrors)
                            {
                                observerErrors.Add(ex);
                            }
                        }

                        Thread.Yield();
                    }
                });
                observerReady.Wait();

                const int writerCount = 8;
                const int requestsPerWriter = 8;
                var writers = new Task[writerCount];
                for (var writerIndex = 0; writerIndex < writerCount; writerIndex++)
                {
                    writers[writerIndex] = Task.Run(() =>
                    {
                        for (var requestIndex = 0; requestIndex < requestsPerWriter; requestIndex++)
                        {
                            var path = VisualizerHandoffInbox.WriteSnapshotRequest(
                                visualStudioProcessId,
                                metadataPath,
                                "atomic",
                                "test",
                                Guid.NewGuid().ToString("N"));
                            lock (requestPaths)
                            {
                                requestPaths.Add(path);
                            }
                        }
                    });
                }

                Task.WaitAll(writers);
                Volatile.Write(ref stopObserver, 1);
                observer.Wait();

                Assert(
                    requestPaths.Count == writerCount * requestsPerWriter,
                    "Atomic handoff publish did not create every request.");
                Assert(
                    observerErrors.Count == 0,
                    "A published handoff could not be read and parsed after retry. "
                    + (observerErrors.Count == 0
                        ? string.Empty
                        : observerErrors[0].GetType().Name
                            + ": "
                            + observerErrors[0].Message));
                Assert(
                    observed.Count > 0,
                    "The atomic handoff observer did not run concurrently with publication.");
                Assert(
                    Directory.GetFiles(
                        inboxDirectory,
                        "*.publishing.*",
                        SearchOption.TopDirectoryOnly).Length == 0,
                    "Atomic handoff publish left temporary files behind.");
                foreach (var requestPath in requestPaths)
                {
                    var request = VisualizerHandoffInbox.ReadSnapshotRequestInfo(requestPath);
                    Assert(
                        request.MetadataPath == Path.GetFullPath(metadataPath),
                        "Atomically published handoff content did not roundtrip.");
                }
            }
            finally
            {
                Volatile.Write(ref stopObserver, 1);
                observerReady.Dispose();
                foreach (var requestPath in requestPaths)
                {
                    VisualizerHandoffInbox.CleanupRequestArtifacts(requestPath);
                }

                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }

                if (Directory.Exists(inboxDirectory))
                {
                    Directory.Delete(inboxDirectory, true);
                }
            }
        }

        private static void ReadPublishedHandoffWithRetry(string requestPath)
        {
            const int attemptCount = 10;
            const int retryDelayMilliseconds = 50;
            Exception? last = null;
            for (var attempt = 0; attempt < attemptCount; attempt++)
            {
                try
                {
                    VisualizerHandoffInbox.ReadSnapshotRequestInfo(requestPath);
                    return;
                }
                catch (Exception ex) when (
                    ex is IOException
                    || ex is UnauthorizedAccessException)
                {
                    last = ex;
                    if (attempt + 1 < attemptCount)
                    {
                        Thread.Sleep(retryDelayMilliseconds);
                    }
                }
            }

            throw last ?? new IOException("Published handoff request could not be read.");
        }

        private static void VisualizerHandoffInboxClaimsRequestExactlyOnce()
        {
            var visualStudioProcessId = CreateVisualizerTestProcessId();
            var inboxDirectory = VisualizerHandoffInbox.GetInboxDirectory(visualStudioProcessId);
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            string? requestPath = null;
            string? successfulProcessingPath = null;
            var successCount = 0;
            var gate = new ManualResetEventSlim(false);
            try
            {
                Directory.CreateDirectory(directory);
                var metadataPath = Path.Combine(directory, "claim.rbuf.json");
                File.WriteAllText(metadataPath, "{}");
                requestPath = VisualizerHandoffInbox.WriteSnapshotRequest(
                    visualStudioProcessId,
                    metadataPath,
                    "claim",
                    "test");

                const int claimantCount = 32;
                var claimants = new Task[claimantCount];
                for (var claimantIndex = 0; claimantIndex < claimantCount; claimantIndex++)
                {
                    claimants[claimantIndex] = Task.Run(() =>
                    {
                        gate.Wait();
                        string processingPath;
                        if (VisualizerHandoffInbox.TryClaimRequest(
                                requestPath,
                                out processingPath))
                        {
                            Interlocked.Increment(ref successCount);
                            lock (gate)
                            {
                                successfulProcessingPath = processingPath;
                            }
                        }
                    });
                }

                gate.Set();
                Task.WaitAll(claimants);

                Assert(
                    successCount == 1,
                    "Exactly one concurrent handoff claimant must succeed, but "
                    + successCount
                    + " succeeded.");
                Assert(
                    !string.IsNullOrWhiteSpace(successfulProcessingPath)
                    && File.Exists(successfulProcessingPath),
                    "The winning handoff claim did not own a processing file.");
                Assert(!File.Exists(requestPath), "Claimed handoff remained visible as ready.");
                Assert(
                    VisualizerHandoffInbox.GetRequestState(requestPath)
                        == VisualizerHandoffRequestState.Processing,
                    "Claimed handoff did not report Processing state.");
                var claimedRequest = VisualizerHandoffInbox.ReadSnapshotRequestInfo(
                    successfulProcessingPath!);
                Assert(
                    claimedRequest.MetadataPath == Path.GetFullPath(metadataPath),
                    "Claimed handoff content was not preserved.");
            }
            finally
            {
                gate.Dispose();
                if (!string.IsNullOrWhiteSpace(requestPath))
                {
                    VisualizerHandoffInbox.CleanupRequestArtifacts(requestPath!);
                }

                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }

                if (Directory.Exists(inboxDirectory))
                {
                    Directory.Delete(inboxDirectory, true);
                }
            }
        }

        private static void VisualizerHandoffInboxTracksExplicitCompletion()
        {
            var visualStudioProcessId = CreateVisualizerTestProcessId();
            var inboxDirectory = VisualizerHandoffInbox.GetInboxDirectory(visualStudioProcessId);
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            var requestPaths = new List<string>();
            string? lateAcknowledgementTransitionPath = null;
            try
            {
                Directory.CreateDirectory(directory);
                var metadataPath = Path.Combine(directory, "completion.rbuf.json");
                File.WriteAllText(metadataPath, "{}");

                var acknowledgedRequestPath = VisualizerHandoffInbox.WriteSnapshotRequest(
                    visualStudioProcessId,
                    metadataPath,
                    "ack",
                    "test");
                requestPaths.Add(acknowledgedRequestPath);
                Assert(
                    VisualizerHandoffInbox.GetRequestState(acknowledgedRequestPath)
                        == VisualizerHandoffRequestState.Ready,
                    "New handoff did not report Ready state.");

                string acknowledgedProcessingPath;
                Assert(
                    VisualizerHandoffInbox.TryClaimRequest(
                        acknowledgedRequestPath,
                        out acknowledgedProcessingPath),
                    "Acknowledgement test could not claim its handoff.");
                Assert(
                    VisualizerHandoffInbox.GetRequestState(acknowledgedRequestPath)
                        == VisualizerHandoffRequestState.Processing,
                    "Disappearance of the ready file must not count as acknowledgement.");
                Assert(
                    VisualizerHandoffInbox.TryAcknowledgeRequest(
                        acknowledgedRequestPath,
                        acknowledgedProcessingPath),
                    "Claimed handoff could not publish acknowledgement.");
                Assert(
                    VisualizerHandoffInbox.GetRequestState(acknowledgedRequestPath)
                        == VisualizerHandoffRequestState.Acknowledged,
                    "Acknowledged handoff did not report Acknowledged state.");
                Assert(
                    File.Exists(VisualizerHandoffInbox.GetAcknowledgementPath(
                        acknowledgedRequestPath)),
                    "Acknowledgement marker was not published.");

                var rejectedRequestPath = VisualizerHandoffInbox.WriteSnapshotRequest(
                    visualStudioProcessId,
                    metadataPath,
                    "nack",
                    "test");
                requestPaths.Add(rejectedRequestPath);
                string rejectedProcessingPath;
                Assert(
                    VisualizerHandoffInbox.TryClaimRequest(
                        rejectedRequestPath,
                        out rejectedProcessingPath),
                    "Rejection test could not claim its handoff.");
                var rejectionObservedWithReason = false;
                var observedRejectionReason = string.Empty;
                var rejectionObserver = Task.Run(() =>
                {
                    var deadline = DateTime.UtcNow.AddSeconds(5);
                    while (DateTime.UtcNow < deadline)
                    {
                        if (VisualizerHandoffInbox.GetRequestState(rejectedRequestPath)
                            == VisualizerHandoffRequestState.Rejected)
                        {
                            rejectionObservedWithReason =
                                VisualizerHandoffInbox.TryReadRejectionReason(
                                    rejectedRequestPath,
                                    out observedRejectionReason);
                            return;
                        }

                        Thread.Yield();
                    }
                });
                var processingLock = new FileStream(
                    rejectedProcessingPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None);
                var releaseProcessingLock = Task.Run(() =>
                {
                    Thread.Sleep(150);
                    processingLock.Dispose();
                });
                Assert(
                    VisualizerHandoffInbox.TryRejectRequest(
                        rejectedRequestPath,
                        rejectedProcessingPath,
                        "The image document could not be created."),
                    "Claimed handoff could not publish rejection.");
                releaseProcessingLock.Wait();
                rejectionObserver.Wait();
                Assert(
                    rejectionObservedWithReason
                    && observedRejectionReason
                        == "The image document could not be created.",
                    "A visible NACK marker must always have its rejection reason available.");
                Assert(
                    VisualizerHandoffInbox.GetRequestState(rejectedRequestPath)
                        == VisualizerHandoffRequestState.Rejected,
                    "Rejected handoff did not report Rejected state.");
                string rejectionReason;
                Assert(
                    VisualizerHandoffInbox.TryReadRejectionReason(
                        rejectedRequestPath,
                        out rejectionReason)
                    && rejectionReason == "The image document could not be created.",
                    "Rejected handoff reason did not roundtrip.");

                var delayedCleanupRequestPath =
                    VisualizerHandoffInbox.WriteSnapshotRequest(
                        visualStudioProcessId,
                        metadataPath,
                        "delayed-cleanup",
                        "test");
                requestPaths.Add(delayedCleanupRequestPath);
                string delayedCleanupProcessingPath;
                Assert(
                    VisualizerHandoffInbox.TryClaimRequest(
                        delayedCleanupRequestPath,
                        out delayedCleanupProcessingPath)
                    && VisualizerHandoffInbox.TryAcknowledgeRequest(
                        delayedCleanupRequestPath,
                        delayedCleanupProcessingPath),
                    "Delayed cleanup test could not publish acknowledgement.");
                VisualizerHandoffInbox.ScheduleTerminalArtifactCleanup(
                    new[] { delayedCleanupRequestPath },
                    TimeSpan.FromSeconds(2));
                Assert(
                    SpinWait.SpinUntil(
                        () => VisualizerHandoffInbox.GetRequestState(
                            delayedCleanupRequestPath)
                            == VisualizerHandoffRequestState.Missing,
                        3000),
                    "Delayed cleanup did not remove a terminal handoff.");

                var lockedCleanupRequestPath =
                    VisualizerHandoffInbox.WriteSnapshotRequest(
                        visualStudioProcessId,
                        metadataPath,
                        "locked-delayed-cleanup",
                        "test");
                requestPaths.Add(lockedCleanupRequestPath);
                string lockedCleanupProcessingPath;
                Assert(
                    VisualizerHandoffInbox.TryClaimRequest(
                        lockedCleanupRequestPath,
                        out lockedCleanupProcessingPath)
                    && VisualizerHandoffInbox.TryAcknowledgeRequest(
                        lockedCleanupRequestPath,
                        lockedCleanupProcessingPath),
                    "Locked delayed cleanup test could not publish acknowledgement.");
                var lockedAcknowledgementPath =
                    VisualizerHandoffInbox.GetAcknowledgementPath(
                        lockedCleanupRequestPath);
                using (var acknowledgementLock = new FileStream(
                    lockedAcknowledgementPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                {
                    VisualizerHandoffInbox.ScheduleTerminalArtifactCleanup(
                        new[] { lockedCleanupRequestPath },
                        TimeSpan.FromSeconds(2));
                    Thread.Sleep(300);
                    Assert(
                        File.Exists(lockedAcknowledgementPath),
                        "The locked acknowledgement unexpectedly disappeared.");
                }

                Assert(
                    SpinWait.SpinUntil(
                        () => VisualizerHandoffInbox.GetRequestState(
                            lockedCleanupRequestPath)
                            == VisualizerHandoffRequestState.Missing,
                        3000),
                    "Delayed cleanup did not retry a temporarily locked acknowledgement.");

                var lateAcknowledgementRequestPath =
                    VisualizerHandoffInbox.WriteSnapshotRequest(
                        visualStudioProcessId,
                        metadataPath,
                        "late-acknowledgement-cleanup",
                        "test");
                requestPaths.Add(lateAcknowledgementRequestPath);
                string lateAcknowledgementProcessingPath;
                Assert(
                    VisualizerHandoffInbox.TryClaimRequest(
                        lateAcknowledgementRequestPath,
                        out lateAcknowledgementProcessingPath),
                    "Late acknowledgement cleanup test could not claim its request.");
                lateAcknowledgementTransitionPath = Path.Combine(
                    inboxDirectory,
                    Guid.NewGuid().ToString("N") + ".transition");
                File.Move(
                    lateAcknowledgementProcessingPath,
                    lateAcknowledgementTransitionPath);
                Assert(
                    VisualizerHandoffInbox.GetRequestState(
                        lateAcknowledgementRequestPath)
                        == VisualizerHandoffRequestState.Missing,
                    "Late acknowledgement test did not create the intended transient Missing state.");
                VisualizerHandoffInbox.ScheduleTerminalArtifactCleanup(
                    new[] { lateAcknowledgementRequestPath },
                    TimeSpan.FromSeconds(2));
                var publishLateAcknowledgement = Task.Run(() =>
                {
                    Thread.Sleep(50);
                    File.Move(
                        lateAcknowledgementTransitionPath,
                        VisualizerHandoffInbox.GetAcknowledgementPath(
                            lateAcknowledgementRequestPath));
                    lateAcknowledgementTransitionPath = null;
                });
                publishLateAcknowledgement.Wait();
                Assert(
                    SpinWait.SpinUntil(
                        () => VisualizerHandoffInbox.GetRequestState(
                            lateAcknowledgementRequestPath)
                            == VisualizerHandoffRequestState.Missing,
                        3000),
                    "Delayed cleanup abandoned an acknowledgement after transient Missing.");

                var readyCleanupBoundaryRequestPath =
                    VisualizerHandoffInbox.WriteSnapshotRequest(
                        visualStudioProcessId,
                        metadataPath,
                        "ready-cleanup-boundary",
                        "test");
                requestPaths.Add(readyCleanupBoundaryRequestPath);
                VisualizerHandoffInbox.ScheduleTerminalArtifactCleanup(
                    new[] { readyCleanupBoundaryRequestPath },
                    TimeSpan.FromMilliseconds(200));
                Thread.Sleep(400);
                Assert(
                    VisualizerHandoffInbox.GetRequestState(
                        readyCleanupBoundaryRequestPath)
                        == VisualizerHandoffRequestState.Ready,
                    "Delayed terminal cleanup must not delete a Ready handoff.");

                foreach (var requestPath in requestPaths)
                {
                    VisualizerHandoffInbox.CleanupRequestArtifacts(requestPath);
                    Assert(
                        VisualizerHandoffInbox.GetRequestState(requestPath)
                            == VisualizerHandoffRequestState.Missing,
                        "Handoff cleanup left protocol artifacts behind.");
                    Assert(
                        Directory.GetFiles(
                            Path.GetDirectoryName(requestPath)!,
                            Path.GetFileName(requestPath) + "*",
                            SearchOption.TopDirectoryOnly).Length == 0,
                        "Handoff cleanup left an orphaned protocol file behind.");
                }
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(
                        lateAcknowledgementTransitionPath)
                    && File.Exists(lateAcknowledgementTransitionPath))
                {
                    File.Delete(lateAcknowledgementTransitionPath);
                }

                foreach (var requestPath in requestPaths)
                {
                    VisualizerHandoffInbox.CleanupRequestArtifacts(requestPath);
                }

                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }

                if (Directory.Exists(inboxDirectory))
                {
                    Directory.Delete(inboxDirectory, true);
                }
            }
        }

        private static int CreateVisualizerTestProcessId()
        {
            return 1000000000 + (Guid.NewGuid().GetHashCode() & 0x3fffffff);
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

        private static void BufferDoctorFindsPaddedMono8Descriptor()
        {
            var correct = CreateDescriptor(2448, 2048, 2560, RawPixelFormat.Mono8, 8);
            var buffer = CreateNoisyMono8Buffer(2448, 2048, 2560, 12345);
            using (var source = RawImageSource.FromMemory(buffer, correct))
            {
                var result = BufferDoctor.Diagnose(source, CancellationToken.None);
                Assert(result.Candidates.Count > 0, "Buffer Doctor returned no candidates.");
                var top = result.Candidates[0];
                Assert(
                    top.Descriptor.Width == 2448
                        && top.Descriptor.Height == 2048
                        && top.Descriptor.Stride == 2560
                        && top.Descriptor.PixelFormat == RawPixelFormat.Mono8,
                    "Buffer Doctor top candidate should be the true padded Mono8 descriptor.");

                var wrong = CreateDescriptor(2448, 2048, 2448, RawPixelFormat.Mono8, 8);
                var wrongCandidate = ScoreDraft(source, wrong);
                Assert(wrongCandidate.Score < top.Score, "The current wrong descriptor (stride = width) should score lower than the diagnosis top candidate.");
            }
        }

        private static void BufferDoctorCorrectStrideWinsOnRowContinuity()
        {
            var correct = CreateDescriptor(2448, 2048, 2560, RawPixelFormat.Mono8, 8);
            var wrong = CreateDescriptor(2448, 2048, 2448, RawPixelFormat.Mono8, 8);
            var buffer = CreateSmoothMono8Buffer(2448, 2048, 2560);
            using (var source = RawImageSource.FromMemory(buffer, correct))
            {
                var correctCandidate = ScoreDraft(source, correct);
                var wrongCandidate = ScoreDraft(source, wrong);
                Assert(correctCandidate.ContentScore > wrongCandidate.ContentScore, "Row-continuity content score should favor the correct padded stride.");
                Assert(correctCandidate.Score > wrongCandidate.Score, "Total score should favor the correct padded stride on a sheared interpretation.");
            }
        }

        private static void BufferDoctorPrefersCorrectEndianness()
        {
            var bigEndianDescriptor = CreateDescriptor(640, 480, 1280, RawPixelFormat.Mono16, 16);
            bigEndianDescriptor.ByteOrder = RawByteOrder.BigEndian;
            var buffer = new byte[bigEndianDescriptor.Stride * bigEndianDescriptor.Height];
            for (var y = 0; y < bigEndianDescriptor.Height; y++)
            {
                for (var x = 0; x < bigEndianDescriptor.Width; x++)
                {
                    var value = x + (2 * y);
                    var offset = (y * bigEndianDescriptor.Stride) + (x * 2);
                    buffer[offset] = (byte)(value >> 8);
                    buffer[offset + 1] = (byte)(value & 0xFF);
                }
            }

            using (var source = RawImageSource.FromMemory(buffer, bigEndianDescriptor))
            {
                var bigEndian = ScoreDraft(source, bigEndianDescriptor);
                var littleEndian = ScoreDraft(source, CreateDescriptor(640, 480, 1280, RawPixelFormat.Mono16, 16));
                Assert(bigEndian.Score > littleEndian.Score, "Big-endian interpretation should win on a big-endian smooth ramp.");
            }
        }

        private static void BufferDoctorPrefersMatchingValidBits()
        {
            var twelveBitDescriptor = CreateDescriptor(640, 480, 1280, RawPixelFormat.Mono16, 12);
            var buffer = new byte[twelveBitDescriptor.Stride * twelveBitDescriptor.Height];
            for (var y = 0; y < twelveBitDescriptor.Height; y++)
            {
                for (var x = 0; x < twelveBitDescriptor.Width; x++)
                {
                    var value = (x + (y * twelveBitDescriptor.Width)) % 4096;
                    var offset = (y * twelveBitDescriptor.Stride) + (x * 2);
                    buffer[offset] = (byte)(value & 0xFF);
                    buffer[offset + 1] = (byte)(value >> 8);
                }
            }

            using (var source = RawImageSource.FromMemory(buffer, twelveBitDescriptor))
            {
                var twelveBit = ScoreDraft(source, twelveBitDescriptor);
                var sixteenBit = ScoreDraft(source, CreateDescriptor(640, 480, 1280, RawPixelFormat.Mono16, 16));
                Assert(twelveBit.Score > sixteenBit.Score, "ValidBits 12 should beat ValidBits 16 when all sampled values are below 4096.");
            }
        }

        private static void BufferDoctorFindsPackedMono12Candidate()
        {
            var width = 640;
            var height = 480;
            var buffer = CreatePackedRows(width, height, 12, 4095);
            var descriptor = CreateDescriptor(width, height, ((width * 12) + 7) / 8, RawPixelFormat.Mono12PackedLsb, 12);
            using (var source = RawImageSource.FromMemory(buffer, descriptor))
            {
                var packed = ScoreDraft(source, descriptor);
                Assert(packed.Descriptor.Stride == descriptor.Stride, "Packed Mono12 candidate stride mismatch.");
                Assert(
                    !RawBufferDiagnostics.HasErrors(RawBufferDiagnostics.AnalyzeLength(buffer.Length, packed.Descriptor)),
                    "Packed Mono12 candidate should have clean length diagnostics.");
            }
        }

        private static void BufferDoctorSamplingStaysWithinCaps()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var rawPath = Path.Combine(directory, "huge-100k-doctor.raw");
                var descriptor = CreateDescriptor(100000, 100000, 100000, RawPixelFormat.Mono8, 8);
                if (!CreateSparseFile(rawPath, descriptor.GetRequiredByteCount()))
                {
                    Console.WriteLine("Skipped Buffer Doctor sampling-cap test because this filesystem does not support sparse files.");
                    return;
                }

                using (var source = RawImageSource.FromFile(rawPath, descriptor))
                {
                    var candidate = ScoreDraft(source, descriptor);
                    Assert(candidate.SampledRowCount > 0, "Buffer Doctor sampling did not read any rows.");
                    Assert(candidate.SampledRowCount <= 64, "Buffer Doctor sampling read more than 64 rows.");
                    Assert(candidate.SampledByteCount <= 4L * 1024 * 1024, "Buffer Doctor sampling read more than 4 MiB.");
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

        private static void BufferDoctorMarksRgbBgrAsAmbiguousTieGroup()
        {
            var width = 640;
            var height = 480;
            var descriptor = CreateDescriptor(width, height, width * 3, RawPixelFormat.RGB24, 8);
            var buffer = new byte[descriptor.Stride * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var offset = (y * descriptor.Stride) + (x * 3);
                    buffer[offset] = (byte)(127 + (120 * Math.Sin((x * 0.2) + (y * 0.3))));
                    buffer[offset + 1] = (byte)(127 + (120 * Math.Sin((x * 0.2) + (y * 0.3) + 1.0)));
                    buffer[offset + 2] = (byte)(127 + (120 * Math.Sin((x * 0.2) + (y * 0.3) + 2.0)));
                }
            }

            using (var source = RawImageSource.FromMemory(buffer, descriptor))
            {
                var result = BufferDoctor.Diagnose(source, CancellationToken.None);
                var rgb = FindCandidate(result, width, height, width * 3, RawPixelFormat.RGB24);
                var bgr = FindCandidate(result, width, height, width * 3, RawPixelFormat.BGR24);
                Assert(rgb != null && bgr != null, "RGB24/BGR24 pair should be among the top candidates.");
                Assert(rgb!.Score == bgr!.Score, "RGB24 and BGR24 should tie on score.");
                Assert(rgb.IsAmbiguousWithGroup && bgr.IsAmbiguousWithGroup, "RGB24/BGR24 should be marked as an ambiguous tie group.");
                Assert(HasReasonContaining(rgb, "cannot be distinguished") && HasReasonContaining(bgr, "cannot be distinguished"), "Ambiguous tie group should carry an explicit reason.");
            }
        }

        private static void BufferDoctorAcceptsTrailingRowFit()
        {
            var width = 640;
            var height = 480;
            var stride = 768;
            var descriptor = CreateDescriptor(width, height, stride, RawPixelFormat.Mono8, 8);
            var buffer = CreateSmoothMono8Buffer(width, height, stride);
            Assert(buffer.Length == (stride * (height - 1)) + width, "Trailing-row fixture length mismatch.");
            using (var source = RawImageSource.FromMemory(buffer, descriptor))
            {
                var candidate = ScoreDraft(source, descriptor);
                Assert(HasReasonContaining(candidate, "Trailing-row fit"), "Trailing-row fit should be reported as a reason.");

                var result = BufferDoctor.Diagnose(source, CancellationToken.None);
                Assert(result.Candidates.Count > 0, "Buffer Doctor returned no candidates for the trailing-row buffer.");
                var top = result.Candidates[0];
                Assert(
                    top.Descriptor.Width == width
                        && top.Descriptor.Height == height
                        && top.Descriptor.Stride == stride
                        && top.Descriptor.PixelFormat == RawPixelFormat.Mono8,
                    "Trailing-row interpretation should be the top candidate.");
            }
        }

        private static void TypeMappingFileRoundTrips()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "type-mappings.json");
                var typeName = typeof(CompanyFrame).FullName!;
                var assemblyName = typeof(CompanyFrame).Assembly.GetName().Name!;

                var store = new TypeMappingStore(null, path);
                var file = new TypeMappingFile();
                file.Mappings.Add(new TypeMapping
                {
                    TypeName = typeName,
                    AssemblyName = assemblyName,
                    Members = new TypeMappingMembers
                    {
                        Data = "ImageAddress",
                        Width = "SizeX",
                        Height = "SizeY",
                        Stride = "LinePitch",
                        PixelFormat = "PixelType"
                    },
                    PixelFormatMap = CompanyPixelFormatMap(),
                    ByteOrder = "LittleEndian"
                });
                store.Save(file);

                var reloaded = new TypeMappingStore(null, path);
                var mapping = reloaded.FindMapping(typeName, assemblyName);
                Assert(mapping != null, "Saved mapping was not found after reload.");
                Assert(mapping!.Members.Data == "ImageAddress" && mapping.Members.Stride == "LinePitch", "Mapping members did not round-trip.");
                Assert(
                    mapping.PixelFormatMap != null && mapping.PixelFormatMap.Count == 4 && mapping.PixelFormatMap["Mono12"] == "Mono12PackedLsb",
                    "pixelFormatMap did not round-trip as a JSON object map.");
                Assert(reloaded.FindMapping(typeName, "Other.Assembly") == null, "Assembly name match must be exact.");
                Assert(reloaded.FindMapping("Other.Type", assemblyName) == null, "Type name match must be exact.");

                File.WriteAllText(path, "{\"version\":99,\"mappings\":[]}");
                var versioned = new TypeMappingStore(null, path);
                Assert(versioned.FindMapping(typeName, assemblyName) == null, "Unknown schema versions must be ignored.");
                Assert(!string.IsNullOrEmpty(versioned.LastLoadError), "Version mismatch should be recorded as a load error.");

                File.WriteAllText(path, "{ not json");
                var malformed = new TypeMappingStore(null, path);
                Assert(malformed.FindMapping(typeName, assemblyName) == null, "Malformed mapping files must be ignored without crashing.");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void TypeMappingResolutionPrefersSolutionLocal()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                var solutionDirectory = Path.Combine(directory, "repo");
                var nestedDirectory = Path.Combine(solutionDirectory, "src", "App", "bin");
                Directory.CreateDirectory(nestedDirectory);
                var userDirectory = Path.Combine(directory, "user");
                Directory.CreateDirectory(userDirectory);

                var typeName = typeof(CompanyFrame).FullName!;
                var assemblyName = typeof(CompanyFrame).Assembly.GetName().Name!;

                var solutionPath = Path.Combine(solutionDirectory, TypeMappingStore.SolutionLocalFileName);
                var solutionStore = new TypeMappingStore(null, solutionPath);
                var solutionFile = new TypeMappingFile();
                solutionFile.Mappings.Add(new TypeMapping
                {
                    TypeName = typeName,
                    AssemblyName = assemblyName,
                    Members = new TypeMappingMembers { Data = "ImageAddress", Width = "SizeX", Height = "SizeY" },
                    ByteOrder = "BigEndian"
                });
                solutionStore.Save(solutionFile);

                var userPath = Path.Combine(userDirectory, "type-mappings.json");
                var userStore = new TypeMappingStore(null, userPath);
                var userFile = new TypeMappingFile();
                userFile.Mappings.Add(new TypeMapping
                {
                    TypeName = typeName,
                    AssemblyName = assemblyName,
                    Members = new TypeMappingMembers { Data = "ImageAddress", Width = "SizeX", Height = "SizeY" },
                    ByteOrder = "LittleEndian"
                });
                userStore.Save(userFile);

                var discovered = TypeMappingStore.FindSolutionLocalMappingPath(nestedDirectory);
                Assert(discovered == solutionPath, "Solution-local mapping file was not discovered by walking up directories.");
                Assert(TypeMappingStore.FindSolutionLocalMappingPath(userDirectory) == null, "Unrelated directories should not discover a solution-local mapping.");

                var store = new TypeMappingStore(discovered, userPath);
                var mapping = store.FindMapping(typeName, assemblyName);
                Assert(mapping != null && mapping!.ByteOrder == "BigEndian", "Solution-local mapping should win over the user mapping.");

                var userOnly = new TypeMappingStore(null, userPath);
                var fallback = userOnly.FindMapping(typeName, assemblyName);
                Assert(fallback != null && fallback!.ByteOrder == "LittleEndian", "User mapping should apply when no solution-local mapping exists.");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void TypeMappingExtractsMappedCompanyFrame()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            var width = 8;
            var height = 4;
            var buffer = new byte[width * height];
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)(i * 3);
            }

            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                Directory.CreateDirectory(directory);
                var frame = new CompanyFrame
                {
                    ImageAddress = handle.AddrOfPinnedObject(),
                    SizeX = width,
                    SizeY = height,
                    LinePitch = width,
                    PixelType = CompanyPixelType.Mono8
                };
                var store = CreateMappingStore(
                    directory,
                    "pointer-mappings.json",
                    typeof(CompanyFrame),
                    new TypeMappingMembers { Data = "ImageAddress", Width = "SizeX", Height = "SizeY", Stride = "LinePitch", PixelFormat = "PixelType" });

                var view = ImageCollectionVisualizerTransfer.CreateView(new object[] { frame }, store);
                var metadata = view.GetMetadata(0);
                Assert(string.IsNullOrEmpty(metadata.Error), "Mapped extraction failed: " + metadata.Error);
                Assert(metadata.Metadata != null, "Mapped extraction produced no metadata.");
                Assert(metadata.Metadata!.SupportsDirectMemory, "Pointer-backed mapping should use the direct-memory path.");
                Assert(
                    metadata.Metadata.Descriptor.Width == width && metadata.Metadata.Descriptor.Height == height && metadata.Metadata.Descriptor.Stride == width,
                    "Mapped descriptor mismatch.");
                Assert(metadata.Metadata.BufferAddress == handle.AddrOfPinnedObject().ToInt64(), "Mapped buffer address mismatch.");

                var chunk = view.GetChunk(0, new VisualizerSnapshotChunkRequest { Offset = 0, Count = buffer.Length });
                AssertBytesEqual(buffer, chunk.Buffer, "Mapped pointer chunk read failed.");

                var arrayFrame = new CompanyArrayFrame
                {
                    Pixels = buffer,
                    FrameWidth = width,
                    FrameHeight = height,
                    Format = CompanyPixelType.Mono8
                };
                var arrayStore = CreateMappingStore(
                    directory,
                    "array-mappings.json",
                    typeof(CompanyArrayFrame),
                    new TypeMappingMembers { Data = "Pixels", Width = "FrameWidth", Height = "FrameHeight", PixelFormat = "Format" });
                var arrayView = ImageCollectionVisualizerTransfer.CreateView(new object[] { arrayFrame }, arrayStore);
                var arrayMetadata = arrayView.GetMetadata(0);
                Assert(string.IsNullOrEmpty(arrayMetadata.Error), "Array-backed mapped extraction failed: " + arrayMetadata.Error);
                Assert(arrayMetadata.Metadata != null && !arrayMetadata.Metadata!.SupportsDirectMemory, "Array-backed mapping should use the chunked path.");
                var arrayChunk = arrayView.GetChunk(0, new VisualizerSnapshotChunkRequest { Offset = 0, Count = buffer.Length });
                AssertBytesEqual(buffer, arrayChunk.Buffer, "Mapped array chunk read failed.");

                var ushorts = new ushort[width * height];
                for (var i = 0; i < ushorts.Length; i++)
                {
                    ushorts[i] = (ushort)(i * 100);
                }

                var ushortFrame = new CompanyUshortFrame
                {
                    Samples = ushorts,
                    FrameWidth = width,
                    FrameHeight = height,
                    Format = CompanyPixelType.Mono16
                };
                var ushortStore = CreateMappingStore(
                    directory,
                    "ushort-mappings.json",
                    typeof(CompanyUshortFrame),
                    new TypeMappingMembers { Data = "Samples", Width = "FrameWidth", Height = "FrameHeight", PixelFormat = "Format" });
                var ushortView = ImageCollectionVisualizerTransfer.CreateView(new object[] { ushortFrame }, ushortStore);
                var ushortMetadata = ushortView.GetMetadata(0);
                Assert(string.IsNullOrEmpty(ushortMetadata.Error), "Ushort-backed mapped extraction failed: " + ushortMetadata.Error);
                Assert(ushortMetadata.Metadata != null && ushortMetadata.Metadata!.Descriptor.PixelFormat == RawPixelFormat.Mono16, "Ushort mapping should produce a Mono16 descriptor.");
                var ushortChunk = ushortView.GetChunk(0, new VisualizerSnapshotChunkRequest { Offset = 0, Count = ushorts.Length * 2 });
                var expectedUshortBytes = RawBufferSnapshot.FromUInt16Array(ushorts, ushortMetadata.Metadata!.Descriptor).Buffer;
                AssertBytesEqual(expectedUshortBytes, ushortChunk.Buffer, "Mapped ushort chunk read failed.");
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

        private static void TypeMappingAppliesEnumPixelFormatMap()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            var width = 8;
            var height = 4;
            var stride = ((width * 12) + 7) / 8;
            var buffer = new byte[stride * height];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                Directory.CreateDirectory(directory);
                var frame = new CompanyFrame
                {
                    ImageAddress = handle.AddrOfPinnedObject(),
                    SizeX = width,
                    SizeY = height,
                    LinePitch = stride,
                    PixelType = CompanyPixelType.Mono12
                };
                var store = CreateMappingStore(
                    directory,
                    "enum-mappings.json",
                    typeof(CompanyFrame),
                    new TypeMappingMembers { Data = "ImageAddress", Width = "SizeX", Height = "SizeY", Stride = "LinePitch", PixelFormat = "PixelType" });

                var view = ImageCollectionVisualizerTransfer.CreateView(new object[] { frame }, store);
                var metadata = view.GetMetadata(0);
                Assert(string.IsNullOrEmpty(metadata.Error), "Enum-mapped extraction failed: " + metadata.Error);
                Assert(metadata.Metadata != null, "Enum-mapped extraction produced no metadata.");
                Assert(
                    metadata.Metadata!.Descriptor.PixelFormat == RawPixelFormat.Mono12PackedLsb,
                    "pixelFormatMap should map Mono12 to Mono12PackedLsb.");
                Assert(metadata.Metadata.Descriptor.ValidBits == 12, "Mono12PackedLsb should default to 12 valid bits.");
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

        private static void TypeMappingFailureIncludesMemberInventory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var widget = new UnsupportedWidget
                {
                    Handle = new IntPtr(0x1234),
                    Counter = 7,
                    Mode = CompanyPixelType.Bgr
                };
                var store = new TypeMappingStore(null, Path.Combine(directory, "missing-mappings.json"));
                var view = ImageCollectionVisualizerTransfer.CreateView(new object[] { widget }, store);
                var metadata = view.GetMetadata(0);
                Assert(metadata.Metadata == null, "Unsupported type should not produce metadata.");
                Assert(!string.IsNullOrEmpty(metadata.Error), "Unsupported type should report an error row.");
                Assert(metadata.MemberInventory != null && metadata.MemberInventory!.Count >= 3, "Failure should include the member inventory.");

                var handle = FindInventoryItem(metadata.MemberInventory!, "Handle");
                Assert(handle != null && handle!.Kind == "Field" && handle.TypeName == "IntPtr" && handle.SampleValue.Contains("0x"), "Inventory should describe the Handle field.");
                var counter = FindInventoryItem(metadata.MemberInventory!, "Counter");
                Assert(counter != null && counter!.SampleValue == "7", "Inventory should include sample values.");
                var mode = FindInventoryItem(metadata.MemberInventory!, "Mode");
                Assert(
                    mode != null && mode!.Kind == "Property" && mode.EnumValues != null && mode.EnumValues.Contains("Mono12"),
                    "Inventory should list enum values for enum members.");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void TypeMappingMissingMemberFailsVisibly()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var frame = new CompanyFrame
                {
                    ImageAddress = new IntPtr(0x1234),
                    SizeX = 8,
                    SizeY = 4,
                    LinePitch = 8,
                    PixelType = CompanyPixelType.Mono8
                };
                var store = CreateMappingStore(
                    directory,
                    "renamed-mappings.json",
                    typeof(CompanyFrame),
                    new TypeMappingMembers { Data = "ImageAddress", Width = "RenamedWidth", Height = "SizeY" });

                var view = ImageCollectionVisualizerTransfer.CreateView(new object[] { frame }, store);
                var metadata = view.GetMetadata(0);
                Assert(metadata.Metadata == null, "A mapping with a missing member must not produce metadata.");
                Assert(metadata.Error.Contains("RenamedWidth"), "The failure reason should name the missing member.");
                Assert(metadata.MemberInventory != null && metadata.MemberInventory!.Count > 0, "Mapped extraction failure should still include the member inventory.");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void VisionInferenceAutoOpensDirectPointerShape()
        {
            var inventory = new List<VisualizerMemberInventoryItem>
            {
                InventoryItem("ImageAddress", "IntPtr", "0x1234"),
                InventoryItem("SizeX", "Int32", "640"),
                InventoryItem("SizeY", "Int32", "480"),
                InventoryItem("LinePitch", "Int32", "1920"),
                InventoryItem("PixelType", "CompanyPixelType", "Bgr")
            };

            var inference = VisionMemberInference.Infer(inventory, "Contoso.Camera.CompanyFrame");
            Assert(inference.CanAutoOpen, "A complete pointer-backed company frame should pass the automatic-open gate.");
            Assert(inference.ConfidenceScore >= 90, "Complete company frame confidence should be at least 90.");
            Assert(inference.Members.Data == "ImageAddress", "Automatic inference selected the wrong data member.");
            Assert(inference.Members.Width == "SizeX" && inference.Members.Height == "SizeY", "Automatic inference selected the wrong dimensions.");
            Assert(inference.Members.Stride == "LinePitch", "Automatic inference selected the wrong stride.");
            Assert(inference.Members.BufferLength == null, "SizeX/SizeY must not be mistaken for the total buffer length.");
            Assert(inference.PixelFormat == RawPixelFormat.BGR24, "Bgr should resolve to BGR24 without a full type mapping.");
        }

        private static void VisionInferenceSupportsOneLevelNestedMembers()
        {
            var inventory = new List<VisualizerMemberInventoryItem>
            {
                InventoryItem("Storage.Buffer", "UIntPtr", "0x1234"),
                InventoryItem("Info.Width", "Int32", "320"),
                InventoryItem("Info.Height", "Int32", "240"),
                InventoryItem("Info.Stride", "Int32", "320"),
                InventoryItem("Info.PixelFormat", "VendorFormat", "Mono8")
            };

            var inference = VisionMemberInference.Infer(inventory, "Vendor.NestedImageFrame");
            Assert(inference.CanAutoOpen, "One-level nested image members should pass the automatic-open gate.");
            Assert(inference.Members.Data == "Storage.Buffer", "Nested data path inference failed.");
            Assert(inference.Members.Width == "Info.Width" && inference.Members.Height == "Info.Height", "Nested dimension path inference failed.");
            Assert(inference.PixelFormat == RawPixelFormat.Mono8, "Nested pixel format inference failed.");
        }

        private static void VisionInferenceRequestsOnlyAmbiguousPixelFormat()
        {
            var inventory = new List<VisualizerMemberInventoryItem>
            {
                InventoryItem("Data", "IntPtr", "0x1234"),
                InventoryItem("Width", "Int32", "128"),
                InventoryItem("Height", "Int32", "64"),
                InventoryItem("Stride", "Int32", "192"),
                InventoryItem("PixelFormat", "VendorFormat", "Mono12")
            };

            var inference = VisionMemberInference.Infer(inventory, "Vendor.ImageFrame");
            Assert(inference.HasRequiredMembers, "Ambiguous format should not discard otherwise complete member inference.");
            Assert(inference.RequiresPixelFormatMapping, "Mono12 must remain explicit because packed and unpacked layouts differ.");
            Assert(!inference.CanAutoOpen, "An ambiguous pixel format must not auto-open.");
            Assert(inference.MissingRoles.Count == 0, "Pixel format ambiguity should not ask the user to remap data or dimensions.");
        }

        private static void VisionInferenceHidesLowConfidenceShape()
        {
            var inventory = new List<VisualizerMemberInventoryItem>
            {
                InventoryItem("Handle", "IntPtr", "0x1234"),
                InventoryItem("Counter", "Int32", "7")
            };

            var inference = VisionMemberInference.Infer(inventory, "Vendor.Widget");
            Assert(inference.ConfidenceScore < 40, "A pointer plus unrelated counter should remain below the visible-candidate threshold.");
            Assert(!inference.CanAutoOpen, "A low-confidence object must never auto-open.");
        }

        private static void TypeMappingReadsOneLevelNestedMemberPaths()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            var buffer = new byte[] { 1, 2, 3, 4 };
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                Directory.CreateDirectory(directory);
                var frame = new NestedCompanyFrame
                {
                    Storage = new NestedStorage { ImageAddress = handle.AddrOfPinnedObject() },
                    Info = new NestedFrameInfo
                    {
                        Width = 2,
                        Height = 2,
                        Stride = 2,
                        PixelType = CompanyPixelType.Mono8
                    }
                };
                var store = CreateMappingStore(
                    directory,
                    "nested-mappings.json",
                    typeof(NestedCompanyFrame),
                    new TypeMappingMembers
                    {
                        Data = "Storage.ImageAddress",
                        Width = "Info.Width",
                        Height = "Info.Height",
                        Stride = "Info.Stride",
                        PixelFormat = "Info.PixelType"
                    });

                var view = ImageCollectionVisualizerTransfer.CreateView(new object[] { frame }, store);
                var metadata = view.GetMetadata(0);
                Assert(string.IsNullOrEmpty(metadata.Error), "One-level nested mapping failed: " + metadata.Error);
                Assert(metadata.Metadata != null && metadata.Metadata.Descriptor.Width == 2, "Nested mapping descriptor failed.");
                var chunk = view.GetChunk(0, new VisualizerSnapshotChunkRequest { Offset = 0, Count = buffer.Length });
                AssertBytesEqual(buffer, chunk.Buffer, "Nested mapped pointer data failed.");
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

        private static VisualizerMemberInventoryItem InventoryItem(string name, string typeName, string sampleValue)
        {
            return new VisualizerMemberInventoryItem
            {
                Name = name,
                Kind = "Member",
                TypeName = typeName,
                SampleValue = sampleValue
            };
        }

        private static TypeMappingStore CreateMappingStore(string directory, string fileName, Type mappedType, TypeMappingMembers members)
        {
            var path = Path.Combine(directory, fileName);
            var store = new TypeMappingStore(null, path);
            var file = new TypeMappingFile();
            file.Mappings.Add(new TypeMapping
            {
                TypeName = mappedType.FullName!,
                AssemblyName = mappedType.Assembly.GetName().Name!,
                Members = members,
                PixelFormatMap = CompanyPixelFormatMap(),
                ByteOrder = "LittleEndian"
            });
            store.Save(file);
            return new TypeMappingStore(null, path);
        }

        private static Dictionary<string, string> CompanyPixelFormatMap()
        {
            return new Dictionary<string, string>
            {
                { "Mono8", "Mono8" },
                { "Mono12", "Mono12PackedLsb" },
                { "Bgr", "BGR24" },
                { "Mono16", "Mono16" }
            };
        }

        private static VisualizerMemberInventoryItem? FindInventoryItem(List<VisualizerMemberInventoryItem> inventory, string name)
        {
            for (var i = 0; i < inventory.Count; i++)
            {
                if (inventory[i].Name == name)
                {
                    return inventory[i];
                }
            }

            return null;
        }

        private enum CompanyPixelType
        {
            Mono8,
            Mono12,
            Bgr,
            Mono16
        }

        private sealed class CompanyFrame
        {
            public IntPtr ImageAddress { get; set; }
            public int SizeX { get; set; }
            public int SizeY { get; set; }
            public int LinePitch { get; set; }
            public CompanyPixelType PixelType { get; set; }
        }

        private sealed class CompanyArrayFrame
        {
            public byte[]? Pixels;
            public int FrameWidth;
            public int FrameHeight;
            public CompanyPixelType Format;
        }

        private sealed class CompanyUshortFrame
        {
            public ushort[]? Samples;
            public int FrameWidth;
            public int FrameHeight;
            public CompanyPixelType Format;
        }

        private sealed class NestedCompanyFrame
        {
            public NestedStorage? Storage { get; set; }
            public NestedFrameInfo? Info { get; set; }
        }

        private sealed class NestedStorage
        {
            public IntPtr ImageAddress { get; set; }
        }

        private sealed class NestedFrameInfo
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int Stride { get; set; }
            public CompanyPixelType PixelType { get; set; }
        }

        private sealed class UnsupportedWidget
        {
            public IntPtr Handle;
            public int Counter;
            public CompanyPixelType Mode { get; set; }
        }

        private static BufferInterpretationCandidate ScoreDraft(RawImageSource source, RawImageDescriptor descriptor)
        {
            var drafts = BufferInterpretationCandidateGenerator.Generate(source.Length, descriptor);
            for (var i = 0; i < drafts.Count; i++)
            {
                var draft = drafts[i].Descriptor;
                if (draft.Width == descriptor.Width
                    && draft.Height == descriptor.Height
                    && draft.Stride == descriptor.Stride
                    && draft.PixelFormat == descriptor.PixelFormat
                    && draft.ByteOrder == descriptor.ByteOrder
                    && draft.ValidBits == descriptor.ValidBits)
                {
                    return BufferInterpretationScorer.Score(source, drafts[i], CancellationToken.None);
                }
            }

            throw new InvalidOperationException("Candidate generation did not include the requested descriptor.");
        }

        private static BufferInterpretationCandidate? FindCandidate(
            BufferDiagnosisResult result,
            int width,
            int height,
            int stride,
            RawPixelFormat pixelFormat)
        {
            for (var i = 0; i < result.Candidates.Count; i++)
            {
                var descriptor = result.Candidates[i].Descriptor;
                if (descriptor.Width == width
                    && descriptor.Height == height
                    && descriptor.Stride == stride
                    && descriptor.PixelFormat == pixelFormat)
                {
                    return result.Candidates[i];
                }
            }

            return null;
        }

        private static bool HasReasonContaining(BufferInterpretationCandidate candidate, string text)
        {
            for (var i = 0; i < candidate.Reasons.Count; i++)
            {
                if (candidate.Reasons[i].Contains(text))
                {
                    return true;
                }
            }

            return false;
        }

        private static byte[] CreateNoisyMono8Buffer(int width, int height, int stride, int seed)
        {
            var buffer = new byte[(stride * (height - 1)) + width];
            new Random(seed).NextBytes(buffer);
            return buffer;
        }

        private static byte[] CreateSmoothMono8Buffer(int width, int height, int stride)
        {
            var buffer = new byte[(stride * (height - 1)) + width];
            for (var y = 0; y < height; y++)
            {
                var row = y * stride;
                for (var x = 0; x < width; x++)
                {
                    buffer[row + x] = (byte)(127 + (120 * Math.Sin((x * 0.2) + (y * 0.3))));
                }
            }

            return buffer;
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
