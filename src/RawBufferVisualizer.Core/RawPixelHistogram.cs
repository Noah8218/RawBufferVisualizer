using System;
using System.Collections.Generic;
using System.Threading;

namespace RawBufferVisualizer.Core
{
    public enum RawHistogramChannel { Value, Red, Green, Blue }

    public sealed class RawPixelHistogram
    {
        public IReadOnlyList<long> Bins { get; }
        public long TotalPixels { get; }
        public int SampleCount { get; }
        public int ExcludedCount { get; }
        public int FiniteCount => SampleCount - ExcludedCount;
        public bool IsSampled => SampleCount < TotalPixels;
        public bool UsesRawValues { get; }
        public double Minimum { get; }
        public double Maximum { get; }
        public double LowerBound { get; }
        public double UpperBound { get; }

        private RawPixelHistogram(double[] values, RawImageDescriptor descriptor, bool usesRawValues, CancellationToken cancellationToken)
        {
            TotalPixels = (long)descriptor.Width * descriptor.Height;
            SampleCount = values.Length;
            UsesRawValues = usesRawValues;
            var minimum = double.PositiveInfinity;
            var maximum = double.NegativeInfinity;
            var excluded = 0;
            foreach (var value in values)
            {
                if (double.IsNaN(value) || double.IsInfinity(value)) { excluded++; continue; }
                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
            }
            cancellationToken.ThrowIfCancellationRequested();
            ExcludedCount = excluded;
            Minimum = FiniteCount == 0 ? double.NaN : minimum;
            Maximum = FiniteCount == 0 ? double.NaN : maximum;
            var numericRange = usesRawValues && (descriptor.PixelFormat == RawPixelFormat.Float32 || descriptor.PixelFormat == RawPixelFormat.Int32);
            LowerBound = numericRange && FiniteCount > 0 ? minimum : 0;
            var nominalMaximum = !usesRawValues ? 255 : descriptor.PixelFormat == RawPixelFormat.Mono16
                ? (1 << (descriptor.ValidBits > 0 ? descriptor.ValidBits : 16)) - 1 : descriptor.PixelFormat == RawPixelFormat.Mono10PackedLsb
                    ? 1023 : descriptor.PixelFormat == RawPixelFormat.Mono12PackedLsb ? 4095 : 255;
            UpperBound = numericRange ? (FiniteCount > 0 ? maximum : 1) : Math.Max(nominalMaximum, FiniteCount > 0 ? maximum : 0);
            var bins = new long[256];
            for (var i = 0; i < values.Length; i++)
            {
                if ((i & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                var value = values[i];
                if (double.IsNaN(value) || double.IsInfinity(value)) continue;
                // Equal-width intervals; the last interval includes the upper endpoint.
                var index = UpperBound <= LowerBound ? 0 : Math.Min(255, (int)((value - LowerBound) / (UpperBound - LowerBound) * 256));
                bins[Math.Max(0, index)]++;
            }
            Bins = Array.AsReadOnly(bins);
        }

        public static bool HasColorChannels(RawPixelFormat format) => format == RawPixelFormat.RGB24 || format == RawPixelFormat.BGR24 || format == RawPixelFormat.BGRA32;

        internal static RawPixelHistogram Calculate(double[] values, RawImageDescriptor descriptor, bool usesRawValues, CancellationToken cancellationToken)
            => new RawPixelHistogram(values, descriptor, usesRawValues, cancellationToken);

        internal static void ValidateChannel(RawPixelFormat format, RawHistogramChannel channel)
        {
            if (channel < RawHistogramChannel.Value || channel > RawHistogramChannel.Blue || (channel != RawHistogramChannel.Value && !HasColorChannels(format)))
                throw new ArgumentOutOfRangeException(nameof(channel), "This image does not have the selected channel.");
        }
    }
}
