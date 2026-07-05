using System.Globalization;

namespace RawBufferVisualizer.Core
{
    public static class RawPixelInspector
    {
        public static string Describe(byte[] buffer, RawImageDescriptor descriptor, int x, int y)
        {
            return Describe(buffer, descriptor, x, y, x, y);
        }

        public static string Describe(byte[] buffer, RawImageDescriptor descriptor, int x, int y, int displayX, int displayY)
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
                    return string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, GV={2}, Value={2}, Raw={3}", displayX, displayY, buffer[row + x], DescribeRawBytes(buffer, descriptor, x, y));
                case RawPixelFormat.Mono16:
                    return string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, GV={2}, Value={2}, Raw={3}", displayX, displayY, RawBufferRenderer.ReadUInt16(buffer, row + (x * 2), descriptor.ByteOrder), DescribeRawBytes(buffer, descriptor, x, y));
                case RawPixelFormat.Mono10PackedLsb:
                    return string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, GV={2}, Value={2}, Raw={3}", displayX, displayY, RawBufferRenderer.ReadPackedLsb(buffer, row, x, 10), DescribeRawBytes(buffer, descriptor, x, y));
                case RawPixelFormat.Mono12PackedLsb:
                    return string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, GV={2}, Value={2}, Raw={3}", displayX, displayY, RawBufferRenderer.ReadPackedLsb(buffer, row, x, 12), DescribeRawBytes(buffer, descriptor, x, y));
                case RawPixelFormat.Float32:
                    return string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, Value={2:0.###}, Raw={3}", displayX, displayY, RawBufferRenderer.ReadSingle(buffer, row + (x * 4), descriptor.ByteOrder), DescribeRawBytes(buffer, descriptor, x, y));
                case RawPixelFormat.RGB24:
                    return DescribeRgb(buffer, row + (x * 3), displayX, displayY, true);
                case RawPixelFormat.BGR24:
                    return DescribeRgb(buffer, row + (x * 3), displayX, displayY, false);
                case RawPixelFormat.BGRA32:
                    return string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, B={2}, G={3}, R={4}, A={5}, Raw={6}", displayX, displayY, buffer[row + (x * 4)], buffer[row + (x * 4) + 1], buffer[row + (x * 4) + 2], buffer[row + (x * 4) + 3], DescribeRawBytes(buffer, descriptor, x, y));
                default:
                    return DescribeBayer(buffer, descriptor, x, y, displayX, displayY);
            }
        }

        public static string DescribeValue(byte[] buffer, RawImageDescriptor descriptor, int x, int y)
        {
            if (buffer == null || descriptor == null || x < 0 || y < 0 || x >= descriptor.Width || y >= descriptor.Height)
            {
                return "-";
            }

            var row = y * descriptor.Stride;
            switch (descriptor.PixelFormat)
            {
                case RawPixelFormat.Mono8:
                case RawPixelFormat.Binary:
                    return buffer[row + x].ToString(CultureInfo.InvariantCulture);
                case RawPixelFormat.Mono16:
                    return RawBufferRenderer.ReadUInt16(buffer, row + (x * 2), descriptor.ByteOrder).ToString(CultureInfo.InvariantCulture);
                case RawPixelFormat.Mono10PackedLsb:
                    return RawBufferRenderer.ReadPackedLsb(buffer, row, x, 10).ToString(CultureInfo.InvariantCulture);
                case RawPixelFormat.Mono12PackedLsb:
                    return RawBufferRenderer.ReadPackedLsb(buffer, row, x, 12).ToString(CultureInfo.InvariantCulture);
                case RawPixelFormat.Float32:
                    return RawBufferRenderer.ReadSingle(buffer, row + (x * 4), descriptor.ByteOrder).ToString("0.###", CultureInfo.InvariantCulture);
                case RawPixelFormat.RGB24:
                    return FormatRgb(buffer, row + (x * 3), true);
                case RawPixelFormat.BGR24:
                    return FormatRgb(buffer, row + (x * 3), false);
                case RawPixelFormat.BGRA32:
                    var bgra = row + (x * 4);
                    return string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}", buffer[bgra + 2], buffer[bgra + 1], buffer[bgra], buffer[bgra + 3]);
                default:
                    var color = RawBufferRenderer.GetBayerColor(descriptor.PixelFormat, x, y);
                    var colorName = color == 0 ? "R" : color == 1 ? "G" : "B";
                    return colorName + "=" + buffer[(y * descriptor.Stride) + x].ToString(CultureInfo.InvariantCulture);
            }
        }

        public static string DescribeRawBytes(byte[] buffer, RawImageDescriptor descriptor, int x, int y)
        {
            if (buffer == null || descriptor == null || x < 0 || y < 0 || x >= descriptor.Width || y >= descriptor.Height)
            {
                return "-";
            }

            var row = y * descriptor.Stride;
            var start = row + (x * descriptor.GetBytesPerPixel());
            var count = descriptor.GetBytesPerPixel();
            if (descriptor.PixelFormat == RawPixelFormat.Mono10PackedLsb || descriptor.PixelFormat == RawPixelFormat.Mono12PackedLsb)
            {
                var bitsPerPixel = descriptor.PixelFormat == RawPixelFormat.Mono10PackedLsb ? 10 : 12;
                var startBit = x * bitsPerPixel;
                var endBit = startBit + bitsPerPixel;
                start = row + (startBit / 8);
                count = ((endBit + 7) / 8) - (startBit / 8);
            }

            if (start < 0 || start >= buffer.Length)
            {
                return "-";
            }

            count = System.Math.Min(count, buffer.Length - start);
            return FormatBytes(buffer, start, count);
        }

        private static string DescribeRgb(byte[] buffer, int offset, int x, int y, bool sourceIsRgb)
        {
            var c0 = buffer[offset];
            var c1 = buffer[offset + 1];
            var c2 = buffer[offset + 2];
            var gv = (c0 + c1 + c2) / 3;
            return sourceIsRgb
                ? string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, R={2}, G={3}, B={4}, GV={5}, Raw={6:X2} {7:X2} {8:X2}", x, y, c0, c1, c2, gv, c0, c1, c2)
                : string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, B={2}, G={3}, R={4}, GV={5}, Raw={6:X2} {7:X2} {8:X2}", x, y, c0, c1, c2, gv, c0, c1, c2);
        }

        private static string FormatRgb(byte[] buffer, int offset, bool sourceIsRgb)
        {
            return sourceIsRgb
                ? string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}", buffer[offset], buffer[offset + 1], buffer[offset + 2])
                : string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}", buffer[offset + 2], buffer[offset + 1], buffer[offset]);
        }

        private static string DescribeBayer(byte[] buffer, RawImageDescriptor descriptor, int x, int y, int displayX, int displayY)
        {
            var color = RawBufferRenderer.GetBayerColor(descriptor.PixelFormat, x, y);
            var colorName = color == 0 ? "R" : color == 1 ? "G" : "B";
            return string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, Bayer {2}={3}, Raw={4}", displayX, displayY, colorName, buffer[(y * descriptor.Stride) + x], DescribeRawBytes(buffer, descriptor, x, y));
        }

        private static string FormatBytes(byte[] buffer, int offset, int count)
        {
            if (count <= 0)
            {
                return "-";
            }

            var parts = new string[count];
            for (var i = 0; i < count; i++)
            {
                parts[i] = buffer[offset + i].ToString("X2", CultureInfo.InvariantCulture);
            }

            return string.Join(" ", parts);
        }
    }
}
