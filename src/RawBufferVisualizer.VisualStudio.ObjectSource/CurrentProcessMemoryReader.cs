using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    internal sealed class CurrentProcessMemoryReader
    {
        private static readonly IntPtr CurrentProcessHandle = GetCurrentProcess();

        private readonly IntPtr _address;
        private readonly long _length;
        private readonly byte[] _pageCache;
        private long _pageCacheOffset = -1;
        private int _pageCacheCount;

        internal CurrentProcessMemoryReader(IntPtr address, long length)
        {
            if (address == IntPtr.Zero)
            {
                throw new ArgumentException("Image data pointer is empty.", nameof(address));
            }

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            _address = address;
            _length = length;
            _pageCache = new byte[Math.Max(1, Environment.SystemPageSize)];
        }

        internal static CurrentProcessMemoryReader CreateForRows(
            IntPtr scan0,
            int sourceStride,
            int height,
            long length)
        {
            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            var address = sourceStride < 0
                ? AddOffset(scan0, checked((long)(height - 1) * sourceStride))
                : scan0;
            return new CurrentProcessMemoryReader(address, length);
        }

        internal void CopyTo(
            long sourceOffset,
            byte[] destination,
            int destinationOffset,
            int count)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (sourceOffset < 0 || sourceOffset > _length)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            }

            if (destinationOffset < 0 || destinationOffset > destination.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(destinationOffset));
            }

            if (count < 0
                || count > destination.Length - destinationOffset
                || count > _length - sourceOffset)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (count == 0)
            {
                return;
            }

            var source = GetAddress(sourceOffset, count);
            var pin = GCHandle.Alloc(destination, GCHandleType.Pinned);
            try
            {
                var target = IntPtr.Add(pin.AddrOfPinnedObject(), destinationOffset);
                UIntPtr bytesRead;
                var succeeded = ReadProcessMemory(
                    CurrentProcessHandle,
                    source,
                    target,
                    new UIntPtr((uint)count),
                    out bytesRead);
                var nativeError = succeeded ? 0 : Marshal.GetLastWin32Error();
                var actualCount = bytesRead.ToUInt64();
                if (!succeeded || actualCount != (ulong)count)
                {
                    throw new IOException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Debugger image memory is no longer readable. Requested {0} byte(s), read {1} byte(s). Win32 error {2}. Pause at a valid breakpoint and open the visualizer again.",
                            count,
                            actualCount,
                            nativeError));
                }
            }
            finally
            {
                pin.Free();
            }
        }

        internal byte ReadByte(long sourceOffset)
        {
            if (sourceOffset < 0 || sourceOffset >= _length)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            }

            if (_pageCacheOffset < 0
                || sourceOffset < _pageCacheOffset
                || sourceOffset - _pageCacheOffset >= _pageCacheCount)
            {
                FillPageCache(sourceOffset);
            }

            return _pageCache[checked((int)(sourceOffset - _pageCacheOffset))];
        }

        private void FillPageCache(long sourceOffset)
        {
            var pageSize = _pageCache.Length;
            var address = GetAddress(sourceOffset, 1);
            var pageOffset = GetPageOffset(address, pageSize);
            var available = Math.Min(_length - sourceOffset, pageSize - pageOffset);
            var count = checked((int)Math.Min(pageSize, available));

            CopyTo(sourceOffset, _pageCache, 0, count);
            _pageCacheOffset = sourceOffset;
            _pageCacheCount = count;
        }

        private IntPtr GetAddress(long sourceOffset, int count)
        {
            var lastOffset = count == 0
                ? sourceOffset
                : checked(sourceOffset + count - 1L);
            var startAddress = AddOffset(_address, sourceOffset);
            AddOffset(_address, lastOffset);
            return startAddress;
        }

        private static IntPtr AddOffset(IntPtr address, long offset)
        {
            if (IntPtr.Size == 4)
            {
                var baseAddress = unchecked((uint)address.ToInt32());
                ulong result;
                if (offset >= 0)
                {
                    result = checked((ulong)baseAddress + (ulong)offset);
                    if (result > uint.MaxValue)
                    {
                        throw new OverflowException("Image memory address exceeds the current process address range.");
                    }
                }
                else
                {
                    var magnitude = GetNegativeMagnitude(offset);
                    if (magnitude > baseAddress)
                    {
                        throw new OverflowException("Image memory address exceeds the current process address range.");
                    }

                    result = (ulong)baseAddress - magnitude;
                }

                return new IntPtr(unchecked((int)(uint)result));
            }

            var baseAddress64 = unchecked((ulong)address.ToInt64());
            ulong result64;
            if (offset >= 0)
            {
                result64 = checked(baseAddress64 + (ulong)offset);
            }
            else
            {
                var magnitude = GetNegativeMagnitude(offset);
                if (magnitude > baseAddress64)
                {
                    throw new OverflowException("Image memory address exceeds the current process address range.");
                }

                result64 = baseAddress64 - magnitude;
            }

            return new IntPtr(unchecked((long)result64));
        }

        private static ulong GetNegativeMagnitude(long value)
        {
            return value == long.MinValue
                ? 1UL << 63
                : (ulong)(-value);
        }

        private static int GetPageOffset(IntPtr address, int pageSize)
        {
            return IntPtr.Size == 4
                ? (int)(unchecked((uint)address.ToInt32()) % (uint)pageSize)
                : (int)(unchecked((ulong)address.ToInt64()) % (ulong)pageSize);
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadProcessMemory(
            IntPtr processHandle,
            IntPtr baseAddress,
            IntPtr buffer,
            UIntPtr size,
            out UIntPtr numberOfBytesRead);
    }
}
