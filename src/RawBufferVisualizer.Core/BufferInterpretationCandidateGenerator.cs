using System;
using System.Collections.Generic;
using System.Globalization;

namespace RawBufferVisualizer.Core
{
    public static class BufferInterpretationCandidateGenerator
    {
        public const int MaxPoolSize = 40;
        private const int MaxExpandedPoolSize = 80;
        private const long MaxDimension = 4194304;
        private const int MaxAspectRatio = 256;

        private static readonly int[] Alignments = { 4, 8, 16, 32, 64, 128, 256 };
        private static readonly int[] CommonSensorWidths = { 320, 640, 1280, 1920, 2048, 2448, 2592, 4096, 8192 };
        private static readonly int[] Mono16ValidBitsOptions = { 10, 12, 14, 16 };

        private struct FormatFamily
        {
            public RawPixelFormat PixelFormat;
            public int BitsPerPixel;
            public int ValidBits;
        }

        private static readonly FormatFamily[] Families =
        {
            new FormatFamily { PixelFormat = RawPixelFormat.Mono8, BitsPerPixel = 8, ValidBits = 8 },
            new FormatFamily { PixelFormat = RawPixelFormat.Mono16, BitsPerPixel = 16, ValidBits = 16 },
            new FormatFamily { PixelFormat = RawPixelFormat.RGB24, BitsPerPixel = 24, ValidBits = 8 },
            new FormatFamily { PixelFormat = RawPixelFormat.BGR24, BitsPerPixel = 24, ValidBits = 8 },
            new FormatFamily { PixelFormat = RawPixelFormat.BGRA32, BitsPerPixel = 32, ValidBits = 8 },
            new FormatFamily { PixelFormat = RawPixelFormat.Float32, BitsPerPixel = 32, ValidBits = 32 },
            new FormatFamily { PixelFormat = RawPixelFormat.Int32, BitsPerPixel = 32, ValidBits = 32 },
            new FormatFamily { PixelFormat = RawPixelFormat.Mono10PackedLsb, BitsPerPixel = 10, ValidBits = 10 },
            new FormatFamily { PixelFormat = RawPixelFormat.Mono12PackedLsb, BitsPerPixel = 12, ValidBits = 12 }
        };

        public static bool IsCommonSensorWidth(int width)
        {
            for (var i = 0; i < CommonSensorWidths.Length; i++)
            {
                if (CommonSensorWidths[i] == width)
                {
                    return true;
                }
            }

            return false;
        }

        public static IReadOnlyList<BufferInterpretationDraft> Generate(long bufferLength, RawImageDescriptor? hintDescriptor)
        {
            var drafts = new List<BufferInterpretationDraft>();
            if (bufferLength <= 0)
            {
                return drafts;
            }

            for (var i = 0; i < Families.Length; i++)
            {
                var family = Families[i];
                var tightWidths = AddTightFits(drafts, bufferLength, family);
                AddAlignedFits(drafts, bufferLength, family, tightWidths, hintDescriptor);
            }

            var hintSeeds = new List<BufferInterpretationDraft>();
            AddHintSeeds(hintSeeds, bufferLength, hintDescriptor);

            var filtered = new List<BufferInterpretationDraft>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < hintSeeds.Count; i++)
            {
                var descriptor = hintSeeds[i].Descriptor;
                if (RawBufferDiagnostics.HasErrors(RawBufferDiagnostics.AnalyzeLength(bufferLength, descriptor)))
                {
                    continue;
                }

                if (!seen.Add(CreateKey(descriptor)))
                {
                    continue;
                }

                filtered.Add(hintSeeds[i]);
            }

            var generated = new List<BufferInterpretationDraft>();
            for (var i = 0; i < drafts.Count; i++)
            {
                var descriptor = drafts[i].Descriptor;
                if (RawBufferDiagnostics.HasErrors(RawBufferDiagnostics.AnalyzeLength(bufferLength, descriptor)))
                {
                    continue;
                }

                if (!seen.Add(CreateKey(descriptor)))
                {
                    continue;
                }

                generated.Add(drafts[i]);
            }

            generated.Sort(CompareDrafts);
            for (var i = 0; i < generated.Count && filtered.Count < MaxPoolSize; i++)
            {
                filtered.Add(generated[i]);
            }

            return ExpandMono16Variants(filtered);
        }

        private static List<int> AddTightFits(List<BufferInterpretationDraft> drafts, long bufferLength, FormatFamily family)
        {
            var widths = new List<int>();
            var totalBits = bufferLength * 8;
            if (totalBits % family.BitsPerPixel != 0)
            {
                return widths;
            }

            var totalPixels = totalBits / family.BitsPerPixel;
            for (long width = 2; width * width <= totalPixels; width++)
            {
                if (totalPixels % width != 0)
                {
                    continue;
                }

                var height = totalPixels / width;
                AddTightPair(drafts, widths, bufferLength, family, width, height);
                if (height != width)
                {
                    AddTightPair(drafts, widths, bufferLength, family, height, width);
                }
            }

            return widths;
        }

        private static void AddTightPair(
            List<BufferInterpretationDraft> drafts,
            List<int> widths,
            long bufferLength,
            FormatFamily family,
            long width,
            long height)
        {
            if (width < 2 || height < 2 || width > MaxDimension || height > MaxDimension)
            {
                return;
            }

            if (Math.Max(width, height) > Math.Min(width, height) * MaxAspectRatio)
            {
                return;
            }

            var descriptor = CreateDescriptor((int)width, (int)height, family);
            descriptor.Stride = descriptor.GetMinimumStride();
            if (descriptor.Stride <= 0)
            {
                return;
            }

            drafts.Add(new BufferInterpretationDraft(descriptor, ComputeFit(descriptor, bufferLength)));
            widths.Add((int)width);
        }

        private static void AddAlignedFits(
            List<BufferInterpretationDraft> drafts,
            long bufferLength,
            FormatFamily family,
            List<int> tightWidths,
            RawImageDescriptor? hintDescriptor)
        {
            var widths = new List<int>();
            for (var i = 0; i < CommonSensorWidths.Length; i++)
            {
                AddWidth(widths, CommonSensorWidths[i]);
            }

            for (var i = 0; i < tightWidths.Count; i++)
            {
                AddWidth(widths, tightWidths[i]);
            }

            if (hintDescriptor != null && hintDescriptor.Width > 0)
            {
                AddWidth(widths, hintDescriptor.Width);
            }

            for (var i = 0; i < widths.Count; i++)
            {
                var width = widths[i];
                var probe = CreateDescriptor(width, 1, family);
                var minimumStride = probe.GetMinimumStride();
                if (minimumStride <= 0)
                {
                    continue;
                }

                for (var a = 0; a < Alignments.Length; a++)
                {
                    var strideLong = AlignUp(minimumStride, Alignments[a]);
                    if (strideLong <= 0 || strideLong > int.MaxValue)
                    {
                        continue;
                    }

                    var stride = (int)strideLong;
                    if (bufferLength % stride == 0)
                    {
                        var height = bufferLength / stride;
                        if (height >= 1 && height <= MaxDimension)
                        {
                            var descriptor = CreateDescriptor(width, (int)height, family);
                            descriptor.Stride = stride;
                            drafts.Add(new BufferInterpretationDraft(descriptor, BufferInterpretationFitKind.Exact));
                        }
                    }
                    else if (bufferLength > minimumStride && (bufferLength - minimumStride) % stride == 0)
                    {
                        var height = ((bufferLength - minimumStride) / stride) + 1;
                        if (height >= 1 && height <= MaxDimension)
                        {
                            var descriptor = CreateDescriptor(width, (int)height, family);
                            descriptor.Stride = stride;
                            drafts.Add(new BufferInterpretationDraft(descriptor, BufferInterpretationFitKind.TrailingRow));
                        }
                    }
                }
            }
        }

        private static void AddHintSeeds(List<BufferInterpretationDraft> drafts, long bufferLength, RawImageDescriptor? hintDescriptor)
        {
            if (hintDescriptor == null
                || hintDescriptor.Width <= 0
                || hintDescriptor.Height <= 0
                || hintDescriptor.Stride <= 0)
            {
                return;
            }

            drafts.Add(new BufferInterpretationDraft(hintDescriptor.Clone(), ComputeFit(hintDescriptor, bufferLength)));
            for (var i = 0; i < Families.Length; i++)
            {
                var family = Families[i];
                if (family.PixelFormat == hintDescriptor.PixelFormat)
                {
                    continue;
                }

                var descriptor = hintDescriptor.Clone();
                descriptor.PixelFormat = family.PixelFormat;
                descriptor.ValidBits = family.ValidBits;
                descriptor.ByteOrder = RawByteOrder.LittleEndian;
                var minimumStride = descriptor.GetMinimumStride();
                if (minimumStride <= 0)
                {
                    continue;
                }

                if (descriptor.Stride < minimumStride)
                {
                    var aligned = AlignUp(minimumStride, 4);
                    if (aligned > int.MaxValue)
                    {
                        continue;
                    }

                    descriptor.Stride = (int)aligned;
                }

                drafts.Add(new BufferInterpretationDraft(descriptor, ComputeFit(descriptor, bufferLength)));
            }
        }

        private static IReadOnlyList<BufferInterpretationDraft> ExpandMono16Variants(List<BufferInterpretationDraft> drafts)
        {
            var expanded = new List<BufferInterpretationDraft>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < drafts.Count; i++)
            {
                var draft = drafts[i];
                if (draft.Descriptor.PixelFormat != RawPixelFormat.Mono16)
                {
                    if (seen.Add(CreateKey(draft.Descriptor)))
                    {
                        expanded.Add(draft);
                    }

                    continue;
                }

                for (var order = 0; order < 2; order++)
                {
                    for (var v = 0; v < Mono16ValidBitsOptions.Length; v++)
                    {
                        var descriptor = draft.Descriptor.Clone();
                        descriptor.ByteOrder = order == 0 ? RawByteOrder.LittleEndian : RawByteOrder.BigEndian;
                        descriptor.ValidBits = Mono16ValidBitsOptions[v];
                        if (!seen.Add(CreateKey(descriptor)))
                        {
                            continue;
                        }

                        expanded.Add(new BufferInterpretationDraft(descriptor, draft.FitKind));
                        if (expanded.Count >= MaxExpandedPoolSize)
                        {
                            return expanded;
                        }
                    }
                }
            }

            return expanded;
        }

        private static BufferInterpretationFitKind ComputeFit(RawImageDescriptor descriptor, long bufferLength)
        {
            var minimumStride = descriptor.GetMinimumStride();
            if (descriptor.Stride <= 0 || minimumStride <= 0 || descriptor.Height <= 0)
            {
                return BufferInterpretationFitKind.None;
            }

            if ((long)descriptor.Stride * descriptor.Height == bufferLength)
            {
                return BufferInterpretationFitKind.Exact;
            }

            if (((long)descriptor.Stride * (descriptor.Height - 1)) + minimumStride == bufferLength)
            {
                return BufferInterpretationFitKind.TrailingRow;
            }

            return BufferInterpretationFitKind.None;
        }

        private static int CompareDrafts(BufferInterpretationDraft left, BufferInterpretationDraft right)
        {
            var result = FitRank(left.FitKind).CompareTo(FitRank(right.FitKind));
            if (result != 0)
            {
                return result;
            }

            result = AspectScore(left.Descriptor).CompareTo(AspectScore(right.Descriptor));
            if (result != 0)
            {
                return result;
            }

            result = right.Descriptor.Stride.CompareTo(left.Descriptor.Stride);
            if (result != 0)
            {
                return result;
            }

            return ((int)left.Descriptor.PixelFormat).CompareTo((int)right.Descriptor.PixelFormat);
        }

        private static int FitRank(BufferInterpretationFitKind fitKind)
        {
            switch (fitKind)
            {
                case BufferInterpretationFitKind.Exact:
                    return 0;
                case BufferInterpretationFitKind.TrailingRow:
                    return 1;
                default:
                    return 2;
            }
        }

        private static double AspectScore(RawImageDescriptor descriptor)
        {
            if (descriptor.Width <= 0 || descriptor.Height <= 0)
            {
                return double.MaxValue;
            }

            return Math.Abs(Math.Log((double)descriptor.Width / descriptor.Height));
        }

        private static long AlignUp(int value, int alignment)
        {
            return ((long)value + alignment - 1) / alignment * alignment;
        }

        private static void AddWidth(List<int> widths, int width)
        {
            if (width > 0 && !widths.Contains(width))
            {
                widths.Add(width);
            }
        }

        private static RawImageDescriptor CreateDescriptor(int width, int height, FormatFamily family)
        {
            return new RawImageDescriptor
            {
                Width = width,
                Height = height,
                PixelFormat = family.PixelFormat,
                ValidBits = family.ValidBits,
                ByteOrder = RawByteOrder.LittleEndian
            };
        }

        private static string CreateKey(RawImageDescriptor descriptor)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|{3}|{4}|{5}",
                descriptor.Width,
                descriptor.Height,
                descriptor.Stride,
                descriptor.PixelFormat,
                descriptor.ByteOrder,
                descriptor.ValidBits);
        }
    }
}
