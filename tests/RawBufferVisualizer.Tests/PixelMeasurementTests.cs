using System;
using RawBufferVisualizer.Presentation;

namespace RawBufferVisualizer.Tests
{
    internal static class PixelMeasurementTests
    {
        public static void RunAll()
        {
            var model = new PixelMeasurementViewModel();
            Require(!model.CanMeasure && !model.SelectCommand.CanExecute(null) && !model.ClearCommand.CanExecute(null), "Empty image disables measurement");
            model.IsEnabled = true;
            Require(!model.IsEnabled, "Cannot enable without an image");
            model.SetImageSize(2, 2, isPreview: true);
            model.IsEnabled = true;
            model.SelectCommand.Execute(Tuple.Create(1, 1));
            Require(!model.CanMeasure && !model.IsEnabled && model.Start == null && model.MeasurementToolTip.Contains("sampled previews"), "Preview rejects mode and pixel selection and explains why");
            model.SetImageSize(8, 8);
            model.IsEnabled = true;
            Require(model.IsEnabled && model.ModeText == "Measuring", "Mode round trip");

            Pair(model, 1, 2, 6, 2, "Width 5 px · Height 0 px");
            Pair(model, 3, 7, 3, 0, "Width 0 px · Height 7 px");
            Pair(model, 6, 5, 1, 2, "Width 5 px · Height 3 px");
            Pair(model, 4, 4, 4, 4, "Width 0 px · Height 0 px");
            model.SelectCommand.Execute(Tuple.Create(7, 7));
            Require(model.Start?.Item1 == 7 && model.End == null, "Third click starts the next pair");
            foreach (var invalid in new[] { Tuple.Create(-1, 0), Tuple.Create(0, -1), Tuple.Create(8, 0), Tuple.Create(0, 8) })
                model.SelectCommand.Execute(invalid);
            Require(model.Start?.Item1 == 7 && model.End == null, "Outside pixels do not advance measurement");
            model.SelectCommand.Execute(null);
            model.SelectCommand.Execute("invalid");
            Require(model.End == null, "Unknown command parameters do not advance measurement");

            model.ClearCommand.Execute(null);
            Require(model.Start == null && model.End == null && model.IsEnabled && !model.ClearCommand.CanExecute(null), "Clear retains mode and disables itself");
            Pair(model, 0, 0, 7, 7, "Width 7 px · Height 7 px");
            model.IsEnabled = false;
            model.SelectCommand.Execute(Tuple.Create(1, 1));
            Require(model.Start == null && model.End == null, "Mode off clears and ignores pixel selection");
            model.IsEnabled = true;
            Pair(model, 1, 1, 2, 2, "Width 1 px · Height 1 px");
            model.SetImageSize(16, 9);
            Require(!model.IsEnabled && model.Start == null && model.End == null && model.CanMeasure, "Image change resets all measurement state");
            model.IsEnabled = true;
            Pair(model, 1, 1, 2, 2, "Width 1 px · Height 1 px");
            model.SetImageSize(2, 2, isPreview: true);
            Require(!model.CanMeasure && !model.IsEnabled && model.Start == null && model.End == null && !model.ClearCommand.CanExecute(null), "Preview after full image clears mode and endpoints");
            model.SetImageSize(0, 0);
            Require(!model.CanMeasure && !model.SelectCommand.CanExecute(null), "Clear/error/source loss disables measurement");
            model.SetImageSize(int.MaxValue, int.MaxValue);
            model.IsEnabled = true;
            Pair(model, int.MaxValue - 1, int.MaxValue - 1, 0, 0, "Width 2147483646 px · Height 2147483646 px");
            Console.WriteLine("Pixel measurement: coordinate distances, bounds, mode, command and reset transitions passed.");
        }

        private static void Pair(PixelMeasurementViewModel model, int x1, int y1, int x2, int y2, string expected)
        {
            model.ClearCommand.Execute(null);
            model.SelectCommand.Execute(Tuple.Create(x1, y1));
            Require(model.Start != null && model.End == null && model.ClearCommand.CanExecute(null), "First endpoint accepted");
            model.SelectCommand.Execute(Tuple.Create(x2, y2));
            Require(model.Status == expected, expected);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
