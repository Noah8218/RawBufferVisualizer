using System;

namespace RawBufferVisualizer.Core
{
    public sealed class RawPixelStatistics
    {
        public long Count { get; private set; }
        public double Minimum { get; private set; }
        public double Maximum { get; private set; }
        public double Mean { get; private set; }
        public double StandardDeviation { get; private set; }

        private RawPixelStatistics() { }

        internal static RawPixelStatistics Calculate(byte[] buffer, RawImageDescriptor descriptor, int x, int y, int width, int height)
        {
            var result = new RawPixelStatistics { Minimum = double.PositiveInfinity, Maximum = double.NegativeInfinity };
            var sumSquaredDeviations = 0.0;
            for (var row = y; row < y + height; row++)
            {
                for (var column = x; column < x + width; column++)
                {
                    var value = RawPixelInspector.ReadNumericValue(buffer, descriptor, column, row);
                    if (double.IsNaN(value) || double.IsInfinity(value)) continue;
                    result.Count++;
                    result.Minimum = Math.Min(result.Minimum, value);
                    result.Maximum = Math.Max(result.Maximum, value);
                    // Welford's update avoids subtracting two large, nearly equal squares.
                    var delta = value - result.Mean;
                    result.Mean += delta / result.Count;
                    sumSquaredDeviations += delta * (value - result.Mean);
                }
            }

            if (result.Count == 0)
            {
                result.Minimum = result.Maximum = result.Mean = result.StandardDeviation = double.NaN;
            }
            else
            {
                result.StandardDeviation = Math.Sqrt(Math.Max(0, sumSquaredDeviations / result.Count));
            }
            return result;
        }
    }
}
