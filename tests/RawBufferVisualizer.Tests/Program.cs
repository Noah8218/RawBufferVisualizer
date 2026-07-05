using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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
                RawBufferViewCreatesDescriptorAndChunks();
                BitmapVisualizerObjectSourceCreatesTransfer();
                MatVisualizerObjectSourceCreatesTransfer();
                EmguCvMatVisualizerObjectSourceCreatesTransfer();
                VisualizerBridgeWritesLaunchSnapshot();
                VisualizerBridgePreparesChunkedLaunchSnapshot();
                VisualizerBridgePreparesMultiLaunchSnapshots();
                ViewerPathResolverFindsConfiguredViewer();
                VisualizerHandoffInboxRoundTripsMetadataPath();
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

                var transfer = BitmapVisualizerTransfer.CreateTransfer((object)bitmap, "bitmap0");

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

        private static void MatVisualizerObjectSourceCreatesTransfer()
        {
            using (var mat = new Mat(1, 2, MatType.CV_8UC3, new Scalar(3, 2, 1)))
            {
                var transfer = OpenCvSharpMatVisualizerTransfer.CreateTransfer(mat, "mat0");

                Assert(transfer.DisplayName == "mat0", "Mat visualizer display name failed.");
                Assert(transfer.SourceType == typeof(Mat).FullName, "Mat visualizer source type failed.");
                Assert(transfer.Descriptor.Width == 2 && transfer.Descriptor.Height == 1, "Mat visualizer dimensions failed.");
                Assert(transfer.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Mat visualizer pixel format failed.");
                Assert(transfer.Buffer.Length >= 6, "Mat visualizer buffer length failed.");
            }
        }

        private static void EmguCvMatVisualizerObjectSourceCreatesTransfer()
        {
            using (var mat = new Emgu.CV.Mat(
                1,
                2,
                Emgu.CV.DepthType.Cv8U,
                3,
                new byte[] { 3, 2, 1, 6, 5, 4 },
                6))
            {
                var transfer = EmguCvMatVisualizerTransfer.CreateTransfer(mat, "emgu0");

                Assert(transfer.DisplayName == "emgu0", "Emgu Mat visualizer display name failed.");
                Assert(transfer.SourceType == "Emgu.CV.Mat", "Emgu Mat visualizer source type failed.");
                Assert(transfer.Descriptor.Width == 2 && transfer.Descriptor.Height == 1, "Emgu Mat visualizer dimensions failed.");
                Assert(transfer.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Emgu Mat visualizer pixel format failed.");
                Assert(transfer.Buffer.Length == 6 && transfer.Buffer[0] == 3 && transfer.Buffer[5] == 4, "Emgu Mat visualizer buffer failed.");
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

        private static void VisualizerHandoffInboxRoundTripsMetadataPath()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var metadataPath = Path.Combine(directory, "camera.rbuf.json");
                File.WriteAllText(metadataPath, "{}");

                var requestPath = VisualizerHandoffInbox.WriteSnapshotRequest(metadataPath);
                var restored = VisualizerHandoffInbox.ReadSnapshotRequest(requestPath);
                var request = VisualizerHandoffInbox.ReadSnapshotRequestInfo(requestPath);

                var typedRequestPath = VisualizerHandoffInbox.WriteSnapshotRequest(metadataPath, "bitmapBgr24", "System.Drawing.Bitmap");
                var typedRequest = VisualizerHandoffInbox.ReadSnapshotRequestInfo(typedRequestPath);

                Assert(File.Exists(requestPath), "Handoff request file was not created.");
                Assert(restored == Path.GetFullPath(metadataPath), "Handoff metadata path roundtrip failed.");
                Assert(request.MetadataPath == Path.GetFullPath(metadataPath), "Handoff request info metadata path failed.");
                Assert(typedRequest.DisplayName == "bitmapBgr24", "Handoff display name roundtrip failed.");
                Assert(typedRequest.SourceType == "System.Drawing.Bitmap", "Handoff source type roundtrip failed.");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
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
