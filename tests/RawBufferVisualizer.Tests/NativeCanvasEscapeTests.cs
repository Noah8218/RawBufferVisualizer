using System;
using System.Reflection;
using System.Threading;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.OpenGlCanvas;
using Forms = System.Windows.Forms;

namespace RawBufferVisualizer.Tests
{
    internal static class NativeCanvasEscapeTests
    {
        public static void RunAll()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try { RunOnSta(); }
                catch (Exception ex) { failure = ex; }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(20))) throw new TimeoutException("Native canvas input check exceeded 20 seconds.");
            if (failure != null) throw new InvalidOperationException("Native canvas input check failed.", failure);
        }

        private static void RunOnSta()
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var preview = typeof(Forms.Control).GetMethod("OnPreviewKeyDown", flags)!;
            var keyDown = typeof(Forms.Control).GetMethod("OnKeyDown", flags)!;
            var canvas = new RawOpenGlImageCanvas();
            var control = (Forms.Control)typeof(RawOpenGlImageCanvas).GetField("_openGlControl", flags)!.GetValue(canvas)!;
            var host = (IDisposable)typeof(RawOpenGlImageCanvas).GetField("_host", flags)!.GetValue(canvas)!;
            var checks = 0;
            var cleared = 0;
            void Check(bool valid, string message)
            {
                if (!valid) throw new InvalidOperationException(message);
                checks++;
                Console.WriteLine("PASS " + message);
            }
            Forms.PreviewKeyDownEventArgs Preview(Forms.Keys key)
            {
                var args = new Forms.PreviewKeyDownEventArgs(key);
                preview.Invoke(control, new object[] { args });
                return args;
            }
            Forms.KeyEventArgs Down(Forms.Keys key)
            {
                var args = new Forms.KeyEventArgs(key);
                keyDown.Invoke(control, new object[] { args });
                return args;
            }
            using var source = RawImageSource.FromMemory(new byte[] { 42 }, new RawImageDescriptor
            {
                Width = 1, Height = 1, Stride = 1, PixelFormat = RawPixelFormat.Mono8, ValidBits = 8
            });
            try
            {
                canvas.LoadRawImageSource(source);
                // Load resets the old image and emits its own clear notification before this key test.
                canvas.PixelPinned += (_, e) => { if (e.X < 0 && e.Y < 0) cleared++; };
                Check(!Preview(Forms.Keys.Escape).IsInputKey && !Down(Forms.Keys.Escape).Handled,
                    "Unpinned Escape stays available to the host");
                Check(canvas.PinMarkerAtImagePixel(0, 0), "One-pixel marker is admitted");
                Check(Preview(Forms.Keys.Escape).IsInputKey, "Pinned Escape is admitted through native input preprocessing");
                Check(canvas.PinnedMarkerText == string.Empty && cleared == 1,
                    "Escape preview clears the marker even when the IDE omits KeyDown");
                var escape = Down(Forms.Keys.Escape);
                Check(escape.Handled && escape.SuppressKeyPress && canvas.PinnedMarkerText == string.Empty && cleared == 1,
                    "Native Escape clears the marker and notifies its owner exactly once"
                    + $" (handled={escape.Handled}, suppressed={escape.SuppressKeyPress}, marker='{canvas.PinnedMarkerText}', clears={cleared})");
                Check(!Preview(Forms.Keys.Escape).IsInputKey && !Down(Forms.Keys.Escape).Handled && cleared == 1,
                    "Repeated unpinned Escape does not emit another clear event");
                canvas.PinMarkerAtImagePixel(0, 0);
                Preview(Forms.Keys.Escape);
                Check(canvas.PinnedMarkerText == string.Empty && cleared == 2,
                    "A second preview-only Escape clears its own marker once");
                Check(!Preview(Forms.Keys.A).IsInputKey && !Down(Forms.Keys.A).Handled,
                    "A missing Escape KeyDown does not consume the next ordinary key");
                Check(!Preview(Forms.Keys.Escape).IsInputKey && !Down(Forms.Keys.Escape).Handled && cleared == 2,
                    "A missing KeyDown leaves no stale Escape ownership on the next preview");
                canvas.PinMarkerAtImagePixel(0, 0);
                foreach (var modifier in new[] { Forms.Keys.Control, Forms.Keys.Shift, Forms.Keys.Alt })
                    Check(!Preview(modifier | Forms.Keys.Escape).IsInputKey
                        && !Down(modifier | Forms.Keys.Escape).Handled && canvas.PinnedMarkerText != string.Empty,
                        modifier + "+Escape preserves the marker and host shortcut");
                Check(!Preview(Forms.Keys.A).IsInputKey && !Down(Forms.Keys.A).Handled && canvas.PinnedMarkerText != string.Empty,
                    "Ordinary native keys are not intercepted");
                canvas.ClearImage();
                Check(!Preview(Forms.Keys.Escape).IsInputKey && !Down(Forms.Keys.Escape).Handled,
                    "Cleared canvas releases Escape to the host");
                Console.WriteLine("Native canvas Escape: " + checks + " assertions; unshown component events, not desktop key injection.");
            }
            finally
            {
                canvas.ClearImage();
                host.Dispose();
            }
        }
    }
}
