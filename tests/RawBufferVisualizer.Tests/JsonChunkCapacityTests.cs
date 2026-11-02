using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.Tests
{
    internal static class JsonChunkCapacityTests
    {
        private static readonly MethodInfo ReserveMethod = typeof(VisualizerChunkedTransfer)
            .GetMethod("ReserveJsonChunkCapacity", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Production capacity helper was not found.");

        public static void RunAll()
        {
            var chunk = new VisualizerSnapshotChunk
            {
                Buffer = new byte[786432], TotalLength = 786432, IsLastChunk = true
            };
            var wire = JsonSerializer.SerializeToUtf8Bytes(chunk);
            Require(wire.Length == 1048640, "The independent JSON writer must produce the expected chunk envelope.");

            Check("empty stream preserves payload and avoids doubled capacity", () =>
            {
                using var stream = new MemoryStream();
                Reserve(stream, chunk.Buffer.Length);
                Require(stream.Length == 0 && stream.Position == 0 && stream.Capacity >= wire.Length
                    && stream.Capacity < 2 * 1024 * 1024, "Reservation must not publish bytes or use the old doubled capacity.");
                stream.Write(wire, 0, wire.Length);
                Require(stream.ToArray().SequenceEqual(wire), "Reservation must leave every JSON byte unchanged.");
            });
            Check("fixed capacity that fits the actual JSON still works", () =>
            {
                using var stream = new MemoryStream(new byte[wire.Length], writable: true);
                stream.SetLength(0);
                Reserve(stream, chunk.Buffer.Length);
                Require(stream.Capacity == wire.Length && stream.Position == 0 && stream.Length == 0,
                    "A rejected capacity hint must preserve the fixed buffer.");
                stream.Write(wire, 0, wire.Length);
                Require(stream.ToArray().SequenceEqual(wire), "The original serializer can still fill the fixed buffer.");
            });
            Check("existing bytes and write position are preserved", () =>
            {
                using var stream = new MemoryStream();
                stream.Write(new byte[] { 11, 22, 33 }, 0, 3);
                stream.Position = 2;
                var capacity = stream.Capacity;
                Reserve(stream, chunk.Buffer.Length);
                Require(stream.Position == 2 && stream.Length == 3 && stream.Capacity == capacity
                    && stream.ToArray().SequenceEqual(new byte[] { 11, 22, 33 }), "Non-empty streams must be untouched.");
            });
            Check("caller-owned larger capacity is retained", () =>
            {
                using var stream = new MemoryStream(2 * 1024 * 1024);
                Reserve(stream, chunk.Buffer.Length);
                Require(stream.Capacity == 2 * 1024 * 1024 && stream.Length == 0 && stream.Position == 0,
                    "The hint must not shrink or replace existing storage.");
            });
            Check("other, read-only and closed streams keep their original behavior", () =>
            {
                Reserve(Stream.Null, chunk.Buffer.Length);
                using var readOnly = new MemoryStream(new byte[3], writable: false);
                Reserve(readOnly, chunk.Buffer.Length);
                Require(readOnly.Length == 3 && readOnly.Position == 0, "Read-only streams must be untouched.");
                var closed = new MemoryStream();
                closed.Dispose();
                Reserve(closed, chunk.Buffer.Length);
            });
            Check("oversized hints do not overflow or allocate a huge buffer", () =>
            {
                using var stream = new MemoryStream();
                Reserve(stream, int.MaxValue);
                Require(stream.Capacity == 0 && stream.Position == 0 && stream.Length == 0,
                    "An unrepresentable capacity hint must leave serialization behavior unchanged.");
            });
        }

        private static void Reserve(Stream stream, int length) => ReserveMethod.Invoke(null, new object[] { stream, length });
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static void Check(string name, Action action)
        {
            action();
            Console.WriteLine("PASS JSON capacity: " + name);
        }
    }
}
