using System;
using System.Collections.Generic;
using System.Linq;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.VisualStudio.Vssdk;

namespace RawBufferVisualizer.Tests
{
    internal static class SplitPixelInspectionTests
    {
        private static void RunFloatByteOrderCheck()
        {
            var descriptor = new RawImageDescriptor { Width = 4, Height = 2, Stride = 20, PixelFormat = RawPixelFormat.Float32, ValidBits = 32, ByteOrder = RawByteOrder.BigEndian };
            var data = new byte[40];
            for (var y = 0; y < 2; y++) for (var x = 0; x < 4; x++)
            {
                var index = y * descriptor.Stride + x * 4;
                data[index] = 0x3F;
                data[index + 1] = 0xC0;
            }
            using var a = RawImageSource.FromMemory(data, descriptor);
            using var b = RawImageSource.FromMemory(data, descriptor);
            using var split = new RawImageSplitSource(a, b);
            var model = Inspect(split, 3, 1);
            Require(model.RawBytes == "3F C0 00 00" && model.RawByteNote.Contains("big-endian"), "Split must show the original big-endian bytes and note.");
            Require(Value(model, "Value") == "1.50000" && model.ChannelValues.Single(value => value.Name == "Value").Hex == "0x3FC00000", "Float numeric value and IEEE-754 bits must use the same source byte order.");
            Require(model.ByteOffset == "32 (0x20)", "Float offset must retain original padded stride.");
            Console.WriteLine("PASS Split big-endian float source value, bytes, note, IEEE-754 bits and offset");
        }

        public static void RunAll()
        {
            var failures = new List<Exception>();
            Check("Split B color, raw bytes and padded source offset", () =>
            {
                using var a = Color(60, 255, false);
                using var b = Color(40, 255, false);
                using var split = new RawImageSplitSource(a, b);
                var model = Inspect(split, 3, 1);
                Require(model.IsColor && Value(model, "R") == "40" && Value(model, "G") == "80" && Value(model, "B") == "120", "B wrapper must not hide the blue channel.");
                Require(model.RawBytes == "78 50 28" && model.RawByteNote == " (B G R)", "Raw bytes must exclude wrapper punctuation and retain source order.");
                Require(model.ByteOffset == "25 (0x19)", "Offset must use the padded BGR24 source, not generated BGRA32.");
            });
            Check("Split A original alpha", () =>
            {
                using var a = Color(60, 17, true);
                using var b = Color(40, 255, true);
                using var split = new RawImageSplitSource(a, b);
                var model = Inspect(split, 0, 0);
                Require(Value(model, "A") == "17" && model.RawBytes == "78 50 3C 11", "A wrapper must not hide the original alpha.");
            });
            Check("Nested split wrappers", () =>
            {
                using var a = Color(60, 255, false);
                using var b = Color(40, 255, false);
                using var inner = new RawImageSplitSource(a, b);
                using var outer = new RawImageSplitSource(a, inner);
                var model = Inspect(outer, 3, 1);
                Require(model.IsColor && Value(model, "R") == "40" && model.RawBytes == "78 50 28" && model.ByteOffset == "25 (0x19)", "Nested source identity must select the innermost pixel layout.");
            });
            Check("Split Mono16 original scalar and offset", () =>
            {
                var descriptor = new RawImageDescriptor { Width = 4, Height = 2, Stride = 10, PixelFormat = RawPixelFormat.Mono16, ValidBits = 16 };
                var data = new byte[20];
                for (var y = 0; y < 2; y++) for (var x = 0; x < 4; x++) { data[y * 10 + x * 2] = 1; data[y * 10 + x * 2 + 1] = 2; }
                using var a = RawImageSource.FromMemory(data, descriptor);
                using var b = RawImageSource.FromMemory(data, descriptor);
                using var split = new RawImageSplitSource(a, b);
                var model = Inspect(split, 1, 1);
                Require(!model.IsColor && Value(model, "GV") == "513" && model.RawBytes == "01 02" && model.ByteOffset == "12 (0xC)", "Scalar raw bytes and offsets must remain source values.");
            });
            {
                Check("Diff generated pixels precede source annotations", () =>
                {
                    using var a = Color(60, 255, false);
                    using var b = Color(40, 255, false);
                    using var diff = new RawImageDifferenceSource(a, b);
                    var model = Inspect(diff, 3, 1);
                    Require(model.IsColor && Value(model, "R") == "20" && Value(model, "G") == "0" && Value(model, "B") == "0" && Value(model, "A") == "255", "Diff must keep its generated channels before A/B annotations.");
                    Require(model.RawBytes == "00 00 14 FF" && model.ByteOffset == "28 (0x1C)", "Diff bytes must retain the generated BGRA32 layout.");
                });
                Check("Pixel layout copies, bounds and missing token guards", () =>
                {
                    using var a = Color(60, 255, false);
                    using var b = Color(40, 255, true);
                    using var split = new RawImageSplitSource(a, b);
                    var selected = split.GetPixelDescriptor(0, 0);
                    Require(selected.PixelFormat == RawPixelFormat.BGR24 && split.GetPixelDescriptor(3, 0).PixelFormat == RawPixelFormat.BGRA32, "Inspection layout follows the selected source.");
                    selected.Stride = 1;
                    Require(split.GetPixelDescriptor(0, 0).Stride == 16, "Returned layout must not mutate a source.");
                    try { split.GetPixelDescriptor(-1, 0); throw new InvalidOperationException("Negative pixel was admitted."); }
                    catch (ArgumentException) { }
                    Require(RawPixelInspector.ReadDescriptionToken(null, "B=") == null
                        && RawPixelInspector.ReadDescriptionToken("B=[X=1]", "B=") == null
                        && RawPixelInspector.ReadDescriptionToken("B=120", "") == null,
                        "Absent or wrapper-only tokens must remain missing without an unbounded search.");
                });
            }
            Check("Split big-endian float interpretation", RunFloatByteOrderCheck);
            if (failures.Count != 0) throw new AggregateException(failures);
            Console.WriteLine("Split inspection focused checks passed: 7");

            void Check(string name, Action action)
            {
                try { action(); Console.WriteLine("PASS " + name); }
                catch (Exception error) { failures.Add(new Exception(name + ": " + error.Message, error)); Console.WriteLine("FAIL " + name + ": " + error.Message); }
            }
        }

        private static PixelInspectorViewModel Inspect(RawImageSource source, int x, int y)
        {
            var model = new PixelInspectorViewModel();
            model.Update(source, source.Descriptor, x, y, source.CreateRenderOptions());
            return model;
        }

        private static RawImageSource Color(byte red, byte alpha, bool withAlpha)
        {
            var bytesPerPixel = withAlpha ? 4 : 3;
            var descriptor = new RawImageDescriptor { Width = 4, Height = 2, Stride = 16, PixelFormat = withAlpha ? RawPixelFormat.BGRA32 : RawPixelFormat.BGR24, ValidBits = 8 };
            var bytes = new byte[32];
            for (var y = 0; y < 2; y++) for (var x = 0; x < 4; x++)
            {
                var i = y * 16 + x * bytesPerPixel;
                bytes[i] = 120; bytes[i + 1] = 80; bytes[i + 2] = red;
                if (withAlpha) bytes[i + 3] = alpha;
            }
            return RawImageSource.FromMemory(bytes, descriptor);
        }

        private static string Value(PixelInspectorViewModel model, string channel) => model.ChannelValues.Single(value => value.Name == channel).Decimal;
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
