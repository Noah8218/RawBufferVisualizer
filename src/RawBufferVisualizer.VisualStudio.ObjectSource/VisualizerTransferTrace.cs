using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    internal static class VisualizerTransferTrace
    {
        internal static void Write(string stage)
        {
            var prefix = Environment.GetEnvironmentVariable("RAWBUFFERVISUALIZER_TRANSFER_TRACE");
            if (string.IsNullOrWhiteSpace(prefix)) return;
            try
            {
                using (var process = Process.GetCurrentProcess())
                    File.AppendAllText(prefix + "-" + process.Id.ToString(CultureInfo.InvariantCulture) + ".log",
                        DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " " + stage
                        + " GC=" + GC.CollectionCount(0) + "/" + GC.CollectionCount(1) + "/" + GC.CollectionCount(2) + Environment.NewLine);
            }
            catch { /* Optional diagnostics must not affect a debugger evaluation. */ }
        }
    }
}
