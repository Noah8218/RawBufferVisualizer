using System;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.DebuggerVisualizers;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public sealed class BitmapVisualizerObjectSource : VisualizerObjectSource
    {
        private object? _cachedBitmap;
        private BitmapVisualizerView? _cachedView;

        public override void GetData(object target, Stream outgoingData)
        {
            SerializeAsJson(
                outgoingData,
                VisualizerChunkedTransfer.AttachExpressionIdentity(
                    BitmapVisualizerTransfer.CreateMetadata(GetView(target)),
                    target));
        }

        public override void TransferData(object target, Stream incomingData, Stream outgoingData)
        {
            var request = DeserializeFromJson<VisualizerSnapshotChunkRequest>(incomingData);
            if (request == null)
            {
                throw new InvalidDataException("Chunk request is required.");
            }

            if (request.Operation == VisualizerSnapshotOperation.Preview)
            {
                SerializeAsJson(outgoingData, BitmapVisualizerTransfer.CreatePreview(GetView(target), request));
                return;
            }

            SerializeAsJson(outgoingData, BitmapVisualizerTransfer.CreateChunk(GetView(target), request));
        }

        private BitmapVisualizerView GetView(object target)
        {
            if (!ReferenceEquals(target, _cachedBitmap))
            {
                _cachedBitmap = target;
                _cachedView = BitmapVisualizerTransfer.CreateView(target);
            }

            return _cachedView ?? throw new InvalidOperationException("Bitmap view was not created.");
        }
    }

    public sealed class BitmapVisualizerView
    {
        public object Bitmap { get; set; } = new object();
        public object PixelFormat { get; set; } = new object();
        public RawImageDescriptor Descriptor { get; set; } = new RawImageDescriptor();
        public int BytesPerPixel { get; set; }
        public long BufferLength { get; set; }
        public IntPtr CapturedScan0 { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        internal int SourceStride { get; set; }
        internal int[]? Palette { get; set; }
    }

    public static class BitmapVisualizerTransfer
    {
        private const string BitmapFullName = "System.Drawing.Bitmap";

        public static BitmapVisualizerView CreateView(object? bitmap, string? displayName = null)
        {
            if (bitmap == null)
            {
                throw new ArgumentNullException(nameof(bitmap));
            }

            if (bitmap.GetType().FullName != BitmapFullName)
            {
                throw new NotSupportedException("Only System.Drawing.Bitmap is supported. Actual type: " + bitmap.GetType().FullName);
            }

            var pixelFormat = Get<object>(bitmap, "PixelFormat");
            var palette = pixelFormat.ToString() == "Format8bppIndexed" ? ReadPalette(bitmap) : null;
            if (palette != null && BitmapPixelConverter.IsIdentityGrayPalette(palette)) palette = null;
            var format = palette == null ? ToRawPixelFormat(pixelFormat) : RawPixelFormat.BGRA32;
            var bytesPerPixel = GetBytesPerPixel(format);
            var width = Get<int>(bitmap, "Width");
            var height = Get<int>(bitmap, "Height");
            var bitmapData = LockBits(bitmap, width, height, pixelFormat);
            int stride;
            IntPtr capturedScan0;
            try
            {
                stride = Math.Abs(Get<int>(bitmapData, "Stride"));
                capturedScan0 = Get<IntPtr>(bitmapData, "Scan0");
                if (capturedScan0 == IntPtr.Zero)
                {
                    throw new ArgumentException("Bitmap Scan0 pointer is empty.", nameof(bitmap));
                }
            }
            finally
            {
                UnlockBits(bitmap, bitmapData);
            }

            var descriptor = new RawImageDescriptor
            {
                Width = width,
                Height = height,
                Stride = palette == null ? stride : checked(width * 4),
                PixelFormat = format,
                ValidBits = bytesPerPixel == 1 ? 8 : bytesPerPixel * 8,
                ByteOrder = RawByteOrder.LittleEndian
            };

            return new BitmapVisualizerView
            {
                Bitmap = bitmap,
                PixelFormat = pixelFormat,
                Descriptor = descriptor,
                BytesPerPixel = bytesPerPixel,
                BufferLength = checked((long)descriptor.Stride * height),
                SourceStride = stride,
                Palette = palette,
                CapturedScan0 = capturedScan0,
                SourceType = BitmapFullName,
                DisplayName = displayName ?? string.Empty
            };
        }

        public static VisualizerSnapshotMetadata CreateMetadata(BitmapVisualizerView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            return VisualizerChunkedTransfer.CreateCapturedPointerMetadata(
                view.Descriptor,
                view.BufferLength,
                view.CapturedScan0,
                view.SourceType,
                view.DisplayName,
                view.CapturedScan0,
                "Scan0");
        }

        public static VisualizerSnapshotTransfer CreateTransfer(object? bitmap, string? displayName = null)
        {
            var view = CreateView(bitmap, displayName);
            if (view.BufferLength > int.MaxValue)
            {
                throw new InvalidOperationException("Bitmap is too large to copy into one managed transfer. Use chunked transfer instead.");
            }

            var buffer = new byte[(int)view.BufferLength];
            long offset = 0;
            while (offset < view.BufferLength)
            {
                var chunk = CreateChunk(
                    view,
                    new VisualizerSnapshotChunkRequest
                    {
                        Offset = offset,
                        Count = (int)Math.Min(VisualizerChunkedTransfer.DefaultChunkSize, view.BufferLength - offset)
                    });
                Buffer.BlockCopy(chunk.Buffer, 0, buffer, checked((int)offset), chunk.Buffer.Length);
                offset += chunk.Buffer.Length;
            }

            return new VisualizerSnapshotTransfer
            {
                Descriptor = view.Descriptor.Clone(),
                Buffer = buffer,
                SourceType = view.SourceType,
                DisplayName = view.DisplayName
            };
        }

        public static VisualizerSnapshotTransfer CreatePreview(
            BitmapVisualizerView view,
            VisualizerSnapshotChunkRequest request)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            var bitmapData = LockBits(
                view.Bitmap,
                view.Descriptor.Width,
                view.Descriptor.Height,
                view.PixelFormat);
            try
            {
                var dataStride = Get<int>(bitmapData, "Stride");
                EnsureStrideMatches(view, dataStride);
                var sourceDescriptor = view.Descriptor.Clone();
                sourceDescriptor.Stride = view.SourceStride;
                if (view.Palette != null)
                {
                    sourceDescriptor.PixelFormat = RawPixelFormat.Mono8;
                    sourceDescriptor.ValidBits = 8;
                }
                var preview = VisualizerSampledPreview.CreateFromRows(
                    Get<IntPtr>(bitmapData, "Scan0"),
                    dataStride,
                    checked((long)view.SourceStride * sourceDescriptor.Height),
                    sourceDescriptor,
                    view.SourceType,
                    view.DisplayName,
                    request.MaximumWidth,
                    request.MaximumHeight);
                BitmapPixelConverter.NormalizeBgra32(preview.Buffer, 0, checked(preview.Descriptor.Width * preview.Descriptor.Height), view.Palette, view.PixelFormat.ToString() == "Format32bppPArgb", view.PixelFormat.ToString() == "Format32bppRgb");
                return preview;
            }
            finally
            {
                UnlockBits(view.Bitmap, bitmapData);
            }
        }

        public static VisualizerSnapshotChunk CreateChunk(
            BitmapVisualizerView view,
            VisualizerSnapshotChunkRequest request)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Offset < 0 || request.Offset > view.BufferLength)
            {
                throw new ArgumentOutOfRangeException(nameof(request.Offset));
            }

            if (request.Count <= 0 || request.Count > VisualizerChunkedTransfer.DefaultChunkSize)
            {
                throw new ArgumentOutOfRangeException(nameof(request.Count));
            }

            var remaining = view.BufferLength - request.Offset;
            var length = (int)Math.Min(request.Count, remaining);
            var buffer = new byte[length];
            if (length == 0)
            {
                return new VisualizerSnapshotChunk
                {
                    Offset = request.Offset,
                    Buffer = buffer,
                    TotalLength = view.BufferLength,
                    IsLastChunk = true
                };
            }

            var bitmapData = LockBits(
                view.Bitmap,
                view.Descriptor.Width,
                view.Descriptor.Height,
                view.PixelFormat);
            try
            {
                var dataStride = Get<int>(bitmapData, "Stride");
                EnsureStrideMatches(view, dataStride);
                CopyNormalizedChunk(view, bitmapData, dataStride, request.Offset, buffer);
            }
            finally
            {
                UnlockBits(view.Bitmap, bitmapData);
            }

            return new VisualizerSnapshotChunk
            {
                Offset = request.Offset,
                Buffer = buffer,
                TotalLength = view.BufferLength,
                IsLastChunk = request.Offset + length >= view.BufferLength
            };
        }

        private static void CopyNormalizedChunk(
            BitmapVisualizerView view,
            object bitmapData,
            int dataStride,
            long requestOffset,
            byte[] target)
        {
            var scan0 = Get<IntPtr>(bitmapData, "Scan0");
            var reader = CurrentProcessMemoryReader.CreateForRows(
                scan0,
                dataStride,
                view.Descriptor.Height,
                checked((long)view.SourceStride * view.Descriptor.Height));
            var stride = view.Descriptor.Stride;
            var pixelBytes = checked(view.Descriptor.Width * view.BytesPerPixel);
            var premultiplied = view.PixelFormat.ToString() == "Format32bppPArgb";
            var opaque = view.PixelFormat.ToString() == "Format32bppRgb";
            byte[]? converted = null;
            var copied = 0;
            while (copied < target.Length)
            {
                var logicalOffset = requestOffset + copied;
                var row = checked((int)(logicalOffset / stride));
                var column = checked((int)(logicalOffset % stride));
                var segmentLength = Math.Min(target.Length - copied, stride - column);
                if (column < pixelBytes)
                {
                    var copyLength = Math.Min(segmentLength, pixelBytes - column);
                    var rowOffset = dataStride >= 0
                        ? checked((long)row * dataStride)
                        : checked((long)(view.Descriptor.Height - 1 - row) * view.SourceStride);
                    if (view.Palette != null || premultiplied || opaque)
                    {
                        var firstPixel = column / 4;
                        var pixelCount = checked((column % 4 + copyLength + 3) / 4);
                        var requiredBytes = checked(pixelCount * 4);
                        if (converted == null || converted.Length < requiredBytes) converted = new byte[requiredBytes];
                        var sourcePixelStride = view.Palette == null ? 4 : 1;
                        reader.CopyTo(rowOffset + (long)firstPixel * sourcePixelStride, converted, 0, checked(pixelCount * sourcePixelStride));
                        BitmapPixelConverter.NormalizeBgra32(converted, 0, pixelCount, view.Palette, premultiplied, opaque, sourcePixelStride);
                        Buffer.BlockCopy(converted, column % 4, target, copied, copyLength);
                    }
                    else
                    {
                        reader.CopyTo(rowOffset + column, target, copied, copyLength);
                    }
                }

                copied += segmentLength;
            }
        }

        private static void EnsureStrideMatches(BitmapVisualizerView view, int dataStride)
        {
            if (Math.Abs((long)dataStride) != view.SourceStride)
            {
                throw new InvalidOperationException("Bitmap stride changed while the visualizer was active.");
            }
        }

        private static int[] ReadPalette(object bitmap)
        {
            var entries = (Array)Get<object>(Get<object>(bitmap, "Palette"), "Entries");
            var palette = new int[entries.Length];
            var toArgb = FindMethod(entries.GetType().GetElementType()!, "ToArgb", 0);
            for (var i = 0; i < palette.Length; i++) palette[i] = (int)toArgb.Invoke(entries.GetValue(i), null)!;
            return palette;
        }

        private static object LockBits(object bitmap, int width, int height, object pixelFormat)
        {
            var method = FindMethod(bitmap.GetType(), "LockBits", 3);
            var parameters = method.GetParameters();
            var rectangle = Activator.CreateInstance(parameters[0].ParameterType, 0, 0, width, height);
            var readOnly = Enum.Parse(parameters[1].ParameterType, "ReadOnly");
            var data = method.Invoke(bitmap, new[] { rectangle, readOnly, pixelFormat });
            if (data == null)
            {
                throw new InvalidOperationException("Bitmap.LockBits returned null.");
            }

            return data;
        }

        private static void UnlockBits(object bitmap, object bitmapData)
        {
            var method = FindMethod(bitmap.GetType(), "UnlockBits", 1);
            method.Invoke(bitmap, new[] { bitmapData });
        }

        private static MethodInfo FindMethod(Type type, string name, int parameterCount)
        {
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name == name && method.GetParameters().Length == parameterCount)
                {
                    return method;
                }
            }

            throw new MissingMethodException(type.FullName, name);
        }

        private static T Get<T>(object instance, string propertyName)
        {
            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
            {
                throw new MissingMemberException(instance.GetType().FullName, propertyName);
            }

            var value = property.GetValue(instance);
            if (value == null)
            {
                throw new InvalidOperationException(propertyName + " returned null.");
            }

            return (T)value;
        }

        private static RawPixelFormat ToRawPixelFormat(object pixelFormat)
        {
            switch (pixelFormat.ToString())
            {
                case "Format8bppIndexed":
                    return RawPixelFormat.Mono8;
                case "Format24bppRgb":
                    return RawPixelFormat.BGR24;
                case "Format32bppArgb":
                case "Format32bppPArgb":
                case "Format32bppRgb":
                    return RawPixelFormat.BGRA32;
                default:
                    throw new NotSupportedException("Unsupported Bitmap pixel format: " + pixelFormat);
            }
        }

        private static int GetBytesPerPixel(RawPixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
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
