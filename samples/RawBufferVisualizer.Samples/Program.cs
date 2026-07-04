using System;
using System.IO;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.Samples
{
    internal static class Program
    {
        private static int Main()
        {
            var outputDirectory = Path.GetFullPath(Path.Combine("artifacts", "samples"));
            Directory.CreateDirectory(outputDirectory);

            var mono8 = CreateMono8Sample(640, 480);
            SaveSample(outputDirectory, "mono8-gradient", mono8.Buffer, mono8.Descriptor);
            SaveSample(outputDirectory, "mono16-gradient", CreateMono16Sample(320, 240), CreateDescriptor(320, 240, 640, RawPixelFormat.Mono16, 16));
            SaveSample(outputDirectory, "mono10-packed-lsb", CreatePackedMonoSample(320, 240, 10), CreateDescriptor(320, 240, 400, RawPixelFormat.Mono10PackedLsb, 10));
            SaveSample(outputDirectory, "mono12-packed-lsb", CreatePackedMonoSample(320, 240, 12), CreateDescriptor(320, 240, 480, RawPixelFormat.Mono12PackedLsb, 12));
            SaveSample(outputDirectory, "binary-checker", CreateBinarySample(320, 240), CreateDescriptor(320, 240, 320, RawPixelFormat.Binary, 1));
            SaveSample(outputDirectory, "rgb24-color", CreateRgb24Sample(320, 240, true), CreateDescriptor(320, 240, 960, RawPixelFormat.RGB24, 8));
            SaveSample(outputDirectory, "bgr24-color", CreateRgb24Sample(320, 240, false), CreateDescriptor(320, 240, 960, RawPixelFormat.BGR24, 8));
            SaveSample(outputDirectory, "bgra32-color", CreateBgra32Sample(320, 240), CreateDescriptor(320, 240, 1280, RawPixelFormat.BGRA32, 8));
            SaveSample(outputDirectory, "float32-height", CreateFloat32Sample(320, 240), CreateDescriptor(320, 240, 1280, RawPixelFormat.Float32, 32));
            SaveSample(outputDirectory, "bayer-rggb8-color", CreateBayer8Sample(320, 240, RawPixelFormat.BayerRGGB8), CreateDescriptor(320, 240, 320, RawPixelFormat.BayerRGGB8, 8));
            SaveSample(outputDirectory, "bayer-grbg8-color", CreateBayer8Sample(320, 240, RawPixelFormat.BayerGRBG8), CreateDescriptor(320, 240, 320, RawPixelFormat.BayerGRBG8, 8));
            SaveSample(outputDirectory, "bayer-gbrg8-color", CreateBayer8Sample(320, 240, RawPixelFormat.BayerGBRG8), CreateDescriptor(320, 240, 320, RawPixelFormat.BayerGBRG8, 8));
            SaveSample(outputDirectory, "bayer-bggr8-color", CreateBayer8Sample(320, 240, RawPixelFormat.BayerBGGR8), CreateDescriptor(320, 240, 320, RawPixelFormat.BayerBGGR8, 8));

            return 0;
        }

        private static SampleImage CreateMono8Sample(int width, int height)
        {
            var descriptor = CreateDescriptor(width, height, width, RawPixelFormat.Mono8, 8);
            var buffer = new byte[descriptor.Stride * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    buffer[(y * descriptor.Stride) + x] = (byte)((x + y) & 0xFF);
                }
            }

            return new SampleImage(buffer, descriptor);
        }

        private static byte[] CreateMono16Sample(int width, int height)
        {
            var buffer = new byte[width * height * 2];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var value = (ushort)(((x * 256) + (y * 128)) & 0xFFFF);
                    WriteUInt16(buffer, ((y * width) + x) * 2, value);
                }
            }

            return buffer;
        }

        private static byte[] CreatePackedMonoSample(int width, int height, int bitsPerPixel)
        {
            var stride = (width * bitsPerPixel + 7) / 8;
            var buffer = new byte[stride * height];
            var max = (1 << bitsPerPixel) - 1;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var value = ((x * max) / Math.Max(width - 1, 1) + (y * 17)) & max;
                    WritePackedLsb(buffer, y * stride, x, bitsPerPixel, value);
                }
            }

            return buffer;
        }

        private static byte[] CreateBinarySample(int width, int height)
        {
            var buffer = new byte[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    buffer[(y * width) + x] = ((x / 16 + y / 16) & 1) == 0 ? (byte)0 : (byte)255;
                }
            }

            return buffer;
        }

        private static byte[] CreateRgb24Sample(int width, int height, bool rgb)
        {
            var buffer = new byte[width * height * 3];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var r = (byte)(x * 255 / Math.Max(width - 1, 1));
                    var g = (byte)(y * 255 / Math.Max(height - 1, 1));
                    var b = (byte)((x + y) & 0xFF);
                    var offset = ((y * width) + x) * 3;
                    if (rgb)
                    {
                        buffer[offset] = r;
                        buffer[offset + 1] = g;
                        buffer[offset + 2] = b;
                    }
                    else
                    {
                        buffer[offset] = b;
                        buffer[offset + 1] = g;
                        buffer[offset + 2] = r;
                    }
                }
            }

            return buffer;
        }

        private static byte[] CreateBgra32Sample(int width, int height)
        {
            var buffer = new byte[width * height * 4];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var offset = ((y * width) + x) * 4;
                    buffer[offset] = (byte)((x + y) & 0xFF);
                    buffer[offset + 1] = (byte)(y * 255 / Math.Max(height - 1, 1));
                    buffer[offset + 2] = (byte)(x * 255 / Math.Max(width - 1, 1));
                    buffer[offset + 3] = 255;
                }
            }

            return buffer;
        }

        private static byte[] CreateFloat32Sample(int width, int height)
        {
            var buffer = new byte[width * height * 4];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var value = (float)(Math.Sin(x * 0.05) + Math.Cos(y * 0.05));
                    var bytes = BitConverter.GetBytes(value);
                    Buffer.BlockCopy(bytes, 0, buffer, ((y * width) + x) * 4, 4);
                }
            }

            return buffer;
        }

        private static byte[] CreateBayer8Sample(int width, int height, RawPixelFormat format)
        {
            var buffer = new byte[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var color = GetBayerColor(format, x, y);
                    buffer[(y * width) + x] = color == 0
                        ? (byte)(x * 255 / Math.Max(width - 1, 1))
                        : color == 1
                            ? (byte)(y * 255 / Math.Max(height - 1, 1))
                            : (byte)((x + y) & 0xFF);
                }
            }

            return buffer;
        }

        private static RawImageDescriptor CreateDescriptor(int width, int height, int stride, RawPixelFormat pixelFormat, int validBits)
        {
            return new RawImageDescriptor
            {
                Width = width,
                Height = height,
                Stride = stride,
                PixelFormat = pixelFormat,
                ValidBits = validBits,
                ByteOrder = RawByteOrder.LittleEndian
            };
        }

        private static void SaveSample(string outputDirectory, string name, byte[] buffer, RawImageDescriptor descriptor)
        {
            var outputPath = Path.Combine(outputDirectory, name + ".rbuf.json");
            RawBufferSnapshot.Save(outputPath, buffer, descriptor);
            Console.WriteLine(outputPath);
        }

        private static int GetBayerColor(RawPixelFormat format, int x, int y)
        {
            var evenX = (x & 1) == 0;
            var evenY = (y & 1) == 0;

            switch (format)
            {
                case RawPixelFormat.BayerRGGB8:
                    return evenY ? (evenX ? 0 : 1) : (evenX ? 1 : 2);
                case RawPixelFormat.BayerGRBG8:
                    return evenY ? (evenX ? 1 : 0) : (evenX ? 2 : 1);
                case RawPixelFormat.BayerGBRG8:
                    return evenY ? (evenX ? 1 : 2) : (evenX ? 0 : 1);
                case RawPixelFormat.BayerBGGR8:
                    return evenY ? (evenX ? 2 : 1) : (evenX ? 1 : 0);
                default:
                    return 1;
            }
        }

        private static void WriteUInt16(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)(value >> 8);
        }

        private static void WritePackedLsb(byte[] buffer, int rowOffset, int x, int bitsPerPixel, int value)
        {
            var bitOffset = x * bitsPerPixel;
            for (var bit = 0; bit < bitsPerPixel; bit++)
            {
                if (((value >> bit) & 1) == 0)
                {
                    continue;
                }

                var targetBit = bitOffset + bit;
                buffer[rowOffset + (targetBit / 8)] |= (byte)(1 << (targetBit % 8));
            }
        }

        private sealed class SampleImage
        {
            public byte[] Buffer { get; private set; }
            public RawImageDescriptor Descriptor { get; private set; }

            public SampleImage(byte[] buffer, RawImageDescriptor descriptor)
            {
                Buffer = buffer;
                Descriptor = descriptor;
            }
        }
    }
}
