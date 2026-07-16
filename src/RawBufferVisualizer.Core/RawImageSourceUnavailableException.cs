using System;
using System.IO;

namespace RawBufferVisualizer.Core
{
    public sealed class RawImageSourceUnavailableException : IOException
    {
        public int NativeErrorCode { get; private set; }

        public RawImageSourceUnavailableException(string message, int nativeErrorCode)
            : base(message)
        {
            NativeErrorCode = nativeErrorCode;
        }
    }
}
