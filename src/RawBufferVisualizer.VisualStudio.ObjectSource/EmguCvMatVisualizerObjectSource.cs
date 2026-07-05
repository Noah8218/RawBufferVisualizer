using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.DebuggerVisualizers;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public sealed class EmguCvMatVisualizerObjectSource : VisualizerObjectSource
    {
        private object? _cachedMat;
        private VisualizerSnapshotTransfer? _cachedTransfer;

        public override void GetData(object target, Stream outgoingData)
        {
            SerializeAsJson(outgoingData, VisualizerChunkedTransfer.CreateMetadata(GetTransfer(target)));
        }

        public override void TransferData(object target, Stream incomingData, Stream outgoingData)
        {
            var request = DeserializeFromJson<VisualizerSnapshotChunkRequest>(incomingData);
            if (request == null)
            {
                throw new InvalidDataException("Chunk request is required.");
            }

            SerializeAsJson(outgoingData, VisualizerChunkedTransfer.CreateChunk(GetTransfer(target), request));
        }

        private VisualizerSnapshotTransfer GetTransfer(object target)
        {
            if (!ReferenceEquals(target, _cachedMat))
            {
                _cachedMat = target;
                _cachedTransfer = EmguCvMatVisualizerTransfer.CreateTransfer(target);
            }

            return _cachedTransfer ?? throw new InvalidOperationException("Emgu Mat transfer was not created.");
        }
    }

    public static class EmguCvMatVisualizerTransfer
    {
        private const string MatFullName = "Emgu.CV.Mat";

        public static VisualizerSnapshotTransfer CreateTransfer(object mat, string? displayName = null)
        {
            if (mat == null)
            {
                throw new ArgumentNullException(nameof(mat));
            }

            if (mat.GetType().FullName != MatFullName)
            {
                throw new NotSupportedException("Only Emgu.CV.Mat is supported.");
            }

            if (Get<bool>(mat, "IsEmpty"))
            {
                throw new ArgumentException("Emgu Mat is empty.", nameof(mat));
            }

            if (Get<int>(mat, "Dims") != 2)
            {
                throw new NotSupportedException("Only 2D Emgu Mat images are supported.");
            }

            var depth = Get<object>(mat, "Depth");
            var channels = Get<int>(mat, "NumberOfChannels");
            var stride = Get<int>(mat, "Step");
            var height = Get<int>(mat, "Rows");
            var descriptor = new RawImageDescriptor
            {
                Width = Get<int>(mat, "Cols"),
                Height = height,
                Stride = stride,
                PixelFormat = ToRawPixelFormat(depth, channels),
                ValidBits = GetValidBits(depth),
                ByteOrder = RawByteOrder.LittleEndian
            };

            var data = Get<IntPtr>(mat, "DataPointer");
            if (data == IntPtr.Zero)
            {
                throw new ArgumentException("Emgu Mat data pointer is empty.", nameof(mat));
            }

            var byteCount = checked(stride * height);
            var buffer = new byte[byteCount];
            Marshal.Copy(data, buffer, 0, byteCount);

            return RawBufferSnapshotObjectSource.CreateTransfer(
                RawBufferSnapshot.FromByteArray(buffer, descriptor),
                MatFullName,
                displayName);
        }

        private static RawPixelFormat ToRawPixelFormat(object depth, int channels)
        {
            var depthValue = ToDepthValue(depth);
            if (depthValue == 0 && channels == 1)
            {
                return RawPixelFormat.Mono8;
            }

            if (depthValue == 0 && channels == 3)
            {
                return RawPixelFormat.BGR24;
            }

            if (depthValue == 0 && channels == 4)
            {
                return RawPixelFormat.BGRA32;
            }

            if (depthValue == 2 && channels == 1)
            {
                return RawPixelFormat.Mono16;
            }

            if (depthValue == 5 && channels == 1)
            {
                return RawPixelFormat.Float32;
            }

            throw new NotSupportedException(
                "Unsupported Emgu Mat depth/channels: " + Convert.ToString(depth, CultureInfo.InvariantCulture) + " C" + channels);
        }

        private static int GetValidBits(object depth)
        {
            switch (ToDepthValue(depth))
            {
                case 0:
                    return 8;
                case 2:
                    return 16;
                case 5:
                    return 32;
                default:
                    return 0;
            }
        }

        private static int ToDepthValue(object depth)
        {
            if (depth == null)
            {
                throw new ArgumentNullException(nameof(depth));
            }

            if (depth is IConvertible)
            {
                return Convert.ToInt32(depth, CultureInfo.InvariantCulture);
            }

            var name = Convert.ToString(depth, CultureInfo.InvariantCulture);
            switch (name)
            {
                case "Cv8U":
                    return 0;
                case "Cv16U":
                    return 2;
                case "Cv32F":
                    return 5;
                default:
                    throw new NotSupportedException("Unsupported Emgu depth: " + name);
            }
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
    }
}
