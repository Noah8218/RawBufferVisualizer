using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using RawBufferVisualizer.BitmapAdapter;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.Tests
{
    internal static class PixelCorrectnessTests
    {
        public static void RunAll()
        {
            var failures = new List<Exception>();
            Run(nameof(BitmapAdapterPreservesSignedRows), BitmapAdapterPreservesSignedRows, failures);
            Run(nameof(BitmapTransfersPreservePaletteAndAlpha), BitmapTransfersPreservePaletteAndAlpha, failures);
            Run(nameof(FloatSourcesUseTheSameScale), FloatSourcesUseTheSameScale, failures);
            Run(nameof(BayerSamplesPreserveDemosaicedColor), BayerSamplesPreserveDemosaicedColor, failures);
            Run(nameof(FloatPixelDescriptionsRoundTrip), FloatPixelDescriptionsRoundTrip, failures);
            Run(nameof(StatisticsUseFiniteRawValues), StatisticsUseFiniteRawValues, failures);
            Run(nameof(StatisticsPreserveFormatAndGeneratedViewSemantics), StatisticsPreserveFormatAndGeneratedViewSemantics, failures);
            Run(nameof(FloatRenderingDoesNotAllocatePerPixel), FloatRenderingDoesNotAllocatePerPixel, failures);
            if (failures.Count != 0) throw new AggregateException(failures);
        }

        private static void Run(string name, Action test, List<Exception> failures)
        {
            try
            {
                test();
                Console.WriteLine("PASS " + name);
            }
            catch (Exception ex)
            {
                failures.Add(new InvalidOperationException(name, ex));
                Console.WriteLine("FAIL " + name + ": " + ex.Message);
            }
        }

        private static void BitmapAdapterPreservesSignedRows()
        {
            var allocation = Marshal.AllocHGlobal(64);
            try
            {
                var guard = new byte[64];
                for (var i = 0; i < guard.Length; i++) guard[i] = 0xCC;
                Marshal.Copy(guard, 0, allocation, guard.Length);
                Marshal.Copy(new byte[] { 60, 50, 40, 0, 30, 20, 10, 0 }, 0, allocation, 8);
                using (var bitmap = new Bitmap(1, 2, -4, PixelFormat.Format24bppRgb, IntPtr.Add(allocation, 4)))
                {
                    var expected = new byte[] { 30, 20, 10, 0, 60, 50, 40, 0 };
                    BytesEqual(expected, BitmapSnapshot.FromBitmap(bitmap).Buffer, "SDK signed rows");
                    BytesEqual(expected, BitmapVisualizerTransfer.CreateTransfer(bitmap).Buffer, "Registered signed rows");
                    AssertBitmapPaths(bitmap, new byte[] { 30, 20, 10, 255, 60, 50, 40, 255 });
                }
            }
            finally
            {
                Marshal.FreeHGlobal(allocation);
            }
        }

        private static void BitmapTransfersPreservePaletteAndAlpha()
        {
            using (var bitmap = new Bitmap(3, 2, PixelFormat.Format8bppIndexed))
            {
                var palette = bitmap.Palette;
                for (var i = 0; i < palette.Entries.Length; i++) palette.Entries[i] = Color.FromArgb(255, i, i, i);
                bitmap.Palette = palette;
                SetBitmapRows(bitmap, new byte[] { 1, 2, 3, 3, 2, 1 }, 3);
                Assert(BitmapSnapshot.FromBitmap(bitmap).Descriptor.PixelFormat == RawPixelFormat.Mono8, "Identity grayscale should retain Mono8.");
                Assert(BitmapVisualizerTransfer.CreateView(bitmap).Descriptor.PixelFormat == RawPixelFormat.Mono8, "Registered identity grayscale should retain Mono8.");
                palette.Entries[1] = Color.FromArgb(255, 240, 20, 10);
                palette.Entries[2] = Color.FromArgb(128, 40, 80, 120);
                palette.Entries[3] = Color.FromArgb(0, 0, 0, 0);
                bitmap.Palette = palette;
                var expected = new byte[] { 10, 20, 240, 255, 120, 80, 40, 128, 0, 0, 0, 0, 0, 0, 0, 0, 120, 80, 40, 128, 10, 20, 240, 255 };
                AssertBitmapPaths(bitmap, expected);
            }

            foreach (var format in new[] { PixelFormat.Format32bppArgb, PixelFormat.Format32bppPArgb, PixelFormat.Format32bppRgb })
            {
                using (var bitmap = new Bitmap(3, 1, format))
                {
                    var premultiplied = format == PixelFormat.Format32bppPArgb;
                    var opaque = format == PixelFormat.Format32bppRgb;
                    var input = premultiplied
                        ? new byte[] { 0, 0, 128, 128, 0, 0, 0, 0, 10, 20, 30, 255 }
                        : new byte[] { 10, 20, 240, 128, 0, 0, 0, 0, 10, 20, 30, 255 };
                    var expected = premultiplied
                        ? new byte[] { 0, 0, 255, 128, 0, 0, 0, 0, 10, 20, 30, 255 }
                        : (byte[])input.Clone();
                    if (opaque) for (var i = 3; i < expected.Length; i += 4) expected[i] = 255;
                    SetBitmapRows(bitmap, input, 12);
                    AssertBitmapPaths(bitmap, expected);
                }
            }
        }

        private static void AssertBitmapPaths(Bitmap bitmap, byte[] expected)
        {
            var snapshot = BitmapSnapshot.FromBitmap(bitmap);
            BytesEqual(expected, RawBufferRenderer.Render(snapshot.Buffer, snapshot.Descriptor).Bgra32, "SDK " + bitmap.PixelFormat);
            var view = BitmapVisualizerTransfer.CreateView(bitmap);
            var full = BitmapVisualizerTransfer.CreateTransfer(bitmap);
            BytesEqual(expected, RawBufferRenderer.Render(full.Buffer, full.Descriptor).Bgra32, "Registered full " + bitmap.PixelFormat);
            var assembled = new byte[checked((int)view.BufferLength)];
            for (var offset = 0; offset < assembled.Length;)
            {
                var chunk = BitmapVisualizerTransfer.CreateChunk(view, new VisualizerSnapshotChunkRequest { Offset = offset, Count = 3 });
                Assert(chunk.Buffer.Length > 0, "Chunk must make progress.");
                Buffer.BlockCopy(chunk.Buffer, 0, assembled, offset, chunk.Buffer.Length);
                offset += chunk.Buffer.Length;
            }
            BytesEqual(expected, RawBufferRenderer.Render(assembled, view.Descriptor).Bgra32, "Unaligned chunks " + bitmap.PixelFormat);
            var preview = BitmapVisualizerTransfer.CreatePreview(view, new VisualizerSnapshotChunkRequest { MaximumWidth = 128, MaximumHeight = 128 });
            BytesEqual(expected, preview.Buffer, "Preview " + bitmap.PixelFormat);
        }

        private static void SetBitmapRows(Bitmap bitmap, byte[] pixels, int rowBytes)
        {
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try
            {
                for (var y = 0; y < bitmap.Height; y++)
                    Marshal.Copy(pixels, y * rowBytes, new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride), rowBytes);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static void FloatSourcesUseTheSameScale()
        {
            foreach (var order in new[] { RawByteOrder.LittleEndian, RawByteOrder.BigEndian })
            {
                var descriptor = Descriptor(3, 2, RawPixelFormat.Float32, 4);
                descriptor.ByteOrder = order;
                var data = FloatBytes(new[] { 100f, 150f, 200f, -100f, float.NaN, float.PositiveInfinity }, order);
                WithSources(data, descriptor, source =>
                {
                    var options = source.CreateRenderOptions();
                    Assert(options.BlackLevel == -100 && options.WhiteLevel == 200, "Float bounds must be [-100, 200] for " + source.GetType().Name);
                    BytesEqual(new byte[] { 170, 170, 170, 255, 212, 212, 212, 255, 255, 255, 255, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255 },
                        source.RenderTile(0, 0, 3, 2, options).Bgra32, "Float source render");
                });
                var invalid = FloatBytes(new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.NaN, float.NaN, float.NaN }, order);
                WithSources(invalid, descriptor, source =>
                {
                    var options = source.CreateRenderOptions();
                    Assert(options.BlackLevel == 0 && options.WhiteLevel == 1, "Non-finite-only range must be [0, 1].");
                });
            }

            var largeDescriptor = Descriptor(1025, 65, RawPixelFormat.Float32, 4);
            var values = new float[1025 * 65];
            values[0] = -10;
            values[values.Length - 1] = 90;
            values[1025 + 1] = 1000; // Between the bounded column samples.
            RawRenderOptions? first = null;
            WithSources(FloatBytes(values, RawByteOrder.LittleEndian), largeDescriptor, source =>
            {
                var options = source.CreateRenderOptions();
                Assert(options.BlackLevel == -10 && options.WhiteLevel == 90, "Large images must use the bounded range policy.");
                if (first == null) first = options;
                Assert(options.BlackLevel == first.BlackLevel && options.WhiteLevel == first.WhiteLevel, "Bounded sample locations differ between sources.");
            });
        }

        private static void BayerSamplesPreserveDemosaicedColor()
        {
            foreach (var format in new[] { RawPixelFormat.BayerRGGB8, RawPixelFormat.BayerGRBG8, RawPixelFormat.BayerGBRG8, RawPixelFormat.BayerBGGR8 })
            {
                var descriptor = Descriptor(9, 7, format, 1);
                descriptor.Stride = 12;
                var data = new byte[descriptor.Stride * descriptor.Height];
                for (var y = 0; y < descriptor.Height; y++)
                    for (var x = 0; x < descriptor.Width; x++)
                        data[y * descriptor.Stride + x] = (byte)(BayerChannel(format, x, y) == 0 ? 180 + x : BayerChannel(format, x, y) == 1 ? 90 + y : 30 + x + y);
                CheckBayerPreviews(data, descriptor);
                WithSources(data, descriptor, source =>
                {
                    foreach (var step in new[] { 2, 3, 5 })
                        foreach (var origin in new[] { 0, 1 })
                        {
                            var width = descriptor.Width - origin;
                            var height = descriptor.Height - origin;
                            var rendered = source.RenderTileSampled(origin, origin, width, height, step, source.CreateRenderOptions());
                            for (var y = 0; y < rendered.Height; y++)
                                for (var x = 0; x < rendered.Width; x++)
                                {
                                    var sourceX = origin + x * step;
                                    var sourceY = origin + y * step;
                                    var offset = (y * rendered.Width + x) * 4;
                                    Assert(rendered.Bgra32[offset] == ExpectedBayer(data, descriptor, sourceX, sourceY, 2)
                                        && rendered.Bgra32[offset + 1] == ExpectedBayer(data, descriptor, sourceX, sourceY, 1)
                                        && rendered.Bgra32[offset + 2] == ExpectedBayer(data, descriptor, sourceX, sourceY, 0)
                                        && rendered.Bgra32[offset + 3] == 255, "Bayer mismatch: " + format + ", step " + step + ", origin " + origin + ", source " + source.GetType().Name);
                                }
                        }
                });
            }
        }

        private static void CheckBayerPreviews(byte[] data, RawImageDescriptor descriptor)
        {
            foreach (var maximum in new[] { 3, 5, 12 })
            {
                var step = Math.Max((descriptor.Width + maximum - 1) / maximum, (descriptor.Height + maximum - 1) / maximum);
                var preview = VisualizerSampledPreview.Create(data, descriptor, "test", "Bayer", maximum, maximum);
                var pinned = GCHandle.Alloc(data, GCHandleType.Pinned);
                try
                {
                    var pointerPreview = VisualizerSampledPreview.Create(pinned.AddrOfPinnedObject(), data.Length, descriptor, "test", "Bayer", maximum, maximum);
                    BytesEqual(preview.Buffer, pointerPreview.Buffer, "Bayer preview byte/pointer agreement");
                }
                finally
                {
                    pinned.Free();
                }
                for (var y = 0; y < preview.Descriptor.Height; y++)
                    for (var x = 0; x < preview.Descriptor.Width; x++)
                        for (var channel = 0; channel < 3; channel++)
                            Assert(preview.Buffer[(y * preview.Descriptor.Width + x) * 4 + channel] == ExpectedBayer(data, descriptor, x * step, y * step, 2 - channel), "Bayer preview color mismatch.");
            }
        }

        private static int BayerChannel(RawPixelFormat format, int x, int y)
        {
            var grids = format == RawPixelFormat.BayerRGGB8 ? new[] { 0, 1, 1, 2 }
                : format == RawPixelFormat.BayerGRBG8 ? new[] { 1, 0, 2, 1 }
                : format == RawPixelFormat.BayerGBRG8 ? new[] { 1, 2, 0, 1 } : new[] { 2, 1, 1, 0 };
            return grids[(y % 2) * 2 + x % 2];
        }

        private static byte ExpectedBayer(byte[] data, RawImageDescriptor descriptor, int x, int y, int channel)
        {
            var sum = 0;
            var count = 0;
            for (var row = Math.Max(0, y - 1); row <= Math.Min(descriptor.Height - 1, y + 1); row++)
                for (var column = Math.Max(0, x - 1); column <= Math.Min(descriptor.Width - 1, x + 1); column++)
                    if (BayerChannel(descriptor.PixelFormat, column, row) == channel)
                    {
                        sum += data[row * descriptor.Stride + column];
                        count++;
                    }
            return count == 0 ? (byte)0 : (byte)(sum / count);
        }

        private static void FloatPixelDescriptionsRoundTrip()
        {
            var values = new[] { 0.0001f, -0.0002f, 1.2345678f, float.Epsilon, float.MaxValue };
            foreach (var order in new[] { RawByteOrder.LittleEndian, RawByteOrder.BigEndian })
            {
                var descriptor = Descriptor(values.Length, 1, RawPixelFormat.Float32, 4);
                descriptor.ByteOrder = order;
                var data = FloatBytes(values, order);
                WithSources(data, descriptor, source =>
                {
                    for (var x = 0; x < values.Length; x++)
                    {
                        var text = source.DescribePixel(x, 0);
                        var start = text.IndexOf("Value=", StringComparison.Ordinal) + 6;
                        var end = text.IndexOf(',', start);
                        var actual = float.Parse(text.Substring(start, end - start), System.Globalization.CultureInfo.InvariantCulture);
                        Assert(actual.Equals(values[x]), "Float description lost precision: " + text);
                        var compact = RawPixelInspector.DescribeValue(data, descriptor, x, 0);
                        Assert(float.Parse(compact, System.Globalization.CultureInfo.InvariantCulture).Equals(values[x]), "Compact raw value lost precision.");
                    }
                });
            }
        }

        private static void StatisticsUseFiniteRawValues()
        {
            foreach (var order in new[] { RawByteOrder.LittleEndian, RawByteOrder.BigEndian })
            {
                var values = new[] { 0.0001f, 0.0002f, 0.0003f, float.NaN, float.PositiveInfinity, float.NegativeInfinity };
                var descriptor = Descriptor(3, 2, RawPixelFormat.Float32, 4);
                descriptor.ByteOrder = order;
                var expectedMean = ((double)values[0] + values[1] + values[2]) / 3;
                var expectedVariance = 0.0;
                for (var i = 0; i < 3; i++) expectedVariance += Math.Pow(values[i] - expectedMean, 2) / 3;
                WithSources(FloatBytes(values, order), descriptor, source =>
                {
                    var stats = source.GetStatistics(0, 0, 3, 2);
                    Assert(stats.Count == 3 && stats.Minimum == values[0] && stats.Maximum == values[2], "Statistics must use finite raw values.");
                    Assert(Math.Abs(stats.Mean - expectedMean) < 1e-15 && Math.Abs(stats.StandardDeviation - Math.Sqrt(expectedVariance)) < 1e-15, "Small Float32 statistics lost precision.");
                    var empty = source.GetStatistics(0, 1, 3, 1);
                    Assert(empty.Count == 0 && double.IsNaN(empty.Mean) && double.IsNaN(empty.StandardDeviation), "Non-finite-only statistics must be empty.");
                });

                descriptor = Descriptor(3, 1, RawPixelFormat.Int32, 4);
                descriptor.ByteOrder = order;
                var data = new byte[12];
                for (var i = 0; i < 3; i++)
                {
                    var bytes = BitConverter.GetBytes(int.MaxValue - 2 + i);
                    if ((order == RawByteOrder.BigEndian) == BitConverter.IsLittleEndian) Array.Reverse(bytes);
                    Buffer.BlockCopy(bytes, 0, data, i * 4, 4);
                }
                WithSources(data, descriptor, source =>
                {
                    var stats = source.GetStatistics(0, 0, 3, 1);
                    Assert(stats.Mean == int.MaxValue - 1 && Math.Abs(stats.StandardDeviation - Math.Sqrt(2.0 / 3)) < 1e-12, "Large-value variance suffered cancellation.");
                });
            }
        }

        private static void StatisticsPreserveFormatAndGeneratedViewSemantics()
        {
            foreach (var format in new[] { RawPixelFormat.Mono8, RawPixelFormat.Binary, RawPixelFormat.BayerRGGB8, RawPixelFormat.BayerGRBG8, RawPixelFormat.BayerGBRG8, RawPixelFormat.BayerBGGR8 })
                WithSources(new byte[] { 1, 2, 3 }, Descriptor(3, 1, format, 1), source => Assert(source.GetStatistics(1, 0, 2, 1).Mean == 2.5, "Byte-format statistics must use raw scalar values."));
            foreach (var format in new[] { RawPixelFormat.RGB24, RawPixelFormat.BGR24, RawPixelFormat.BGRA32 })
            {
                var bgra = format == RawPixelFormat.BGRA32;
                WithSources(bgra ? new byte[] { 1, 2, 2, 0 } : new byte[] { 1, 2, 2 }, Descriptor(1, 1, format, bgra ? 4 : 3),
                    source => Assert(source.GetStatistics(0, 0, 1, 1).Mean == (bgra ? 5.0 / 3 : 1), "Existing color scalar semantics changed."));
            }
            foreach (var bits in new[] { 10, 12 })
            {
                var descriptor = Descriptor(5, 1, bits == 10 ? RawPixelFormat.Mono10PackedLsb : RawPixelFormat.Mono12PackedLsb, 1);
                descriptor.Stride = (5 * bits + 7) / 8;
                descriptor.ValidBits = bits;
                var data = new byte[descriptor.Stride];
                for (var x = 0; x < 5; x++)
                    for (var bit = 0; bit < bits; bit++)
                        if (((100 + x) & (1 << bit)) != 0) data[(x * bits + bit) / 8] |= (byte)(1 << ((x * bits + bit) % 8));
                WithSources(data, descriptor, source => Assert(source.GetStatistics(1, 0, 3, 1).Mean == 102, "Packed statistics lost the bit offset."));
            }
            using (var left = RawImageSource.FromMemory(new byte[] { 240, 240 }, Descriptor(2, 1, RawPixelFormat.Mono8, 1)))
            using (var right = RawImageSource.FromMemory(new byte[] { 30, 30 }, Descriptor(2, 1, RawPixelFormat.Mono8, 1)))
            using (var difference = new RawImageDifferenceSource(left, right))
            using (var split = new RawImageSplitSource(left, right))
            {
                Assert(difference.GetStatistics(0, 0, 2, 1).Mean == 210, "Difference statistics must describe the generated view.");
                Assert(split.GetStatistics(0, 0, 2, 1).Mean == 135, "Split statistics must describe both displayed halves.");
            }
        }

        private static void FloatRenderingDoesNotAllocatePerPixel()
        {
            const int width = 256;
            var descriptor = Descriptor(width, width, RawPixelFormat.Float32, 4);
            var options = new RawRenderOptions { AutoScale = false, BlackLevel = 0, WhiteLevel = 1 };
            foreach (var order in new[] { RawByteOrder.LittleEndian, RawByteOrder.BigEndian })
            {
                descriptor.ByteOrder = order;
                var data = FloatBytes(new float[width * width], order);
                RawBufferRenderer.Render(data, descriptor, options);
                var before = GC.GetAllocatedBytesForCurrentThread();
                var rendered = RawBufferRenderer.Render(data, descriptor, options);
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                GC.KeepAlive(rendered);
                Assert(allocated <= rendered.Bgra32.Length + 4096L, "Float render allocated " + allocated + " bytes for " + rendered.Bgra32.Length + " output bytes.");
            }
        }

        private static void WithSources(byte[] data, RawImageDescriptor descriptor, Action<RawImageSource> check)
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizer-PixelCorrectness-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "pixels.raw");
            File.WriteAllBytes(path, data);
            var pinned = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                using (var memory = RawImageSource.FromMemory(data, descriptor)) check(memory);
                using (var file = RawImageSource.FromFile(path, descriptor)) check(file);
                using (var live = RawImageSource.FromProcessMemory(Process.GetCurrentProcess().Id, pinned.AddrOfPinnedObject().ToInt64(), data.Length, descriptor)) check(live);
            }
            finally
            {
                pinned.Free();
                File.Delete(path);
                Directory.Delete(directory);
            }
        }

        private static byte[] FloatBytes(float[] values, RawByteOrder order)
        {
            var data = new byte[values.Length * 4];
            for (var i = 0; i < values.Length; i++)
            {
                var bytes = BitConverter.GetBytes(values[i]);
                if ((order == RawByteOrder.BigEndian) == BitConverter.IsLittleEndian) Array.Reverse(bytes);
                Buffer.BlockCopy(bytes, 0, data, i * 4, 4);
            }
            return data;
        }

        private static RawImageDescriptor Descriptor(int width, int height, RawPixelFormat format, int bytesPerPixel)
        {
            return new RawImageDescriptor { Width = width, Height = height, Stride = width * bytesPerPixel, PixelFormat = format, ValidBits = bytesPerPixel == 4 ? 32 : 8, ByteOrder = RawByteOrder.LittleEndian };
        }

        private static void BytesEqual(byte[] expected, byte[] actual, string context)
        {
            Assert(expected.Length == actual.Length, context + " length mismatch.");
            for (var i = 0; i < expected.Length; i++)
                Assert(expected[i] == actual[i], context + " byte " + i + ": expected " + expected[i] + ", got " + actual[i]);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
