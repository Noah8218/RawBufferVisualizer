using System;
using System.Threading;

namespace RawBufferVisualizer.VisualStudio
{
    internal sealed class DebuggerHandoffSessionGate
    {
        private long _generation;
        private int _acceptingHandoffs;
        private string _sessionToken = string.Empty;

        public void EnterBreakMode()
        {
            if (Volatile.Read(ref _acceptingHandoffs) != 0) return;
            Volatile.Write(ref _sessionToken, Guid.NewGuid().ToString("N"));
            Volatile.Write(ref _acceptingHandoffs, 1);
        }

        public void EnterRunMode()
        {
            Volatile.Write(ref _acceptingHandoffs, 0);
            Interlocked.Increment(ref _generation);
            Volatile.Write(ref _sessionToken, string.Empty);
        }

        public string CaptureToken()
        {
            var generation = Capture();
            var token = Volatile.Read(ref _sessionToken);
            return IsCurrent(generation) ? token : string.Empty;
        }

        public bool IsCurrentToken(string token)
        {
            return !string.IsNullOrEmpty(token) && string.Equals(CaptureToken(), token, StringComparison.Ordinal);
        }

        public long Capture()
        {
            var generation = Interlocked.Read(ref _generation);
            if (Volatile.Read(ref _acceptingHandoffs) == 0
                || Interlocked.Read(ref _generation) != generation)
            {
                return -1;
            }

            return generation;
        }

        public bool IsCurrent(long generation)
        {
            return generation >= 0
                && Volatile.Read(ref _acceptingHandoffs) != 0
                && Interlocked.Read(ref _generation) == generation;
        }
    }
}
