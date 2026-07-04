using System;
using System.IO;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Recorder;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.Samples
{
    internal static class Program
    {
        private static int Main()
        {
            var width = 640;
            var height = 480;
            var stride = width;
            var buffer = new byte[stride * height];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    buffer[(y * stride) + x] = (byte)((x + y) & 0xFF);
                }
            }

            var descriptor = new RawImageDescriptor
            {
                Width = width,
                Height = height,
                Stride = stride,
                PixelFormat = RawPixelFormat.Mono8,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };

            var outputPath = Path.GetFullPath(Path.Combine("artifacts", "samples", "mono8-gradient.rbuf.json"));
            RawBufferSnapshot.Save(outputPath, buffer, descriptor);
            Console.WriteLine(outputPath);

            var vrecPath = Path.GetFullPath(Path.Combine("artifacts", "samples", "mono8-gradient.vrec"));
            using (var shot = VisionRecorder.Begin("SampleCam", "T001", "Main"))
            {
                shot.AddImage("01_raw", "Raw", buffer, descriptor);
                shot.AddParam("Threshold", 120, "01_raw");
                shot.AddRectangleRoi("01_raw", "SearchROI", 100, 80, 240, 180);
                shot.AddMeasure("Width", 12.345, "mm", 12.0, 13.0, true, "01_raw");
                shot.AddEvent("Grab Complete", 1.2);
                shot.Result(true);
                shot.Save(vrecPath);
            }

            Console.WriteLine(vrecPath);
            return 0;
        }
    }
}
