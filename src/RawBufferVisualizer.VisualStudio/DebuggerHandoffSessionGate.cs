using System.Threading;

namespace RawBufferVisualizer.VisualStudio
{
    internal sealed class DebuggerHandoffSessionGate
    {
        private long _generation;
        private int _acceptingHandoffs;

        public void EnterBreakMode()
        {
            Volatile.Write(ref _acceptingHandoffs, 1);
        }

        public void EnterRunMode()
        {
            Volatile.Write(ref _acceptingHandoffs, 0);
            Interlocked.Increment(ref _generation);
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
