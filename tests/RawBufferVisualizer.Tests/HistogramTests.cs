using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.Tests
{
    internal static class HistogramTests
    {
        public static void RunAll()
        {
            ScalarValuesAndPadding();
            NumericRangesAndNonFiniteValues();
            ColorChannelsAndPackedValues();
            BayerSampling();
            LargeFileAndCancellation();
            GeneratedComparisons();
            Console.WriteLine("Histogram: 6 source/numeric/lifetime groups passed.");
        }

        private static void ScalarValuesAndPadding()
        {
            var descriptor = Descriptor(3, 2, RawPixelFormat.Mono8, 8);
            descriptor.Stride = 5;
            Across(new byte[] { 0, 1, 255, 222, 222, 0, 128, 255 }, descriptor, source =>
            {
                var histogram = source.GetHistogram();
                Check(histogram.SampleCount == 6 && !histogram.IsSampled && histogram.UsesRawValues, "Raw sample coverage");
                Check(histogram.Bins[0] == 2 && histogram.Bins[1] == 1 && histogram.Bins[128] == 1 && histogram.Bins[255] == 2 && histogram.Bins.Sum() == 6, "Padding must not be counted");
                Throws<ArgumentOutOfRangeException>(() => source.GetHistogram(RawHistogramChannel.Red));
            });
            foreach (var order in new[] { RawByteOrder.LittleEndian, RawByteOrder.BigEndian })
            {
                descriptor = Descriptor(5, 1, RawPixelFormat.Mono16, 12);
                descriptor.ByteOrder = order;
                var values = new ushort[] { 0, 16, 1024, 2048, 4095 };
                Across(Bytes(values.Select(BitConverter.GetBytes).ToArray(), order), descriptor, source =>
                {
                    var histogram = source.GetHistogram();
                    Check(histogram.LowerBound == 0 && histogram.UpperBound == 4095 && histogram.Maximum == 4095, "Mono16 raw range");
                    foreach (var bin in new[] { 0, 1, 64, 128, 255 }) Check(histogram.Bins[bin] == 1, "Mono16 bin " + bin);
                });
                descriptor.Width = 1; descriptor.Stride = 2;
                Across(Bytes(new[] { BitConverter.GetBytes((ushort)65535) }, order), descriptor, source =>
                    Check(source.GetHistogram().UpperBound == 65535, "Out-of-valid-bits data must not be clipped"));
            }
            Across(new byte[] { 0, 1, 255 }, Descriptor(3, 1, RawPixelFormat.Binary, 8), source =>
            {
                var histogram = source.GetHistogram();
                Check(histogram.Bins[1] == 1 && histogram.Bins[255] == 1, "Binary reports raw bytes, not thresholded display values");
            });
        }

        private static void NumericRangesAndNonFiniteValues()
        {
            foreach (var order in new[] { RawByteOrder.LittleEndian, RawByteOrder.BigEndian })
            {
                var descriptor = Descriptor(5, 1, RawPixelFormat.Int32, 32); descriptor.ByteOrder = order;
                Across(Bytes(new[] { int.MinValue, -123, 0, 123, int.MaxValue }.Select(BitConverter.GetBytes).ToArray(), order), descriptor, source =>
                {
                    var h = source.GetHistogram();
                    Check(h.Minimum == int.MinValue && h.Maximum == int.MaxValue && h.Bins[0] == 1 && h.Bins[127] == 1 && h.Bins[128] == 2 && h.Bins[255] == 1, "Signed extremes and integer precision");
                });
                descriptor = Descriptor(6, 1, RawPixelFormat.Float32, 32); descriptor.ByteOrder = order;
                Across(Bytes(new[] { -0.25f, 0.125f, 0.75f, float.NaN, float.PositiveInfinity, float.NegativeInfinity }.Select(BitConverter.GetBytes).ToArray(), order), descriptor, source =>
                {
                    var h = source.GetHistogram();
                    Check(h.Minimum == -0.25 && h.Maximum == 0.75 && h.Bins[0] == 1 && h.Bins[96] == 1 && h.Bins[255] == 1, "Float32 values must not pass through display quantization");
                    Check(h.ExcludedCount == 3 && h.SampleCount == 6 && h.FiniteCount == 3 && h.Bins.Sum() == 3, "Non-finite accounting");
                });
                descriptor.Width = 2; descriptor.Stride = 8;
                Across(Bytes(new[] { BitConverter.GetBytes(float.NaN), BitConverter.GetBytes(float.PositiveInfinity) }, order), descriptor, source =>
                {
                    var h = source.GetHistogram();
                    Check(h.FiniteCount == 0 && double.IsNaN(h.Minimum) && h.Bins.Sum() == 0, "All non-finite input");
                });
                Across(Bytes(new[] { BitConverter.GetBytes(-7.25f), BitConverter.GetBytes(-7.25f) }, order), descriptor, source =>
                {
                    var h = source.GetHistogram();
                    Check(h.LowerBound == -7.25 && h.UpperBound == -7.25 && h.Bins[0] == 2, "Constant negative input");
                });
            }
        }

        private static void ColorChannelsAndPackedValues()
        {
            foreach (var format in new[] { RawPixelFormat.RGB24, RawPixelFormat.BGR24, RawPixelFormat.BGRA32 })
            {
                var buffer = format == RawPixelFormat.RGB24 ? new byte[] { 10, 20, 30, 240, 150, 60 }
                    : format == RawPixelFormat.BGR24 ? new byte[] { 30, 20, 10, 60, 150, 240 } : new byte[] { 30, 20, 10, 0, 60, 150, 240, 255 };
                Across(buffer, Descriptor(2, 1, format, 8), source =>
                {
                    foreach (var channel in new[] { RawHistogramChannel.Value, RawHistogramChannel.Red, RawHistogramChannel.Green, RawHistogramChannel.Blue })
                    {
                        var h = source.GetHistogram(channel);
                        var expected = channel == RawHistogramChannel.Red ? new[] { 10, 240 } : channel == RawHistogramChannel.Blue ? new[] { 30, 60 } : new[] { 20, 150 };
                        Check(h.Bins[expected[0]] == 1 && h.Bins[expected[1]] == 1 && h.Bins.Sum() == 2, "Color order/alpha " + format + " " + channel);
                    }
                });
            }
            foreach (var bits in new[] { 10, 12 })
            {
                var max = (1 << bits) - 1;
                var descriptor = Descriptor(5, 2, bits == 10 ? RawPixelFormat.Mono10PackedLsb : RawPixelFormat.Mono12PackedLsb, bits);
                descriptor.Stride += 3;
                var data = new byte[descriptor.GetRequiredByteCount()];
                var values = new[] { 0, max / 4 + 1, max / 2 + 1, max * 3 / 4 + 1, max };
                for (var y = 0; y < 2; y++)
                    for (var x = 0; x < 5; x++)
                        for (var bit = 0; bit < bits; bit++)
                            if ((values[x] & (1 << bit)) != 0) data[y * descriptor.Stride + (x * bits + bit) / 8] |= (byte)(1 << ((x * bits + bit) % 8));
                Across(data, descriptor, source =>
                {
                    var h = source.GetHistogram();
                    Check(h.UpperBound == max && h.Bins.Sum() == 10, "Packed range/count");
                    foreach (var bin in new[] { 0, 64, 128, 192, 255 }) Check(h.Bins[bin] == 2, "Packed bit alignment " + bin);
                });
            }
        }

        private static void BayerSampling()
        {
            var data = new byte[600 * 600];
            for (var y = 0; y < 600; y++)
                for (var x = 0; x < 600; x++) data[y * 600 + x] = (byte)(10 + (y & 1) * 20 + (x & 1) * 10);
            foreach (var format in new[] { RawPixelFormat.BayerRGGB8, RawPixelFormat.BayerGRBG8, RawPixelFormat.BayerGBRG8, RawPixelFormat.BayerBGGR8 })
                Across(data, Descriptor(600, 600, format, 8), source =>
                {
                    var h = source.GetHistogram();
                    Check(h.IsSampled && h.SampleCount == 65536 && h.TotalPixels == 360000, "Bayer bounded coverage");
                    foreach (var bin in new[] { 10, 20, 30, 40 }) Check(h.Bins[bin] == 16384, "Sampling must retain all four Bayer parities");
                });
        }

        private static void LargeFileAndCancellation()
        {
            var path = Path.Combine(Path.GetTempPath(), "histogram-large-" + Guid.NewGuid().ToString("N") + ".raw");
            var descriptor = Descriptor(10000, 10000, RawPixelFormat.Mono8, 8);
            try
            {
                using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                {
                    file.SetLength(descriptor.GetRequiredByteCount());
                    file.WriteByte(12); file.Position = file.Length - 1; file.WriteByte(255);
                }
                using (var source = RawImageSource.FromFile(path, descriptor))
                {
                    var before = GC.GetAllocatedBytesForCurrentThread();
                    var h = source.GetHistogram();
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                    Check(h.SampleCount == 65536 && h.TotalPixels == 100000000 && h.Bins[12] == 1 && h.Bins[255] == 1, "Large file endpoint sampling");
                    Check(allocated < 2 * 1024 * 1024, "Histogram allocated a frame-sized buffer: " + allocated);
                    using (var cancellation = new CancellationTokenSource())
                    {
                        cancellation.Cancel();
                        Throws<OperationCanceledException>(() => source.GetHistogram(cancellationToken: cancellation.Token));
                    }
                    Console.WriteLine("Large file histogram allocated " + allocated + " bytes for 100,000,000 source pixels.");
                }
            }
            finally { File.Delete(path); }
            using (var cancellation = new CancellationTokenSource())
            {
                var source = new DisplayProbeSource(600, 600, 100) { OnPixel = () => cancellation.Cancel() };
                Throws<OperationCanceledException>(() => source.GetHistogram(cancellationToken: cancellation.Token));
                Check(source.PixelReads == 1, "Cancellation must stop before another sample");
            }
            var bytes = new byte[] { 1 };
            var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var source = RawImageSource.FromProcessMemory(Process.GetCurrentProcess().Id, pin.AddrOfPinnedObject().ToInt64(), 1, Descriptor(1, 1, RawPixelFormat.Mono8, 8));
                source.Dispose();
                Throws<RawImageSourceUnavailableException>(() => source.GetHistogram());
            }
            finally { pin.Free(); }
        }

        private static void GeneratedComparisons()
        {
            var left = new DisplayProbeSource(4, 2, 240);
            var right = new DisplayProbeSource(4, 2, 30);
            using (var difference = new RawImageDifferenceSource(left, right))
            {
                var h = difference.GetHistogram();
                Check(!h.UsesRawValues && h.Bins[210] == 8, "Difference display values");
                Check(left.RangeReads == 1 && right.RangeReads == 1, "Display ranges must be prepared once, not per pixel");
                using (var nested = new RawImageDifferenceSource(difference, left)) Check(nested.GetHistogram().Bins[30] == 8, "Nested comparison sampling");
            }
            using (var split = new RawImageSplitSource(left, right))
            {
                var h = split.GetHistogram();
                Check(!h.UsesRawValues && h.Bins[240] == 4 && h.Bins[30] == 4, "Split uses the matching half");
            }
        }

        private static void Across(byte[] buffer, RawImageDescriptor descriptor, Action<RawImageSource> check)
        {
            var path = Path.Combine(Path.GetTempPath(), "histogram-" + Guid.NewGuid().ToString("N") + ".raw");
            File.WriteAllBytes(path, buffer);
            var pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                using (var source = RawImageSource.FromMemory(buffer, descriptor)) check(source);
                using (var source = RawImageSource.FromFile(path, descriptor)) check(source);
                using (var source = RawImageSource.FromProcessMemory(Process.GetCurrentProcess().Id, pin.AddrOfPinnedObject().ToInt64(), buffer.Length, descriptor)) check(source);
            }
            finally { pin.Free(); File.Delete(path); }
        }

        private static RawImageDescriptor Descriptor(int width, int height, RawPixelFormat format, int bits)
        {
            var descriptor = new RawImageDescriptor { Width = width, Height = height, PixelFormat = format, ValidBits = bits };
            descriptor.Stride = descriptor.GetMinimumStride();
            return descriptor;
        }

        private static byte[] Bytes(byte[][] values, RawByteOrder order)
        {
            if ((order == RawByteOrder.BigEndian) == BitConverter.IsLittleEndian)
                foreach (var value in values) Array.Reverse(value);
            return values.SelectMany(value => value).ToArray();
        }

        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name);
        }

        private sealed class DisplayProbeSource : RawImageSource
        {
            private readonly byte _value;
            public int PixelReads;
            public int RangeReads;
            public Action? OnPixel;
            public DisplayProbeSource(int width, int height, byte value) : base(HistogramTests.Descriptor(width, height, RawPixelFormat.BGRA32, 8), (long)width * height * 4, null) { _value = value; }
            public override bool IsFileBacked => false;
            public override RawImageSource WithDescriptor(RawImageDescriptor descriptor) => throw new NotSupportedException();
            public override RawRenderOptions CreateRenderOptions() { RangeReads++; return new RawRenderOptions(); }
            public override RenderedImage RenderTile(int x, int y, int width, int height, RawRenderOptions? options)
            {
                Check(width == 1 && height == 1, "Generated histogram must not request the full frame");
                PixelReads++; OnPixel?.Invoke();
                return new RenderedImage(1, 1, new[] { _value, _value, _value, (byte)255 });
            }
            public override string DescribePixel(int x, int y) => throw new NotSupportedException();
            public override byte[] ReadAllBytes() => throw new NotSupportedException();
            public override void CopyRawTo(string path) => throw new NotSupportedException();
        }
    }
}
