using System;
using System.Globalization;

namespace RawBufferVisualizer.VisualStudio
{
    public enum AutomaticImageCollectionKind
    {
        None = 0,
        OneDimensionalArray,
        GenericList
    }

    public sealed class AutomaticImageCollectionDescriptor
    {
        public AutomaticImageCollectionKind Kind { get; set; }
        public string ElementTypeName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Defines the bounded, side-effect-aware collection shapes that Automatic Inspector may expand.
    /// Arbitrary IEnumerable values and unknown element types intentionally remain excluded.
    /// </summary>
    public static class AutomaticImageCollectionPolicy
    {
        public const int MaximumItemsPerCollection = 128;
        public const int MaximumItemsPerScan = 128;
        public const int MaximumCollectionRootsPerScan = 8;
        public const int MaximumItemsPerBatch = 8;
        public const int BatchSoftTimeBudgetMilliseconds = 2000;

        private const string OpenCvSharpMatTypeName = "OpenCvSharp.Mat";
        private const string EmguCvMatTypeName = "Emgu.CV.Mat";

        public static bool TryDescribe(
            string runtimeTypeName,
            out AutomaticImageCollectionDescriptor descriptor)
        {
            descriptor = new AutomaticImageCollectionDescriptor();
            if (string.IsNullOrWhiteSpace(runtimeTypeName))
            {
                return false;
            }

            var normalized = StripTopLevelAssemblySuffix(
                runtimeTypeName.Trim().Replace("global::", string.Empty));
            string elementTypeName;
            if (TryGetOneDimensionalArrayElement(normalized, out elementTypeName))
            {
                descriptor.Kind = AutomaticImageCollectionKind.OneDimensionalArray;
                descriptor.ElementTypeName = elementTypeName;
                return true;
            }

            if (TryGetGenericListElement(normalized, out elementTypeName))
            {
                descriptor.Kind = AutomaticImageCollectionKind.GenericList;
                descriptor.ElementTypeName = elementTypeName;
                return true;
            }

            return false;
        }

        public static int GetScheduledItemCount(int totalCount, int remainingScanCapacity)
        {
            if (totalCount <= 0 || remainingScanCapacity <= 0)
            {
                return 0;
            }

            return Math.Min(
                totalCount,
                Math.Min(MaximumItemsPerCollection, remainingScanCapacity));
        }

        public static int GetBatchItemCount(int remainingItemCount)
        {
            return remainingItemCount <= 0
                ? 0
                : Math.Min(remainingItemCount, MaximumItemsPerBatch);
        }

        public static string CreateElementExpression(string rootExpression, int index)
        {
            if (string.IsNullOrWhiteSpace(rootExpression))
            {
                throw new ArgumentException("A collection root expression is required.", nameof(rootExpression));
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return rootExpression.Trim()
                + "["
                + index.ToString(CultureInfo.InvariantCulture)
                + "]";
        }

        private static bool TryGetOneDimensionalArrayElement(
            string typeName,
            out string elementTypeName)
        {
            elementTypeName = string.Empty;
            if (!typeName.EndsWith("[]", StringComparison.Ordinal)
                || typeName.EndsWith("[][]", StringComparison.Ordinal)
                || typeName.IndexOf("[,", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            return TryNormalizeSupportedElementType(
                typeName.Substring(0, typeName.Length - 2),
                out elementTypeName);
        }

        private static bool TryGetGenericListElement(
            string typeName,
            out string elementTypeName)
        {
            elementTypeName = string.Empty;
            string innerTypeName;
            if (TryExtractDelimitedTypeArgument(
                    typeName,
                    "System.Collections.Generic.List<",
                    ">",
                    out innerTypeName)
                || TryExtractDelimitedTypeArgument(typeName, "List<", ">", out innerTypeName)
                || TryExtractDelimitedTypeArgument(
                    typeName,
                    "System.Collections.Generic.List(Of ",
                    ")",
                    out innerTypeName)
                || TryExtractDelimitedTypeArgument(typeName, "List(Of ", ")", out innerTypeName)
                || TryExtractDelimitedTypeArgument(
                    typeName,
                    "System.Collections.Generic.List`1[",
                    "]",
                    out innerTypeName))
            {
                return TryNormalizeSupportedElementType(innerTypeName, out elementTypeName);
            }

            return false;
        }

        private static bool TryExtractDelimitedTypeArgument(
            string typeName,
            string prefix,
            string suffix,
            out string innerTypeName)
        {
            innerTypeName = string.Empty;
            if (!typeName.StartsWith(prefix, StringComparison.Ordinal)
                || !typeName.EndsWith(suffix, StringComparison.Ordinal)
                || typeName.Length <= prefix.Length + suffix.Length)
            {
                return false;
            }

            innerTypeName = typeName.Substring(
                prefix.Length,
                typeName.Length - prefix.Length - suffix.Length);
            return true;
        }

        private static bool TryNormalizeSupportedElementType(
            string typeName,
            out string normalizedTypeName)
        {
            normalizedTypeName = string.Empty;
            var candidate = typeName.Trim();
            while (candidate.Length >= 2
                   && candidate[0] == '['
                   && candidate[candidate.Length - 1] == ']')
            {
                candidate = candidate.Substring(1, candidate.Length - 2).Trim();
            }

            if (string.Equals(candidate, OpenCvSharpMatTypeName, StringComparison.OrdinalIgnoreCase)
                || candidate.StartsWith(
                    OpenCvSharpMatTypeName + ",",
                    StringComparison.OrdinalIgnoreCase))
            {
                normalizedTypeName = OpenCvSharpMatTypeName;
                return true;
            }

            if (string.Equals(candidate, EmguCvMatTypeName, StringComparison.OrdinalIgnoreCase)
                || candidate.StartsWith(
                    EmguCvMatTypeName + ",",
                    StringComparison.OrdinalIgnoreCase))
            {
                normalizedTypeName = EmguCvMatTypeName;
                return true;
            }

            return false;
        }

        private static string StripTopLevelAssemblySuffix(string typeName)
        {
            var angleDepth = 0;
            var squareDepth = 0;
            for (var index = 0; index < typeName.Length; index++)
            {
                switch (typeName[index])
                {
                    case '<':
                        angleDepth++;
                        break;
                    case '>':
                        angleDepth = Math.Max(0, angleDepth - 1);
                        break;
                    case '[':
                        squareDepth++;
                        break;
                    case ']':
                        squareDepth = Math.Max(0, squareDepth - 1);
                        break;
                    case ',':
                        if (angleDepth == 0 && squareDepth == 0)
                        {
                            return typeName.Substring(0, index).Trim();
                        }

                        break;
                }
            }

            return typeName;
        }
    }
}
