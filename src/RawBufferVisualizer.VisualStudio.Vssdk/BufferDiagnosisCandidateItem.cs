using System.Globalization;
using System.Windows;
using System.Windows.Media.Imaging;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    internal sealed class BufferDiagnosisCandidateItem
    {
        public BufferInterpretationCandidate Candidate { get; private set; }
        public BitmapSource? Thumbnail { get; private set; }
        public string Title { get; private set; }
        public string Summary { get; private set; }
        public string ScoreText { get; private set; }
        public string AmbiguityNote { get; private set; }
        public string ReasonsText { get; private set; }
        public string AccessibleName { get; private set; }

        public Visibility AmbiguityVisibility
        {
            get { return string.IsNullOrEmpty(AmbiguityNote) ? Visibility.Collapsed : Visibility.Visible; }
        }

        public BufferDiagnosisCandidateItem(
            BufferInterpretationCandidate candidate,
            BitmapSource? thumbnail = null)
        {
            Candidate = candidate;
            Thumbnail = thumbnail;
            var descriptor = candidate.Descriptor;
            var padding = descriptor.Stride - descriptor.GetMinimumStride();
            Title = string.Format(
                CultureInfo.InvariantCulture,
                "{0}  {1} x {2}",
                descriptor.PixelFormat,
                descriptor.Width,
                descriptor.Height);
            Summary = string.Format(
                CultureInfo.InvariantCulture,
                "stride {0}{1}, {2} bits, {3}",
                descriptor.Stride,
                padding > 0 ? " (+" + padding + " pad/row)" : string.Empty,
                descriptor.ValidBits,
                descriptor.ByteOrder == RawByteOrder.LittleEndian ? "LE" : "BE");
            ScoreText = candidate.Score.ToString(CultureInfo.InvariantCulture);
            AmbiguityNote = candidate.IsAmbiguousWithGroup
                ? "Tied group: cannot be distinguished from buffer content."
                : string.Empty;
            AccessibleName = Title + ", " + Summary
                + (string.IsNullOrEmpty(AmbiguityNote) ? string.Empty : ", " + AmbiguityNote);
            ReasonsText = candidate.Reasons.Count == 0
                ? "No scoring reasons."
                : string.Join("\n", candidate.Reasons);
        }
    }
}
