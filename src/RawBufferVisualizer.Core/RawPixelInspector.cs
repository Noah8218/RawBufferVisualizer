using System.Globalization;

namespace RawBufferVisualizer.Core
{
    public static class RawPixelInspector
    {
        public static string Describe(byte[] buffer, RawImageDescriptor descriptor, int x, int y)
        {
            if (buffer == null)
            {
                return "No buffer";
            }

            if (descriptor == null)
            {
                return "No descriptor";
            }

            if (x < 0 || y < 0 || x >= descriptor.Width || y >= descriptor.Height)
            {
                return "Outside image";
            }

            var row = y * descriptor.Stride;
            switch (descriptor.PixelFormat)
            {
                case RawPixelFormat.Mono8:
                case RawPixelFormat.Binary:
                    return string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, Value={2}", x, y, buffer[row + x]);
                case RawPixelFormat.Mono16:
                    return string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, Value={2}", x, y, RawBufferRenderer.ReadUInt16(buffer, row + (x * 2), descriptor.ByteOrder));
                case RawPixelFormat.Float32:
                    return string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, Value={2:0.###}", x, y, RawBufferRenderer.ReadSingle(buffer, row + (x * 4), descriptor.ByteOrder));
                case RawPixelFormat.RGB24:
                    return DescribeRgb(buffer, row + (x * 3), x, y, true);
                case RawPixelFormat.BGR24:
                    return DescribeRgb(buffer, row + (x * 3), x, y, false);
                case RawPixelFormat.BGRA32:
                    return string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, B={2}, G={3}, R={4}, A={5}", x, y, buffer[row + (x * 4)], buffer[row + (x * 4) + 1], buffer[row + (x * 4) + 2], buffer[row + (x * 4) + 3]);
                default:
                    return DescribeBayer(buffer, descriptor, x, y);
            }
        }

        private static string DescribeRgb(byte[] buffer, int offset, int x, int y, bool sourceIsRgb)
        {
            var c0 = buffer[offset];
            var c1 = buffer[offset + 1];
            var c2 = buffer[offset + 2];
            return sourceIsRgb
                ? string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, R={2}, G={3}, B={4}", x, y, c0, c1, c2)
                : string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, B={2}, G={3}, R={4}", x, y, c0, c1, c2);
        }

        private static string DescribeBayer(byte[] buffer, RawImageDescriptor descriptor, int x, int y)
        {
            var color = RawBufferRenderer.GetBayerColor(descriptor.PixelFormat, x, y);
            var colorName = color == 0 ? "R" : color == 1 ? "G" : "B";
            return string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, Bayer {2}={3}", x, y, colorName, buffer[(y * descriptor.Stride) + x]);
        }
    }
}

