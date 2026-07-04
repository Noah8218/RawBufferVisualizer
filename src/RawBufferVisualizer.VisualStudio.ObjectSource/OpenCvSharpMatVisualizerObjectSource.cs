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
    public sealed class OpenCvSharpMatVisualizerObjectSource : VisualizerObjectSource
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
                _cachedTransfer = OpenCvSharpMatVisualizerTransfer.CreateTransfer(target);
            }

            return _cachedTransfer ?? throw new InvalidOperationException("Mat transfer was not created.");
        }
    }

    public static class OpenCvSharpMatVisualizerTransfer
    {
        private const string MatFullName = "OpenCvSharp.Mat";

        public static VisualizerSnapshotTransfer CreateTransfer(object mat, string? displayName = null)
        {
            if (mat == null)
            {
                throw new ArgumentNullException(nameof(mat));
            }

            if (mat.GetType().FullName != MatFullName)
            {
                throw new NotSupportedException("Only OpenCvSharp.Mat is supported.");
            }

            if (Invoke<bool>(mat, "Empty"))
            {
                throw new ArgumentException("Mat is empty.", nameof(mat));
            }

            if (Get<int>(mat, "Dims") != 2)
            {
                throw new NotSupportedException("Only 2D Mat images are supported.");
            }

            var matType = Invoke<object>(mat, "Type");
            var stride = checked((int)Invoke<long>(mat, "Step"));
            var height = Get<int>(mat, "Height");
            var descriptor = new RawImageDescriptor
            {
                Width = Get<int>(mat, "Width"),
                Height = height,
                Stride = stride,
                PixelFormat = ToRawPixelFormat(matType),
                ValidBits = GetValidBits(matType),
                ByteOrder = RawByteOrder.LittleEndian
            };

            var data = Get<IntPtr>(mat, "Data");
            if (data == IntPtr.Zero)
            {
                throw new ArgumentException("Mat data pointer is empty.", nameof(mat));
            }

            var byteCount = checked(stride * height);
            var buffer = new byte[byteCount];
            Marshal.Copy(data, buffer, 0, byteCount);

            return RawBufferSnapshotObjectSource.CreateTransfer(
                RawBufferSnapshot.FromByteArray(buffer, descriptor),
                MatFullName,
                displayName);
        }

        private static RawPixelFormat ToRawPixelFormat(object matType)
        {
            var depth = Get<int>(matType, "Depth");
            var channels = Get<int>(matType, "Channels");

            if (depth == 0 && channels == 1)
            {
                return RawPixelFormat.Mono8;
            }

            if (depth == 0 && channels == 3)
            {
                return RawPixelFormat.BGR24;
            }

            if (depth == 0 && channels == 4)
            {
                return RawPixelFormat.BGRA32;
            }

            if (depth == 2 && channels == 1)
            {
                return RawPixelFormat.Mono16;
            }

            if (depth == 5 && channels == 1)
            {
                return RawPixelFormat.Float32;
            }

            throw new NotSupportedException("Unsupported Mat type: " + Convert.ToString(matType, CultureInfo.InvariantCulture));
        }

        private static int GetValidBits(object matType)
        {
            switch (Get<int>(matType, "Depth"))
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

        private static T Invoke<T>(object instance, string methodName)
        {
            var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (method == null)
            {
                throw new MissingMethodException(instance.GetType().FullName, methodName);
            }

            var value = method.Invoke(instance, null);
            if (value == null)
            {
                throw new InvalidOperationException(methodName + " returned null.");
            }

            return (T)value;
        }
    }
}
