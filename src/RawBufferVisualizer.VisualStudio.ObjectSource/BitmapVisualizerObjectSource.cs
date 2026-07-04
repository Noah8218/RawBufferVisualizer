using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.DebuggerVisualizers;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public sealed class BitmapVisualizerObjectSource : VisualizerObjectSource
    {
        private object? _cachedBitmap;
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
            if (!ReferenceEquals(target, _cachedBitmap))
            {
                _cachedBitmap = target;
                _cachedTransfer = BitmapVisualizerTransfer.CreateTransfer(target);
            }

            return _cachedTransfer ?? throw new InvalidOperationException("Bitmap transfer was not created.");
        }
    }

    public static class BitmapVisualizerTransfer
    {
        private const string BitmapFullName = "System.Drawing.Bitmap";

        public static VisualizerSnapshotTransfer CreateTransfer(object? bitmap, string? displayName = null)
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
            var format = ToRawPixelFormat(pixelFormat);
            var bytesPerPixel = GetBytesPerPixel(format);
            var width = Get<int>(bitmap, "Width");
            var height = Get<int>(bitmap, "Height");
            var bitmapData = LockBits(bitmap, width, height, pixelFormat);
            try
            {
                var dataStride = Get<int>(bitmapData, "Stride");
                var stride = Math.Abs(dataStride);
                var scan0 = Get<IntPtr>(bitmapData, "Scan0");
                var buffer = new byte[checked(stride * height)];
                for (var y = 0; y < height; y++)
                {
                    var source = scan0 + (dataStride >= 0 ? y * dataStride : (height - 1 - y) * stride);
                    Marshal.Copy(source, buffer, y * stride, width * bytesPerPixel);
                }

                var snapshot = RawBufferSnapshot.FromByteArray(buffer, new RawImageDescriptor
                {
                    Width = width,
                    Height = height,
                    Stride = stride,
                    PixelFormat = format,
                    ValidBits = bytesPerPixel == 1 ? 8 : bytesPerPixel * 8,
                    ByteOrder = RawByteOrder.LittleEndian
                });

                return RawBufferSnapshotObjectSource.CreateTransfer(
                    snapshot,
                    BitmapFullName,
                    displayName);
            }
            finally
            {
                UnlockBits(bitmap, bitmapData);
            }
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
