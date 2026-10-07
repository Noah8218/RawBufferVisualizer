using System;
using RawBufferVisualizer.VisualStudio.Vssdk;

namespace RawBufferVisualizer.Tests
{
    internal static class InspectorNavigationTests
    {
        public static void RunAll()
        {
            foreach (var width in new[] { 640, 759, 760, 1000, 1440 })
            {
                var model = new InspectorNavigationViewModel();
                model.SetWidth(width);
                Require(model.IsNarrow == (width < 760) && model.IsOpen == (width >= 760), "Default at " + width);
                foreach (var section in new[] { "Pixel", "Histogram", "Format", "Compare", "Diagnostics" })
                {
                    model.Show(section);
                    Require(model.IsOpen && model.Section == section, "Section available at " + width);
                    model.CloseCommand.Execute(null);
                    Require(!model.IsOpen && !model.CloseCommand.CanExecute(null), "Collapse at " + width);
                }
                model.IsCompareSelected = true;
                Require(model.IsCompareSelected && !model.IsPixelSelected, "Select one section");
                model.IsCompareSelected = false;
                Require(!model.IsOpen, "Repeat active rail button collapses");
                model.SetWidth(width == 759 ? 758 : width + 1);
                Require(!model.IsOpen, "Resize preserves user collapse");
            }
            var navigation = new InspectorNavigationViewModel();
            navigation.SetWidth(1000);
            navigation.IsFormatSelected = true;
            Require(navigation.Title == "Buffer format", "Format title");
            navigation.Close();
            navigation.SetWidth(640);
            Require(!navigation.IsOpen, "Overlay starts closed");
            navigation.IsHistogramSelected = true;
            navigation.SetWidth(1440);
            Require(!navigation.IsOpen && navigation.Section == "Histogram", "Docked collapse survives overlay use");
            navigation.SetWidth(640);
            Require(navigation.IsHistogramSelected, "Overlay selection survives a width round trip");
            Console.WriteLine("Inspector navigation: five widths, all sections, collapse and resize transitions passed.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
