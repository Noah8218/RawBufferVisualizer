using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media.Imaging;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    internal sealed class BufferDiagnosisCandidateItem : INotifyPropertyChanged
    {
        private readonly string _summary;
        private bool _isCurrent;

        public event PropertyChangedEventHandler? PropertyChanged;
        public bool IsTop { get; }
        public bool IsCurrent { get => _isCurrent; set { if (_isCurrent == value) return; _isCurrent = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary))); } }
        public BufferInterpretationCandidate Candidate { get; private set; }
        public BitmapSource? Thumbnail { get; private set; }
        public string Title { get; private set; }
        public string Summary => _summary + (IsCurrent ? " (current)" : string.Empty);
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
            BitmapSource? thumbnail = null,
            bool isTop = false,
            bool isDocked = false)
        {
            Candidate = candidate;
            Thumbnail = thumbnail;
            IsTop = isTop;
            var descriptor = candidate.Descriptor;
            var padding = descriptor.Stride - descriptor.GetMinimumStride();
            Title = string.Format(
                CultureInfo.InvariantCulture,
                isDocked ? "{0} · {1} × {2}" : "{0}  {1} x {2}",
                descriptor.PixelFormat,
                descriptor.Width,
                descriptor.Height);
            _summary = string.Format(
                CultureInfo.InvariantCulture,
                isDocked ? "stride {0}{1} · {2}-bit · {3}" : "stride {0}{1}, {2} bits, {3}",
                descriptor.Stride,
                padding > 0 ? " (+" + padding + (isDocked ? " B/row padding)" : " pad/row)") : string.Empty,
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
