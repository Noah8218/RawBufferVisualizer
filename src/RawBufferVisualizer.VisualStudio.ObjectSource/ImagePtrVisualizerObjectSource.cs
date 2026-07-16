using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.DebuggerVisualizers;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public sealed class ImagePtrVisualizerObjectSource : VisualizerObjectSource
    {
        private object? _cachedTarget;
        private ImagePtrView? _cachedView;

        public override void GetData(object target, Stream outgoingData)
        {
            SerializeAsJson(outgoingData, ImagePtrVisualizerTransfer.CreateMetadata(GetView(target)));
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
                SerializeAsJson(outgoingData, ImagePtrVisualizerTransfer.CreatePreview(GetView(target), request));
                return;
            }

            SerializeAsJson(outgoingData, ImagePtrVisualizerTransfer.CreateChunk(GetView(target), request));
        }

        private ImagePtrView GetView(object target)
        {
            if (!ReferenceEquals(target, _cachedTarget))
            {
                _cachedTarget = target;
                _cachedView = ImagePtrVisualizerTransfer.CreateView(target);
            }

            return _cachedView ?? throw new InvalidOperationException("ImagePtr view was not created.");
        }
    }

    public sealed class ImagePtrView
    {
        public IntPtr Buffer { get; set; }
        public long BufferLength { get; set; }
        public RawImageDescriptor Descriptor { get; set; } = new RawImageDescriptor();
        public string SourceType { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public static class ImagePtrVisualizerTransfer
    {
        public static ImagePtrView CreateView(object target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var type = target.GetType();
            var ptr = GetValue<IntPtr>(target, "Ptr", "Buffer", "Data", "DataPointer", "_ptr", "_buffer", "_data");
            if (ptr == IntPtr.Zero)
            {
                throw new ArgumentException("Image pointer is empty.", nameof(target));
            }

            var width = GetValue<int>(target, "Width", "_width");
            var height = GetValue<int>(target, "Height", "_height");
            var channels = GetOptionalInt(target, "Channels", "ChannelCount", "_channels");
            var bitDepth = GetOptionalInt(target, "BitDepth", "Depth", "ValidBits", "_bitDepth", "_depth");
            var bpp = NormalizeBpp(
                GetOptionalInt(target, "Bpp", "BytesPerPixel", "BytesPerElement", "BitsPerPixel", "_bpp"),
                channels,
                bitDepth);
            var pixelFormat = TryGetPixelFormat(target, out var explicitFormat)
                ? explicitFormat
                : InferPixelFormat(bpp, channels, bitDepth);

            var bytesPerPixel = GetBytesPerPixel(pixelFormat, bpp, channels, bitDepth);
            var stride = GetOptionalInt(target, "Step", "Stride", "Pitch", "_step", "_stride", "_pitch");
            if (stride <= 0)
            {
                stride = checked(width * bytesPerPixel);
            }

            var descriptor = new RawImageDescriptor
            {
                Width = width,
                Height = height,
                Stride = stride,
                PixelFormat = pixelFormat,
                ValidBits = bitDepth > 0 ? bitDepth : GetDefaultValidBits(pixelFormat),
                ByteOrder = RawByteOrder.LittleEndian
            };

            var length = GetOptionalLong(target, "Length", "BufferLength", "Size", "ByteLength", "_length", "_size");
            if (length <= 0)
            {
                length = checked((long)stride * height);
            }

            return new ImagePtrView
            {
                Buffer = ptr,
                BufferLength = length,
                Descriptor = descriptor,
                SourceType = type.FullName ?? type.Name,
                DisplayName = type.Name
            };
        }

        public static VisualizerSnapshotMetadata CreateMetadata(ImagePtrView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            return VisualizerChunkedTransfer.CreatePointerMetadata(
                view.Descriptor,
                view.BufferLength,
                view.Buffer,
                view.SourceType,
                view.DisplayName);
        }

        public static VisualizerSnapshotChunk CreateChunk(ImagePtrView view, VisualizerSnapshotChunkRequest request)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (view.Buffer == IntPtr.Zero)
            {
                throw new InvalidOperationException("Image pointer is empty.");
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
            if (length > 0)
            {
                Marshal.Copy(Add(view.Buffer, request.Offset), buffer, 0, length);
            }

            return new VisualizerSnapshotChunk
            {
                Offset = request.Offset,
                Buffer = buffer,
                TotalLength = view.BufferLength,
                IsLastChunk = request.Offset + length >= view.BufferLength
            };
        }

        public static VisualizerSnapshotTransfer CreatePreview(
            ImagePtrView view,
            VisualizerSnapshotChunkRequest request)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            return VisualizerSampledPreview.Create(
                view.Buffer,
                view.BufferLength,
                view.Descriptor,
                view.SourceType,
                view.DisplayName,
                request.MaximumWidth,
                request.MaximumHeight);
        }

        private static RawPixelFormat InferPixelFormat(int bpp, int channels, int bitDepth)
        {
            if (channels == 1 && (bitDepth == 0 || bitDepth == 8))
            {
                return RawPixelFormat.Mono8;
            }

            if (channels == 1 && bitDepth == 16)
            {
                return RawPixelFormat.Mono16;
            }

            if (channels == 1 && bitDepth == 32)
            {
                return RawPixelFormat.Float32;
            }

            if (channels == 3 && (bitDepth == 0 || bitDepth == 8))
            {
                return RawPixelFormat.BGR24;
            }

            if (channels == 4 && (bitDepth == 0 || bitDepth == 8))
            {
                return RawPixelFormat.BGRA32;
            }

            switch (bpp)
            {
                case 1:
                    return RawPixelFormat.Mono8;
                case 2:
                    return RawPixelFormat.Mono16;
                case 3:
                    return RawPixelFormat.BGR24;
                case 4:
                    return RawPixelFormat.BGRA32;
                default:
                    throw new NotSupportedException("Unsupported ImagePtr Bpp value: " + bpp.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static int NormalizeBpp(int bpp, int channels, int bitDepth)
        {
            if (bpp <= 0)
            {
                if (channels > 0 && bitDepth > 0)
                {
                    return checked(channels * ((bitDepth + 7) / 8));
                }

                return 0;
            }

            if (bpp <= 4)
            {
                return bpp;
            }

            if (bpp == 8 && channels > 1)
            {
                return channels;
            }

            if (bpp % 8 == 0 && bpp <= 64)
            {
                return bpp / 8;
            }

            return bpp;
        }

        private static int GetBytesPerPixel(RawPixelFormat pixelFormat, int bpp, int channels, int bitDepth)
        {
            if (bpp > 0)
            {
                return bpp;
            }

            if (channels > 0 && bitDepth > 0)
            {
                return checked(channels * ((bitDepth + 7) / 8));
            }

            return pixelFormat == RawPixelFormat.Mono16 ? 2 :
                pixelFormat == RawPixelFormat.BGR24 || pixelFormat == RawPixelFormat.RGB24 ? 3 :
                pixelFormat == RawPixelFormat.BGRA32 || pixelFormat == RawPixelFormat.Float32 ? 4 :
                1;
        }

        private static int GetDefaultValidBits(RawPixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case RawPixelFormat.Mono16:
                    return 16;
                case RawPixelFormat.Float32:
                    return 32;
                case RawPixelFormat.Binary:
                    return 1;
                case RawPixelFormat.Mono10PackedLsb:
                    return 10;
                case RawPixelFormat.Mono12PackedLsb:
                    return 12;
                default:
                    return 8;
            }
        }

        private static bool TryGetPixelFormat(object target, out RawPixelFormat pixelFormat)
        {
            foreach (var name in new[] { "PixelFormat", "Format", "RawPixelFormat" })
            {
                var member = FindMember(target.GetType(), name);
                if (member == null)
                {
                    continue;
                }

                var value = GetMemberValue(target, member);
                if (value == null)
                {
                    continue;
                }

                if (value is RawPixelFormat rawPixelFormat)
                {
                    pixelFormat = rawPixelFormat;
                    return true;
                }

                if (Enum.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), true, out pixelFormat))
                {
                    return true;
                }
            }

            pixelFormat = RawPixelFormat.Mono8;
            return false;
        }

        private static T GetValue<T>(object target, params string[] names)
        {
            foreach (var name in names)
            {
                var member = FindMember(target.GetType(), name);
                if (member == null)
                {
                    continue;
                }

                var value = GetMemberValue(target, member);
                if (value == null)
                {
                    continue;
                }

                return ConvertValue<T>(value);
            }

            throw new MissingMemberException(target.GetType().FullName, string.Join("/", names));
        }

        private static int GetOptionalInt(object target, params string[] names)
        {
            return TryGetOptionalValue(target, out int value, names) ? value : 0;
        }

        private static long GetOptionalLong(object target, params string[] names)
        {
            return TryGetOptionalValue(target, out long value, names) ? value : 0;
        }

        private static bool TryGetOptionalValue<T>(object target, out T result, params string[] names)
        {
            foreach (var name in names)
            {
                var member = FindMember(target.GetType(), name);
                if (member == null)
                {
                    continue;
                }

                var value = GetMemberValue(target, member);
                if (value == null)
                {
                    continue;
                }

                result = ConvertValue<T>(value);
                return true;
            }

            result = default!;
            return false;
        }

        private static MemberInfo? FindMember(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var property = type.GetProperty(name, flags);
            if (property != null)
            {
                return property;
            }

            return type.GetField(name, flags);
        }

        private static object? GetMemberValue(object target, MemberInfo member)
        {
            var property = member as PropertyInfo;
            if (property != null)
            {
                return property.GetValue(target);
            }

            var field = member as FieldInfo;
            if (field != null)
            {
                return field.GetValue(target);
            }

            return null;
        }

        private static T ConvertValue<T>(object value)
        {
            if (value is T typed)
            {
                return typed;
            }

            if (typeof(T) == typeof(IntPtr))
            {
                if (value is UIntPtr uintPtr)
                {
                    return (T)(object)new IntPtr(checked((long)uintPtr.ToUInt64()));
                }

                return (T)(object)new IntPtr(Convert.ToInt64(value, CultureInfo.InvariantCulture));
            }

            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }

        private static IntPtr Add(IntPtr pointer, long offset)
        {
            return new IntPtr(checked(pointer.ToInt64() + offset));
        }
    }
}
