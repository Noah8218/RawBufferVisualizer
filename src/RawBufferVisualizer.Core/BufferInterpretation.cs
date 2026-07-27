using System;
using System.Collections.Generic;
using System.Threading;

namespace RawBufferVisualizer.Core
{
    public enum BufferInterpretationFitKind
    {
        None,
        Exact,
        TrailingRow
    }

    public sealed class BufferInterpretationDraft
    {
        public RawImageDescriptor Descriptor { get; private set; }
        public BufferInterpretationFitKind FitKind { get; private set; }

        public BufferInterpretationDraft(RawImageDescriptor descriptor, BufferInterpretationFitKind fitKind)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException("descriptor");
            }

            Descriptor = descriptor;
            FitKind = fitKind;
        }
    }

    public sealed class BufferInterpretationCandidate
    {
        private readonly List<string> _reasons;

        public RawImageDescriptor Descriptor { get; private set; }
        public int Score { get; private set; }
        public int StructuralScore { get; private set; }
        public int ContentScore { get; private set; }
        public int SampledRowCount { get; private set; }
        public long SampledByteCount { get; private set; }
        public bool IsAmbiguousWithGroup { get; private set; }

        public IReadOnlyList<string> Reasons
        {
            get { return _reasons; }
        }

        public BufferInterpretationCandidate(
            RawImageDescriptor descriptor,
            int score,
            int structuralScore,
            int contentScore,
            int sampledRowCount,
            long sampledByteCount,
            IEnumerable<string> reasons)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException("descriptor");
            }

            Descriptor = descriptor;
            Score = score;
            StructuralScore = structuralScore;
            ContentScore = contentScore;
            SampledRowCount = sampledRowCount;
            SampledByteCount = sampledByteCount;
            _reasons = reasons == null ? new List<string>() : new List<string>(reasons);
        }

        internal void MarkAmbiguousWithGroup(string reason)
        {
            IsAmbiguousWithGroup = true;
            if (!string.IsNullOrEmpty(reason) && !_reasons.Contains(reason))
            {
                _reasons.Add(reason);
            }
        }

        internal BufferInterpretationCandidate CloneForDescriptor(RawImageDescriptor descriptor)
        {
            return new BufferInterpretationCandidate(
                descriptor,
                Score,
                StructuralScore,
                ContentScore,
                SampledRowCount,
                SampledByteCount,
                _reasons);
        }
    }

    public sealed class BufferDiagnosisResult
    {
        public IReadOnlyList<BufferInterpretationCandidate> Candidates { get; private set; }
        public IReadOnlyList<RawDiagnostic> Notes { get; private set; }

        public BufferDiagnosisResult(
            IReadOnlyList<BufferInterpretationCandidate> candidates,
            IReadOnlyList<RawDiagnostic> notes)
        {
            Candidates = candidates ?? new List<BufferInterpretationCandidate>();
            Notes = notes ?? new List<RawDiagnostic>();
        }
    }

    public static class BufferDoctor
    {
        public const int MaxCandidates = 8;
        public const int BayerGroupMinimumScore = 50;

        public static BufferDiagnosisResult Diagnose(RawImageSource source, CancellationToken cancellationToken)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            var hint = source.Descriptor;
            var drafts = BufferInterpretationCandidateGenerator.Generate(source.Length, hint);
            var candidates = new List<BufferInterpretationCandidate>(drafts.Count);
            for (var i = 0; i < drafts.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                candidates.Add(BufferInterpretationScorer.Score(source, drafts[i], cancellationToken));
            }

            MarkColorAmbiguityGroups(candidates);
            AddBayerAmbiguityGroup(candidates, hint);
            candidates.Sort((left, right) => CompareCandidates(left, right, hint));
            if (candidates.Count > MaxCandidates)
            {
                candidates = candidates.GetRange(0, MaxCandidates);
            }

            return new BufferDiagnosisResult(candidates, source.Analyze());
        }

        private static int CompareCandidates(BufferInterpretationCandidate left, BufferInterpretationCandidate right, RawImageDescriptor hint)
        {
            var result = right.Score.CompareTo(left.Score);
            if (result != 0)
            {
                return result;
            }

            result = right.StructuralScore.CompareTo(left.StructuralScore);
            if (result != 0)
            {
                return result;
            }

            result = HintAffinity(right.Descriptor, hint).CompareTo(HintAffinity(left.Descriptor, hint));
            if (result != 0)
            {
                return result;
            }

            result = left.Descriptor.Width.CompareTo(right.Descriptor.Width);
            if (result != 0)
            {
                return result;
            }

            result = left.Descriptor.Height.CompareTo(right.Descriptor.Height);
            if (result != 0)
            {
                return result;
            }

            result = left.Descriptor.Stride.CompareTo(right.Descriptor.Stride);
            if (result != 0)
            {
                return result;
            }

            return ((int)left.Descriptor.PixelFormat).CompareTo((int)right.Descriptor.PixelFormat);
        }

        private static int HintAffinity(RawImageDescriptor descriptor, RawImageDescriptor hint)
        {
            if (hint == null)
            {
                return 0;
            }

            if (descriptor.PixelFormat == hint.PixelFormat)
            {
                return 2;
            }

            if (IsColorTriple(descriptor.PixelFormat) && IsColorTriple(hint.PixelFormat))
            {
                return 1;
            }

            return 0;
        }

        private static void MarkColorAmbiguityGroups(List<BufferInterpretationCandidate> candidates)
        {
            const string reason = "RGB24 and BGR24 cannot be distinguished from buffer content.";
            for (var i = 0; i < candidates.Count; i++)
            {
                var descriptor = candidates[i].Descriptor;
                if (!IsColorTriple(descriptor.PixelFormat))
                {
                    continue;
                }

                for (var j = i + 1; j < candidates.Count; j++)
                {
                    var other = candidates[j].Descriptor;
                    if (!IsColorTriple(other.PixelFormat)
                        || other.PixelFormat == descriptor.PixelFormat
                        || other.Width != descriptor.Width
                        || other.Height != descriptor.Height
                        || other.Stride != descriptor.Stride)
                    {
                        continue;
                    }

                    candidates[i].MarkAmbiguousWithGroup(reason);
                    candidates[j].MarkAmbiguousWithGroup(reason);
                }
            }
        }

        private static void AddBayerAmbiguityGroup(List<BufferInterpretationCandidate> candidates, RawImageDescriptor hint)
        {
            if (hint == null || !IsBayer(hint.PixelFormat))
            {
                return;
            }

            BufferInterpretationCandidate? bestSingleChannel = null;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.Descriptor.PixelFormat != RawPixelFormat.Mono8)
                {
                    continue;
                }

                if (bestSingleChannel == null || candidate.Score > bestSingleChannel.Score)
                {
                    bestSingleChannel = candidate;
                }
            }

            if (bestSingleChannel == null || bestSingleChannel.Score < BayerGroupMinimumScore)
            {
                return;
            }

            var bayerFormats = new[]
            {
                RawPixelFormat.BayerRGGB8,
                RawPixelFormat.BayerGRBG8,
                RawPixelFormat.BayerGBRG8,
                RawPixelFormat.BayerBGGR8
            };
            for (var i = 0; i < bayerFormats.Length; i++)
            {
                var descriptor = bestSingleChannel.Descriptor.Clone();
                descriptor.PixelFormat = bayerFormats[i];
                var candidate = bestSingleChannel.CloneForDescriptor(descriptor);
                candidate.MarkAmbiguousWithGroup("Bayer phase variants cannot be distinguished from buffer content.");
                candidates.Add(candidate);
            }
        }

        private static bool IsColorTriple(RawPixelFormat format)
        {
            return format == RawPixelFormat.RGB24 || format == RawPixelFormat.BGR24;
        }

        private static bool IsBayer(RawPixelFormat format)
        {
            return format == RawPixelFormat.BayerRGGB8
                || format == RawPixelFormat.BayerGRBG8
                || format == RawPixelFormat.BayerGBRG8
                || format == RawPixelFormat.BayerBGGR8;
        }
    }
}
