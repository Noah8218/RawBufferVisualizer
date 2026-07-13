using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace RawBufferVisualizer.VisualStudio
{
    public static class VisualStudioInstance
    {
        private const uint Th32csSnapProcess = 0x00000002;
        private const int MaxProcessDepth = 16;

        public static int GetCurrentProcessId()
        {
            var processes = GetProcessTree();
            var processId = Process.GetCurrentProcess().Id;

            for (var depth = 0; depth < MaxProcessDepth; depth++)
            {
                if (!processes.TryGetValue(processId, out var process) || process == null)
                {
                    break;
                }

                if (string.Equals(
                    Path.GetFileNameWithoutExtension(process.ExecutableName),
                    "devenv",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return processId;
                }

                processId = process.ParentProcessId;
            }

            throw new InvalidOperationException("The hosting Visual Studio instance could not be identified.");
        }

        public static object? GetDte(int processId)
        {
            IRunningObjectTable runningObjectTable;
            IBindCtx bindContext;
            if (GetRunningObjectTable(0, out runningObjectTable) != 0 || runningObjectTable == null)
            {
                return null;
            }

            if (CreateBindCtx(0, out bindContext) != 0 || bindContext == null)
            {
                return null;
            }

            IEnumMoniker monikerEnumerator;
            runningObjectTable.EnumRunning(out monikerEnumerator);
            var monikers = new IMoniker[1];
            var expectedName = "VisualStudio.DTE.17.0:" + processId;
            while (monikerEnumerator.Next(1, monikers, IntPtr.Zero) == 0)
            {
                string displayName;
                try
                {
                    monikers[0].GetDisplayName(bindContext, null, out displayName);
                }
                catch
                {
                    continue;
                }

                if (!displayName.EndsWith(expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                object dte;
                runningObjectTable.GetObject(monikers[0], out dte);
                return dte;
            }

            return null;
        }

        private static Dictionary<int, ProcessInfo> GetProcessTree()
        {
            var snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
            if (snapshot == new IntPtr(-1))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                var processes = new Dictionary<int, ProcessInfo>();
                var entry = new ProcessEntry32
                {
                    Size = (uint)Marshal.SizeOf(typeof(ProcessEntry32))
                };

                if (!Process32First(snapshot, ref entry))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                do
                {
                    processes[(int)entry.ProcessId] = new ProcessInfo((int)entry.ParentProcessId, entry.ExecutableName);
                    entry.Size = (uint)Marshal.SizeOf(typeof(ProcessEntry32));
                }
                while (Process32Next(snapshot, ref entry));

                return processes;
            }
            finally
            {
                CloseHandle(snapshot);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable runningObjectTable);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(int reserved, out IBindCtx bindContext);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry32
        {
            public uint Size;
            public uint Usage;
            public uint ProcessId;
            public IntPtr DefaultHeapId;
            public uint ModuleId;
            public uint Threads;
            public uint ParentProcessId;
            public int BasePriority;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ExecutableName;
        }

        private sealed class ProcessInfo
        {
            public int ParentProcessId { get; private set; }
            public string ExecutableName { get; private set; }

            public ProcessInfo(int parentProcessId, string executableName)
            {
                ParentProcessId = parentProcessId;
                ExecutableName = executableName;
            }
        }
    }
}
