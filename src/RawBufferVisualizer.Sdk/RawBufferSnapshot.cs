using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.Sdk
{
    public sealed class RawBufferSnapshot
    {
        public byte[] Buffer { get; private set; }
        public RawImageDescriptor Descriptor { get; private set; }
        public string? MetadataPath { get; private set; }
        public string? RawPath { get; private set; }

        public RawBufferSnapshot(byte[] buffer, RawImageDescriptor descriptor)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        }

        public static RawBufferSnapshot FromByteArray(byte[] buffer, RawImageDescriptor descriptor)
        {
            return new RawBufferSnapshot((byte[])buffer.Clone(), descriptor.Clone());
        }

        public static RawBufferSnapshot FromIntPtr(IntPtr pointer, int byteCount, RawImageDescriptor descriptor)
        {
            if (pointer == IntPtr.Zero)
            {
                throw new ArgumentException("Pointer must not be zero.", nameof(pointer));
            }

            if (byteCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(byteCount));
            }

            var buffer = new byte[byteCount];
            Marshal.Copy(pointer, buffer, 0, byteCount);
            return new RawBufferSnapshot(buffer, descriptor.Clone());
        }

        public static RawBufferSnapshot FromUInt16Array(ushort[] values, RawImageDescriptor descriptor)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var buffer = new byte[values.Length * 2];
            for (var i = 0; i < values.Length; i++)
            {
                WriteUInt16(buffer, i * 2, values[i], descriptor.ByteOrder);
            }

            return new RawBufferSnapshot(buffer, descriptor.Clone());
        }

        public static RawBufferSnapshot FromFloatArray(float[] values, RawImageDescriptor descriptor)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var buffer = new byte[values.Length * 4];
            for (var i = 0; i < values.Length; i++)
            {
                var bytes = BitConverter.GetBytes(values[i]);
                if ((descriptor.ByteOrder == RawByteOrder.BigEndian) == BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                System.Buffer.BlockCopy(bytes, 0, buffer, i * 4, 4);
            }

            return new RawBufferSnapshot(buffer, descriptor.Clone());
        }

        public static RawBufferSnapshot Save(string metadataPath, byte[] buffer, RawImageDescriptor descriptor)
        {
            var snapshot = FromByteArray(buffer, descriptor);
            snapshot.Save(metadataPath);
            return snapshot;
        }

        public void Save(string metadataPath)
        {
            if (string.IsNullOrWhiteSpace(metadataPath))
            {
                throw new ArgumentException("Metadata path is required.", nameof(metadataPath));
            }

            var diagnostics = RawBufferDiagnostics.Analyze(Buffer, Descriptor);
            if (RawBufferDiagnostics.HasErrors(diagnostics))
            {
                throw new InvalidOperationException("Snapshot descriptor is invalid.");
            }

            var fullMetadataPath = Path.GetFullPath(metadataPath);
            var rawPath = GetDefaultRawPath(fullMetadataPath);
            var directory = Path.GetDirectoryName(fullMetadataPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(rawPath, Buffer);

            var dto = RawBufferSnapshotDto.From(Descriptor);
            dto.RawFile = Path.GetFileName(rawPath);
            using (var stream = File.Create(fullMetadataPath))
            {
                var serializer = new DataContractJsonSerializer(typeof(RawBufferSnapshotDto));
                serializer.WriteObject(stream, dto);
            }

            MetadataPath = fullMetadataPath;
            RawPath = rawPath;
        }

        public static RawBufferSnapshot Load(string metadataPath)
        {
            if (string.IsNullOrWhiteSpace(metadataPath))
            {
                throw new ArgumentException("Metadata path is required.", nameof(metadataPath));
            }

            var fullMetadataPath = Path.GetFullPath(metadataPath);
            RawBufferSnapshotDto dto;
            using (var stream = File.OpenRead(fullMetadataPath))
            {
                var serializer = new DataContractJsonSerializer(typeof(RawBufferSnapshotDto));
                var loaded = serializer.ReadObject(stream);
                if (loaded == null)
                {
                    throw new InvalidDataException("Snapshot metadata is empty.");
                }

                dto = (RawBufferSnapshotDto)loaded;
            }

            var rawPath = dto.GetRawPath(fullMetadataPath);
            var snapshot = new RawBufferSnapshot(File.ReadAllBytes(rawPath), dto.ToDescriptor())
            {
                MetadataPath = fullMetadataPath,
                RawPath = rawPath
            };
            return snapshot;
        }

        private static string GetDefaultRawPath(string metadataPath)
        {
            const string suffix = ".rbuf.json";
            if (metadataPath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return metadataPath.Substring(0, metadataPath.Length - suffix.Length) + ".raw";
            }

            return Path.ChangeExtension(metadataPath, ".raw");
        }

        private static void WriteUInt16(byte[] buffer, int offset, ushort value, RawByteOrder byteOrder)
        {
            if (byteOrder == RawByteOrder.BigEndian)
            {
                buffer[offset] = (byte)(value >> 8);
                buffer[offset + 1] = (byte)(value & 0xFF);
            }
            else
            {
                buffer[offset] = (byte)(value & 0xFF);
                buffer[offset + 1] = (byte)(value >> 8);
            }
        }
    }

    [DataContract]
    internal sealed class RawBufferSnapshotDto
    {
        [DataMember(Name = "rawFile", Order = 0)]
        public string RawFile { get; set; } = string.Empty;

        [DataMember(Name = "width", Order = 1)]
        public int Width { get; set; }

        [DataMember(Name = "height", Order = 2)]
        public int Height { get; set; }

        [DataMember(Name = "stride", Order = 3)]
        public int Stride { get; set; }

        [DataMember(Name = "pixelFormat", Order = 4)]
        public string PixelFormat { get; set; } = RawPixelFormat.Mono8.ToString();

        [DataMember(Name = "validBits", Order = 5)]
        public int ValidBits { get; set; }

        [DataMember(Name = "byteOrder", Order = 6)]
        public string ByteOrder { get; set; } = RawByteOrder.LittleEndian.ToString();

        public static RawBufferSnapshotDto From(RawImageDescriptor descriptor)
        {
            return new RawBufferSnapshotDto
            {
                Width = descriptor.Width,
                Height = descriptor.Height,
                Stride = descriptor.Stride,
                PixelFormat = descriptor.PixelFormat.ToString(),
                ValidBits = descriptor.ValidBits,
                ByteOrder = descriptor.ByteOrder.ToString()
            };
        }

        public RawImageDescriptor ToDescriptor()
        {
            RawPixelFormat pixelFormat;
            RawByteOrder byteOrder;
            if (!Enum.TryParse(PixelFormat, true, out pixelFormat))
            {
                throw new InvalidDataException("Unknown pixel format: " + PixelFormat);
            }

            if (!Enum.TryParse(ByteOrder, true, out byteOrder))
            {
                throw new InvalidDataException("Unknown byte order: " + ByteOrder);
            }

            return new RawImageDescriptor
            {
                Width = Width,
                Height = Height,
                Stride = Stride,
                PixelFormat = pixelFormat,
                ValidBits = ValidBits,
                ByteOrder = byteOrder
            };
        }

        public string GetRawPath(string metadataPath)
        {
            if (string.IsNullOrWhiteSpace(RawFile))
            {
                throw new InvalidDataException("rawFile is required.");
            }

            if (Path.IsPathRooted(RawFile))
            {
                return RawFile;
            }

            var directory = Path.GetDirectoryName(metadataPath) ?? string.Empty;
            return Path.GetFullPath(Path.Combine(directory, RawFile));
        }
    }
}
