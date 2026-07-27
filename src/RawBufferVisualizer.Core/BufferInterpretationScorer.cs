using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace RawBufferVisualizer.Core
{
    public static class BufferInterpretationScorer
    {
        public const int MaxSampledRows = 64;
        public const long MaxSampledBytes = 4L * 1024 * 1024;
        private const int MaxContinuitySegmentBytes = 4096;

        public static BufferInterpretationCandidate Score(
            RawImageSource source,
            BufferInterpretationDraft draft,
            CancellationToken cancellationToken)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (draft == null)
            {
                throw new ArgumentNullException("draft");
            }

            var reasons = new List<string>();
            var structuralScore = ScoreStructural(draft, reasons);
            int sampledRowCount;
            long sampledByteCount;
            var contentScore = ScoreContent(source, draft.Descriptor, cancellationToken, reasons, out sampledRowCount, out sampledByteCount);
            var total = structuralScore + contentScore;
            if (total < 0)
            {
                total = 0;
            }
            else if (total > 100)
            {
                total = 100;
            }

            return new BufferInterpretationCandidate(
                draft.Descriptor.Clone(),
                total,
                structuralScore,
                contentScore,
                sampledRowCount,
                sampledByteCount,
                reasons);
        }

        private static int ScoreStructural(BufferInterpretationDraft draft, List<string> reasons)
        {
            var descriptor = draft.Descriptor;
            var minimumStride = descriptor.GetMinimumStride();
            var score = 0;

            if (draft.FitKind == BufferInterpretationFitKind.Exact)
            {
                score += 15;
                reasons.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Exact length fit: stride {0:N0} x height {1:N0} matches the buffer length.",
                    descriptor.Stride,
                    descriptor.Height));
            }
            else if (draft.FitKind == BufferInterpretationFitKind.TrailingRow)
            {
                score += 10;
                reasons.Add("Trailing-row fit: stride x (height - 1) + minimum stride matches the buffer length.");
            }

            var alignment = GetStrideAlignment(descriptor.Stride);
            if (alignment >= 16)
            {
                score += 10;
                reasons.Add(string.Format(CultureInfo.InvariantCulture, "Stride {0:N0} is aligned to {1} bytes.", descriptor.Stride, alignment));
            }
            else if (alignment >= 4)
            {
                score += 5;
                reasons.Add(string.Format(CultureInfo.InvariantCulture, "Stride {0:N0} is aligned to {1} bytes.", descriptor.Stride, alignment));
            }

            if (IsPlausibleDimensions(descriptor.Width, descriptor.Height))
            {
                score += 5;
                reasons.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Dimensions {0}x{1} are within the plausible range.",
                    descriptor.Width,
                    descriptor.Height));
            }

            if (BufferInterpretationCandidateGenerator.IsCommonSensorWidth(descriptor.Width))
            {
                score += 5;
                reasons.Add(string.Format(CultureInfo.InvariantCulture, "Width {0} is a common sensor width.", descriptor.Width));
            }

            var padding = descriptor.Stride - minimumStride;
            if (padding > 0 && padding <= 512)
            {
                score += 5;
                reasons.Add(string.Format(CultureInfo.InvariantCulture, "Padding per row is {0:N0} bytes.", padding));
            }

            return score;
        }

        private static int ScoreContent(
            RawImageSource source,
            RawImageDescriptor descriptor,
            CancellationToken cancellationToken,
            List<string> reasons,
            out int sampledRowCount,
            out long sampledByteCount)
        {
            sampledRowCount = 0;
            sampledByteCount = 0;
            var minimumStride = descriptor.GetMinimumStride();
            if (descriptor.Height < 1 || descriptor.Stride <= 0 || minimumStride <= 0)
            {
                reasons.Add("Content sampling is not possible for this descriptor; score is structural only.");
                return 0;
            }

            int pairStep;
            var rows = SelectSampleRows(descriptor.Height, out pairStep);
            if (rows.Length == 0)
            {
                reasons.Add("Content sampling is not possible for this descriptor; score is structural only.");
                return 0;
            }

            var bytesPerRow = (int)Math.Min(minimumStride, MaxSampledBytes / rows.Length);
            if (bytesPerRow < 2)
            {
                reasons.Add("Rows are too wide to sample within the byte cap; score is structural only.");
                return 0;
            }

            var samples = new List<byte[]>(rows.Length);
            for (var i = 0; i < rows.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = new byte[bytesPerRow];
                if (!source.TryReadRange((long)rows[i] * descriptor.Stride, row, 0, bytesPerRow))
                {
                    reasons.Add("Range reads are unavailable for this source; score is structural only.");
                    sampledRowCount = 0;
                    sampledByteCount = 0;
                    return 0;
                }

                samples.Add(row);
            }

            sampledRowCount = rows.Length;
            sampledByteCount = (long)rows.Length * bytesPerRow;

            var score = ScoreRowContinuity(samples, bytesPerRow, pairStep, reasons);
            if (descriptor.PixelFormat == RawPixelFormat.Mono16)
            {
                int maxValue;
                long overCount;
                long totalValues;
                double meanDeltaOwn;
                double meanDeltaOther;
                CollectMono16Stats(
                    samples,
                    bytesPerRow,
                    descriptor.ByteOrder,
                    descriptor.ValidBits,
                    out maxValue,
                    out overCount,
                    out totalValues,
                    out meanDeltaOwn,
                    out meanDeltaOther);
                score += ScoreEndianness(descriptor.ByteOrder, meanDeltaOwn, meanDeltaOther, reasons);
                score += ScoreValidBits(descriptor.ValidBits, maxValue, overCount, totalValues, reasons);
            }

            return score;
        }

        private static int[] SelectSampleRows(int height, out int pairStep)
        {
            if (height <= MaxSampledRows)
            {
                pairStep = 1;
                var rows = new int[height];
                for (var i = 0; i < rows.Length; i++)
                {
                    rows[i] = i;
                }

                return rows;
            }

            var pairCount = Math.Min(MaxSampledRows / 2, height - 1);
            pairStep = 2;
            var pairedRows = new int[pairCount * 2];
            for (var i = 0; i < pairCount; i++)
            {
                var y = (int)(((long)i * (height - 2)) / (pairCount - 1));
                pairedRows[i * 2] = y;
                pairedRows[(i * 2) + 1] = y + 1;
            }

            return pairedRows;
        }

        private static int ScoreRowContinuity(List<byte[]> samples, int bytesPerRow, int pairStep, List<string> reasons)
        {
            if (samples.Count < 2)
            {
                return 0;
            }

            var segmentLength = Math.Min(bytesPerRow, MaxContinuitySegmentBytes);
            var useLastSegment = bytesPerRow > segmentLength;
            double totalCorrelation = 0;
            var pairCount = 0;
            for (var i = 0; i + 1 < samples.Count; i += pairStep)
            {
                var correlation = Correlation(samples[i], 0, samples[i + 1], 0, segmentLength);
                if (useLastSegment)
                {
                    correlation = (correlation + Correlation(
                        samples[i],
                        bytesPerRow - segmentLength,
                        samples[i + 1],
                        bytesPerRow - segmentLength,
                        segmentLength)) / 2;
                }

                totalCorrelation += correlation;
                pairCount++;
            }

            if (pairCount == 0)
            {
                return 0;
            }

            var meanCorrelation = totalCorrelation / pairCount;
            var points = (int)Math.Round(30.0 * Math.Max(0.0, meanCorrelation), MidpointRounding.AwayFromZero);
            reasons.Add(string.Format(
                CultureInfo.InvariantCulture,
                "Row continuity correlation {0:0.00} across {1} sampled row pairs.",
                meanCorrelation,
                pairCount));
            return points;
        }

        private static void CollectMono16Stats(
            List<byte[]> samples,
            int bytesPerRow,
            RawByteOrder byteOrder,
            int validBits,
            out int maxValue,
            out long overCount,
            out long totalValues,
            out double meanDeltaOwn,
            out double meanDeltaOther)
        {
            var clampedValidBits = Math.Max(1, Math.Min(16, validBits));
            var limit = (1 << clampedValidBits) - 1;
            maxValue = 0;
            overCount = 0;
            totalValues = 0;
            double sumDeltaOwn = 0;
            double sumDeltaOther = 0;
            long deltaCount = 0;

            for (var row = 0; row < samples.Count; row++)
            {
                var data = samples[row];
                var valueCount = bytesPerRow / 2;
                var previousLittle = -1;
                var previousBig = -1;
                for (var i = 0; i < valueCount; i++)
                {
                    var low = data[i * 2];
                    var high = data[(i * 2) + 1];
                    var littleEndian = low | (high << 8);
                    var bigEndian = (low << 8) | high;
                    var value = byteOrder == RawByteOrder.LittleEndian ? littleEndian : bigEndian;
                    if (value > maxValue)
                    {
                        maxValue = value;
                    }

                    if (value > limit)
                    {
                        overCount++;
                    }

                    totalValues++;
                    if (previousLittle >= 0)
                    {
                        sumDeltaOwn += Math.Abs((byteOrder == RawByteOrder.LittleEndian ? littleEndian : bigEndian)
                            - (byteOrder == RawByteOrder.LittleEndian ? previousLittle : previousBig));
                        sumDeltaOther += Math.Abs((byteOrder == RawByteOrder.LittleEndian ? bigEndian : littleEndian)
                            - (byteOrder == RawByteOrder.LittleEndian ? previousBig : previousLittle));
                        deltaCount++;
                    }

                    previousLittle = littleEndian;
                    previousBig = bigEndian;
                }
            }

            meanDeltaOwn = deltaCount == 0 ? 0 : sumDeltaOwn / deltaCount;
            meanDeltaOther = deltaCount == 0 ? 0 : sumDeltaOther / deltaCount;
        }

        private static int ScoreEndianness(RawByteOrder byteOrder, double meanDeltaOwn, double meanDeltaOther, List<string> reasons)
        {
            var smoothnessOwn = 1.0 / (1.0 + meanDeltaOwn);
            var smoothnessOther = 1.0 / (1.0 + meanDeltaOther);
            var share = Math.Max(0.0, (smoothnessOwn - smoothnessOther) / (smoothnessOwn + smoothnessOther));
            var points = (int)Math.Round(15.0 * share, MidpointRounding.AwayFromZero);
            if (points > 0)
            {
                reasons.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} samples are smoother (mean neighbor delta {1:0.0} vs {2:0.0} for the opposite order).",
                    byteOrder,
                    meanDeltaOwn,
                    meanDeltaOther));
            }
            else
            {
                reasons.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Endianness is ambiguous for the sampled content (mean neighbor delta {0:0.0} vs {1:0.0}).",
                    meanDeltaOwn,
                    meanDeltaOther));
            }

            return points;
        }

        private static int ScoreValidBits(int validBits, int maxValue, long overCount, long totalValues, List<string> reasons)
        {
            var clampedValidBits = Math.Max(1, Math.Min(16, validBits));
            var usedBits = BitLength(maxValue);
            var overFraction = totalValues == 0 ? 0.0 : (double)overCount / totalValues;
            var unusedBits = 16 - Math.Min(16, usedBits);
            var fit = (1.0 - (Math.Abs(usedBits - clampedValidBits) / 16.0)) * (unusedBits / 16.0) * (1.0 - overFraction);
            var points = (int)Math.Round(15.0 * Math.Max(0.0, fit), MidpointRounding.AwayFromZero);
            reasons.Add(string.Format(
                CultureInfo.InvariantCulture,
                "Sampled values use {0} bits (maximum {1}); candidate declares {2} valid bits.",
                usedBits,
                maxValue,
                clampedValidBits));
            return points;
        }

        private static double Correlation(byte[] first, int firstOffset, byte[] second, int secondOffset, int count)
        {
            double sumFirst = 0;
            double sumSecond = 0;
            for (var i = 0; i < count; i++)
            {
                sumFirst += first[firstOffset + i];
                sumSecond += second[secondOffset + i];
            }

            var meanFirst = sumFirst / count;
            var meanSecond = sumSecond / count;
            double covariance = 0;
            double varianceFirst = 0;
            double varianceSecond = 0;
            for (var i = 0; i < count; i++)
            {
                var deltaFirst = first[firstOffset + i] - meanFirst;
                var deltaSecond = second[secondOffset + i] - meanSecond;
                covariance += deltaFirst * deltaSecond;
                varianceFirst += deltaFirst * deltaFirst;
                varianceSecond += deltaSecond * deltaSecond;
            }

            if (varianceFirst < 1e-9 || varianceSecond < 1e-9)
            {
                return varianceFirst < 1e-9 && varianceSecond < 1e-9 && meanFirst == meanSecond ? 1.0 : 0.0;
            }

            return covariance / Math.Sqrt(varianceFirst * varianceSecond);
        }

        private static int GetStrideAlignment(int stride)
        {
            if (stride <= 0)
            {
                return 0;
            }

            if (stride % 64 == 0)
            {
                return 64;
            }

            if (stride % 32 == 0)
            {
                return 32;
            }

            if (stride % 16 == 0)
            {
                return 16;
            }

            if (stride % 8 == 0)
            {
                return 8;
            }

            if (stride % 4 == 0)
            {
                return 4;
            }

            return 0;
        }

        private static bool IsPlausibleDimensions(int width, int height)
        {
            if (width < 16 || height < 16 || width > 65536 || height > 65536)
            {
                return false;
            }

            var larger = Math.Max(width, height);
            var smaller = Math.Min(width, height);
            return larger <= smaller * 16;
        }

        private static int BitLength(int value)
        {
            var bits = 0;
            var remaining = value;
            while (remaining > 0)
            {
                bits++;
                remaining >>= 1;
            }

            return bits;
        }
    }
}
