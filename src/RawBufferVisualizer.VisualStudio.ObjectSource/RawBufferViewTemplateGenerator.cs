using System;
using System.Globalization;
using System.Text;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public static class RawBufferViewTemplateGenerator
    {
        public static string Create(
            TypeMapping mapping,
            RawPixelFormat pixelFormat,
            int bitDepth,
            bool dataIsUnsignedPointer = false,
            string variableName = "frame")
        {
            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            var members = mapping.Members ?? new TypeMappingMembers();
            if (string.IsNullOrWhiteSpace(members.Data)
                || string.IsNullOrWhiteSpace(members.Width)
                || string.IsNullOrWhiteSpace(members.Height))
            {
                throw new ArgumentException("Data, Width, and Height mappings are required.", nameof(mapping));
            }

            if (string.IsNullOrWhiteSpace(variableName))
            {
                throw new ArgumentException("A source variable name is required.", nameof(variableName));
            }

            var data = Member(variableName, members.Data!);
            var width = Member(variableName, members.Width!);
            var height = Member(variableName, members.Height!);
            var stride = string.IsNullOrWhiteSpace(members.Stride)
                ? CreateMinimumStrideExpression(width, pixelFormat)
                : Member(variableName, members.Stride!);
            var bufferLength = string.IsNullOrWhiteSpace(members.BufferLength)
                ? "checked((long)rawBufferStride * " + height + ")"
                : Member(variableName, members.BufferLength!);
            var bitDepthExpression = !string.IsNullOrWhiteSpace(members.ValidBits)
                ? Member(variableName, members.ValidBits!)
                : (!string.IsNullOrWhiteSpace(members.BitDepth)
                    ? Member(variableName, members.BitDepth!)
                    : Math.Max(1, bitDepth).ToString(CultureInfo.InvariantCulture));
            var byteOrder = Enum.TryParse(mapping.ByteOrder, true, out RawByteOrder parsedByteOrder)
                ? parsedByteOrder
                : RawByteOrder.LittleEndian;
            var bufferExpression = dataIsUnsignedPointer
                ? "new IntPtr(unchecked((long)" + data + ".ToUInt64()))"
                : data;

            var builder = new StringBuilder();
            builder.AppendLine("using RawBufferVisualizer.Core;");
            builder.AppendLine("using RawBufferVisualizer.Sdk;");
            builder.AppendLine();
            builder.AppendLine("// The source buffer must remain valid while the debugger reads this view.");
            builder.AppendLine("// PixelFormat and derived metadata reflect the current mapping.");
            builder.Append("var rawBufferStride = ").Append(stride).AppendLine(";");
            builder.AppendLine("var view = new RawBufferView");
            builder.AppendLine("{");
            builder.Append("    Buffer = ").Append(bufferExpression).AppendLine(",");
            builder.Append("    BufferLength = ").Append(bufferLength).AppendLine(",");
            builder.Append("    Width = ").Append(width).AppendLine(",");
            builder.Append("    Height = ").Append(height).AppendLine(",");
            builder.AppendLine("    Stride = rawBufferStride,");
            builder.Append("    PixelFormat = RawPixelFormat.").Append(pixelFormat).AppendLine(",");
            builder.Append("    Channels = ").Append(GetChannelCount(pixelFormat).ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("    BitDepth = ").Append(bitDepthExpression).AppendLine(",");
            builder.Append("    ByteOrder = RawByteOrder.").Append(byteOrder).AppendLine(",");
            builder.Append("    Name = nameof(").Append(variableName).AppendLine(")");
            builder.AppendLine("};");
            return builder.ToString();
        }

        private static string Member(string variableName, string memberPath)
        {
            return variableName + "." + memberPath;
        }

        private static string CreateMinimumStrideExpression(string width, RawPixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case RawPixelFormat.Mono16:
                    return "checked(" + width + " * 2)";
                case RawPixelFormat.Mono10PackedLsb:
                    return "checked((" + width + " * 10 + 7) / 8)";
                case RawPixelFormat.Mono12PackedLsb:
                    return "checked((" + width + " * 12 + 7) / 8)";
                case RawPixelFormat.RGB24:
                case RawPixelFormat.BGR24:
                    return "checked(" + width + " * 3)";
                case RawPixelFormat.BGRA32:
                case RawPixelFormat.Float32:
                case RawPixelFormat.Int32:
                    return "checked(" + width + " * 4)";
                default:
                    return width;
            }
        }

        private static int GetChannelCount(RawPixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case RawPixelFormat.RGB24:
                case RawPixelFormat.BGR24:
                    return 3;
                case RawPixelFormat.BGRA32:
                    return 4;
                default:
                    return 1;
            }
        }
    }
}
