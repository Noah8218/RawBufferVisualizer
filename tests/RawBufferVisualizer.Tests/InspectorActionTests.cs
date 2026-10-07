using System;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.VisualStudio.Vssdk;

namespace RawBufferVisualizer.Tests
{
    internal static class InspectorActionTests
    {
        public static void RunAll()
        {
            var slots = new CompareSelectionViewModel<object>();
            Check(!slots.SwapCommand.CanExecute(null), "Empty comparison cannot swap");
            var a = new object(); var b = new object();
            slots.A = a; slots.SwapCommand.Execute(null);
            Check(slots.A == null && slots.B == a, "Swap works with one slot");
            slots.A = b; slots.SwapCommand.Execute(null);
            Check(slots.A == a && slots.B == b, "Swap exchanges both document identities");
            slots.SwapCommand.Execute(null);
            Check(slots.A == b && slots.B == a, "Repeated Swap restores the identities");
            slots.A = slots.B = null;
            Check(!slots.SwapCommand.CanExecute(null), "Cleared comparison disables Swap");

            var layout = new BufferLayoutViewModel();
            var declared = new RawImageDescriptor { Width = 17, Height = 5, Stride = 56, PixelFormat = RawPixelFormat.BGR24, ValidBits = 8 };
            layout.SetSource(declared, declared, 280);
            RawImageDescriptor? applied = null; var count = 0;
            layout.Applied += value => { applied = value.Clone(); count++; };
            var failure = string.Empty; layout.Failed += message => failure = message;
            layout.Width = "16";
            Check(layout.Derived == "Row bytes 48 · padding 8 B/row · buffer 280 B", "Derived values follow edited fields and actual buffer length");
            layout.ResetCommand.Execute(null);
            Check(layout.Width == "17" && count == 0, "Reset abandons an uncommitted edit without executing interpretation");
            layout.Width = "16"; layout.ApplyCommand.Execute(null);
            Check(applied?.Width == 16 && count == 1 && declared.Width == 17, "Apply emits an independent descriptor");
            layout.ResetCommand.Execute(null);
            Check(applied?.Width == 17 && count == 2 && layout.Error == "", "Reset restores the declared layout after Apply");
            layout.Width = "-1"; layout.ApplyCommand.Execute(null);
            Check(count == 2 && layout.Error == "Width must be a positive integer." && failure == layout.Error, "Invalid layout does not emit a result");
            layout.ResetCommand.Execute(null);
            Check(layout.Width == "17" && layout.Error == "" && count == 2, "Reset recovers invalid input without changing the image");
            layout.Applied += _ => throw new InvalidOperationException("Unreadable source");
            layout.Width = "16"; layout.ApplyCommand.Execute(null);
            Check(layout.Error == "Unreadable source", "Apply result failure is visible");
            layout.Clear();
            Check(!layout.ApplyCommand.CanExecute(null) && !layout.ResetCommand.CanExecute(null) && layout.Derived == "", "Cleared form has no stale state or available actions");
            Console.WriteLine("Inspector actions: Swap identities, layout edit/apply/reset/error/recovery passed.");
        }

        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
