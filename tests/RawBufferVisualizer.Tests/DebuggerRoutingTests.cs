using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.VisualStudio.Debugger;

namespace RawBufferVisualizer.Tests
{
    internal static class DebuggerRoutingTests
    {
        internal static void RunAll()
        {
            AssertSource(typeof(RawBufferSnapshot), "RawBufferSnapshotVisualizerObjectSource");
            AssertSource(typeof(RawBufferView), "RawBufferViewVisualizerObjectSource");
            AssertSource(typeof(Bitmap), "BitmapVisualizerObjectSource");
            AssertSource(typeof(OpenCvSharp.Mat), "OpenCvSharpMatVisualizerObjectSource");
            // The existing runner's Emgu type is a transfer fixture in this test assembly.
            // Selection must reject that look-alike; the real vendor binary is covered by the IDE debuggee.
            AssertSource(typeof(Emgu.CV.Mat), null);
            foreach (var assembly in new[] { "Emgu.CV", "Emgu.CV.World", "Emgu.CV.World.NetStandard", "Emgu.CV.Platform.NetStandard" })
                Require(ImageVisualizerTypePolicy.GetObjectSource("Emgu.CV.Mat", assembly) == "EmguCvMatVisualizerObjectSource", "Supported Emgu assembly alias was lost.");
            AssertSource(typeof(int), null);
            AssertSource(typeof(string), null);
            AssertSource(typeof(object), null);
            AssertSource(typeof(List<int>), null);
            AssertSource(typeof(byte[]), null);

            AssertArgument(typeof(List<Bitmap>), 0);
            AssertArgument(typeof(Dictionary<string, Bitmap>), 1);
            AssertArgument(typeof(Dictionary<Bitmap, int>), 1);
            AssertArgument(typeof(ConcurrentDictionary<string, RawBufferSnapshot>), 1);
            AssertArgument(typeof(IEnumerable<Bitmap>), -1);
            AssertArgument(typeof(Lazy<Bitmap>), -1);
            AssertArgument(typeof(ArrayList), -1);

            Require(ImageVisualizerTypePolicy.GetObjectSource("OpenCvSharp.Mat", "Application") == null, "A matching type name in an unrelated assembly is not an image contract.");
            Require(ImageVisualizerTypePolicy.GetObjectSource("Application.Image", "Application") == null, "An image-like name must not capture the original visualizer.");
            Require(ImageVisualizerTypePolicy.GetObjectSource(null, null) == null, "Unavailable metadata must retain the original provider.");
            Require(ImageVisualizerTypePolicy.GetCollectionValueArgument("System.Collections.Generic.List`1", "Application") == -1, "Do not classify application containers as framework collections.");
            Require(ImageVisualizerTypePolicy.IsOwnedObjectSource("RawBufferVisualizer.VisualStudio.ObjectSource, Version=1.0.0.0, Culture=neutral"), "Owned source identity must include qualified assembly names.");
            Require(ImageVisualizerTypePolicy.IsOwnedObjectSource("RawBufferVisualizer.VisualStudio.ObjectSource"), "Owned simple assembly identity was lost.");
            Require(!ImageVisualizerTypePolicy.IsOwnedObjectSource("RawBufferVisualizer.VisualStudio.ObjectSource.Other"), "Do not remove another extension by assembly-name prefix.");
            Require(!ImageVisualizerTypePolicy.IsOwnedObjectSource(null), "Unidentified providers must be preserved.");
            Require(!ImageVisualizerTypePolicy.IsOwnedObjectSource("Microsoft.VisualStudio.DebuggerVisualizers"), "The built-in viewer must be preserved.");
            Console.WriteLine("Debugger routing: image contracts, unrelated types, dictionary value direction and assembly boundaries passed.");
            Console.WriteLine("Boundary: policy checks do not prove Concord dispatch, mixed collection inspection or IDE default selection.");
        }

        private static void AssertSource(Type type, string? expected)
        {
            Require(ImageVisualizerTypePolicy.GetObjectSource(type.FullName, type.Assembly.GetName().Name) == expected, "Unexpected route for " + type);
        }

        private static void AssertArgument(Type type, int expected)
        {
            var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            Require(ImageVisualizerTypePolicy.GetCollectionValueArgument(definition.FullName, definition.Assembly.GetName().Name) == expected, "Unexpected collection value position for " + type);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
