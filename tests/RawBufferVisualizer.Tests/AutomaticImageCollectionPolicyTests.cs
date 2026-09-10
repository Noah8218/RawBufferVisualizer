using System;
using RawBufferVisualizer.VisualStudio;

namespace RawBufferVisualizer.Tests
{
    internal static class AutomaticImageCollectionPolicyTests
    {
        public static void RunAll()
        {
            RecognizesOnlySupportedOneDimensionalArrays();
            RecognizesSupportedListTypeSpellings();
            RejectsBroadOrUnsafeCollectionShapes();
            SupportsLargeDiscoveryAndBoundedBatches();
            BuildsStableElementExpressions();
        }

        private static void RecognizesOnlySupportedOneDimensionalArrays()
        {
            AssertDescriptor(
                "OpenCvSharp.Mat[]",
                AutomaticImageCollectionKind.OneDimensionalArray,
                "OpenCvSharp.Mat");
            AssertDescriptor(
                "Emgu.CV.Mat[], Emgu.CV",
                AutomaticImageCollectionKind.OneDimensionalArray,
                "Emgu.CV.Mat");

            AssertNotSupported("System.Drawing.Bitmap[]");
            AssertNotSupported("OpenCvSharp.Mat[,]");
            AssertNotSupported("OpenCvSharp.Mat[][]");
        }

        private static void RecognizesSupportedListTypeSpellings()
        {
            AssertDescriptor(
                "System.Collections.Generic.List<OpenCvSharp.Mat>",
                AutomaticImageCollectionKind.GenericList,
                "OpenCvSharp.Mat");
            AssertDescriptor(
                "List<Emgu.CV.Mat>",
                AutomaticImageCollectionKind.GenericList,
                "Emgu.CV.Mat");
            AssertDescriptor(
                "System.Collections.Generic.List(Of OpenCvSharp.Mat)",
                AutomaticImageCollectionKind.GenericList,
                "OpenCvSharp.Mat");
            AssertDescriptor(
                "System.Collections.Generic.List`1[[Emgu.CV.Mat, Emgu.CV]]",
                AutomaticImageCollectionKind.GenericList,
                "Emgu.CV.Mat");
        }

        private static void RejectsBroadOrUnsafeCollectionShapes()
        {
            AssertNotSupported("System.Collections.Generic.List<System.Drawing.Bitmap>");
            AssertNotSupported("System.Collections.Generic.List<System.Object>");
            AssertNotSupported("System.Collections.Generic.Dictionary<System.String, OpenCvSharp.Mat>");
            AssertNotSupported("System.Collections.Generic.IEnumerable<OpenCvSharp.Mat>");
            AssertNotSupported("Vendor.Camera.MatList");
        }

        private static void SupportsLargeDiscoveryAndBoundedBatches()
        {
            Assert(
                AutomaticImageCollectionPolicy.GetScheduledItemCount(5, 128) == 5,
                "A small collection should be inspected in full.");
            Assert(
                AutomaticImageCollectionPolicy.GetScheduledItemCount(50, 128) == 50,
                "A 50-image collection should be discoverable before UI batching.");
            Assert(
                AutomaticImageCollectionPolicy.GetScheduledItemCount(200, 128)
                    == AutomaticImageCollectionPolicy.MaximumItemsPerScan,
                "The per-scan discovery limit was not enforced.");
            Assert(
                AutomaticImageCollectionPolicy.GetScheduledItemCount(50, 3) == 3,
                "The per-scan remaining capacity was not enforced.");
            Assert(
                AutomaticImageCollectionPolicy.GetScheduledItemCount(8, 0) == 0,
                "An exhausted scan budget should schedule no collection items.");
            Assert(
                AutomaticImageCollectionPolicy.GetBatchItemCount(50)
                    == AutomaticImageCollectionPolicy.MaximumItemsPerBatch,
                "The initial UI batch should remain bounded to eight items.");
            Assert(
                AutomaticImageCollectionPolicy.GetBatchItemCount(5) == 5,
                "A small remaining batch should be loaded in full.");
            Assert(
                AutomaticImageCollectionPolicy.GetBatchItemCount(0) == 0,
                "An empty batch should schedule no UI work.");
        }

        private static void BuildsStableElementExpressions()
        {
            Assert(
                AutomaticImageCollectionPolicy.CreateElementExpression("frames", 3) == "frames[3]",
                "A simple collection element expression was not stable.");
            Assert(
                AutomaticImageCollectionPolicy.CreateElementExpression("this.frames", 0) == "this.frames[0]",
                "A member collection element expression was not stable.");
        }

        private static void AssertDescriptor(
            string typeName,
            AutomaticImageCollectionKind expectedKind,
            string expectedElementTypeName)
        {
            AutomaticImageCollectionDescriptor descriptor;
            Assert(
                AutomaticImageCollectionPolicy.TryDescribe(typeName, out descriptor),
                "Expected a supported automatic collection type: " + typeName);
            Assert(descriptor.Kind == expectedKind, "Unexpected collection kind for " + typeName);
            Assert(
                string.Equals(
                    descriptor.ElementTypeName,
                    expectedElementTypeName,
                    StringComparison.Ordinal),
                "Unexpected element type for " + typeName);
        }

        private static void AssertNotSupported(string typeName)
        {
            AutomaticImageCollectionDescriptor descriptor;
            Assert(
                !AutomaticImageCollectionPolicy.TryDescribe(typeName, out descriptor),
                "Unexpected automatic collection support for " + typeName);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
