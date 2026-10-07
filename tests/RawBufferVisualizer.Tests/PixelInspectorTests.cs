using System;
using System.Linq;
using System.Windows.Media;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.VisualStudio.Vssdk;

namespace RawBufferVisualizer.Tests
{
    internal static class PixelInspectorTests
    {
        public static void RunAll()
        {
            var descriptor = new RawImageDescriptor { Width = 17, Height = 5, Stride = 56, PixelFormat = RawPixelFormat.BGR24, ValidBits = 8 };
            var bytes = new byte[280];
            for (int y = 0; y < 5; y++) for (int x = 0; x < 17; x++)
            {
                int i = y * 56 + x * 3;
                bytes[i] = (byte)(10 + x); bytes[i + 1] = (byte)(20 + y); bytes[i + 2] = (byte)(30 + x + y);
            }
            var model = new PixelInspectorViewModel();
            using (var source = RawImageSource.FromMemory(bytes, descriptor))
            {
                model.Update(source, descriptor, 8, 2, source.CreateRenderOptions());
                Require(model.Cells.Count == 25 && model.Cells[12].IsCenter && model.Cells.Count(cell => cell.IsCenter) == 1, "5×5 slots retain the center");
                Require(model.RawBytes == "12 16 28" && model.ByteOffset == "136 (0x88)", "Raw memory order and padded-row byte offset");
                Require(model.ChannelValues.Select(channel => channel.Name).SequenceEqual(new[] { "R", "G", "B", "GV" }), "RGB and gray value are distinct");
                Require(model.ChannelValues[0].Decimal == "40" && model.ChannelValues[0].Hex == "0x28", "Channel decimal/hex use source values");
                Require(((SolidColorBrush)model.Cells[12].Background).Color == Color.FromRgb(40, 22, 18), "Cell color uses rendered pixel");
                Require(model.ProfileSampleCount == 17 && model.ProfileR.Bounds.X == 30 && model.ProfileR.Bounds.Width == 238 && !model.ProfileG.IsEmpty() && !model.ProfileB.IsEmpty(), "All 17 RGB samples are graphed");
                model.SelectedChannel = "R";
                Require(model.Cells[12].Value == "40" && model.Mean == "40.00" && model.StandardDeviation == "2.000", "Channel selection changes numbers and statistics with the approved display precision");
                Require(model.HasPixelData && model.RawByteNote == " (B G R)" && model.ProfileRMarker.Bounds.X == 146, "Pixel state, memory-order note and current profile point are presented");
                model.SelectedChannel = "GV";
                model.Update(source, descriptor, 0, 0, source.CreateRenderOptions());
                Require(model.Cells.Count == 25 && model.Cells[0].Value == "—" && model.Cells[12].IsCenter && model.Count == "9" && model.ProfileSampleCount == 9, "Corner leaves missing cells blank and bounds statistics/profile");
                Require(model.ProfileStart == string.Empty && model.ProfileEnd == "8" && model.ProfileCaption == "Row 0 · x 0–8" && model.ProfileR.Bounds.X == 149 && model.ProfileR.Bounds.Width == 119, "Left-edge profile retains the selected pixel at the center and leaves unavailable positions blank");
                model.Update(source, descriptor, 16, 0, source.CreateRenderOptions());
                Require(model.ProfileStart == "8" && model.ProfileEnd == string.Empty && model.ProfileCaption == "Row 0 · x 8–16" && model.ProfileSampleCount == 9 && model.ProfileR.Bounds.X == 30 && model.ProfileR.Bounds.Width == 119, "Right-edge profile retains the same coordinate frame and only valid samples");
            }
            foreach (var order in new[] { RawByteOrder.LittleEndian, RawByteOrder.BigEndian })
            {
                var floatDescriptor = new RawImageDescriptor { Width = 5, Height = 1, Stride = 20, PixelFormat = RawPixelFormat.Float32, ValidBits = 32, ByteOrder = order };
                var values = new[] { 1e-9f, float.NaN, float.PositiveInfinity, -0.25f, 0.75f };
                var data = values.SelectMany(value => { var encoded = BitConverter.GetBytes(value); if (order == RawByteOrder.BigEndian) Array.Reverse(encoded); return encoded; }).ToArray();
                using (var source = RawImageSource.FromMemory(data, floatDescriptor))
                {
                    model.Update(source, floatDescriptor, 2, 0, source.CreateRenderOptions());
                    Require(!model.IsColor && model.Count == "3" && model.Minimum == "-0.250" && model.Maximum == "0.750", "Float statistics exclude non-finite values in either byte order");
                    Require(model.Cells[12].Value == "Infinity" && !model.ProfileValue.IsEmpty(), "Non-finite grid value is explicit while finite graph segments remain");
                    Require(model.ChannelValues[0].Hex == "0x7F800000", "Float hex retains the IEEE-754 bits in either byte order");
                    Require(model.ProfileValueMarker.IsEmpty() && model.RawByteNote.Contains(order == RawByteOrder.LittleEndian ? "little-endian" : "big-endian"), "Non-finite current value has no misleading graph marker and byte-order note stays accurate");
                    model.Update(source, floatDescriptor, 0, 0, source.CreateRenderOptions());
                    Require(model.ChannelValues[0].Decimal == "0.00000" && model.ChannelValues[0].Hex == "0x3089705F" && model.Cells[12].Value == "0.000", "Tiny float uses approved visible precision while its exact IEEE-754 bits remain available");
                    model.Update(source, floatDescriptor, 4, 0, source.CreateRenderOptions());
                    Require(model.ChannelValues[0].Decimal == "0.75000" && model.Cells[12].Value == "0.750", "Finite float recovers after non-finite input with the approved point and cell precision");
                    var range = source.CreateRenderOptions();
                    range.BlackLevel = 0.5; range.WhiteLevel = 1.2;
                    model.Update(source, floatDescriptor, 4, 0, range);
                    Require(model.ProfileLowLabel == "0.50" && model.ProfileMiddleLabel == "0.85" && model.ProfileHighLabel == "1.20", "Float range labels use the approved two decimal places");
                }
            }
            var largeFloatDescriptor = new RawImageDescriptor { Width = 1, Height = 1, Stride = 4, PixelFormat = RawPixelFormat.Float32, ValidBits = 32 };
            using (var source = RawImageSource.FromMemory(BitConverter.GetBytes(float.MaxValue), largeFloatDescriptor))
            {
                model.Update(source, largeFloatDescriptor, 0, 0, source.CreateRenderOptions());
                Require(model.ChannelValues[0].Decimal == "3.40282347E+38" && model.ChannelValues[0].Hex == "0x7F7FFFFF" && model.Cells[12].Value.Contains("E+38"), "Large finite float remains readable without changing its raw bits");
            }
            var integerDescriptor = new RawImageDescriptor { Width = 1, Height = 1, Stride = 4, PixelFormat = RawPixelFormat.Int32, ValidBits = 32 };
            using (var source = RawImageSource.FromMemory(BitConverter.GetBytes(-123456789), integerDescriptor))
            {
                model.Update(source, integerDescriptor, 0, 0, source.CreateRenderOptions());
                Require(model.ChannelValues[0].Decimal == "-123456789" && model.ChannelValues[0].Hex == "0xF8A432EB" && model.Count == "1", "Signed decimal and raw hex remain distinct at the smallest image boundary");
            }
            model.Clear();
            Require(!model.HasPixelData && model.Cells.Count == 0 && model.ChannelValues.Count == 0 && model.ProfileValue.IsEmpty() && model.ProfileRMarker.IsEmpty(), "Clear releases presentation snapshots and current points");
            Require(new PixelInspectorViewModel.NeighborhoodCell("120", Color.FromRgb(120, 120, 120), false).Foreground == Brushes.Black &&
                new PixelInspectorViewModel.NeighborhoodCell("80", Color.FromRgb(80, 80, 80), false).Foreground == Brushes.White, "Cell text follows the approved relative-luminance contrast threshold");
            Console.WriteLine("Pixel inspector: padded RGB, channel switch, corners, 17-point profiles, float byte orders/non-finite bits, signed integer and clear passed.");
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
