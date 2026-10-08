using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Presentation;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    internal sealed class BufferDiagnosisViewModel<T> : INotifyPropertyChanged where T : class
    {
        private readonly Func<T, RawImageDescriptor> _descriptor;
        private object? _sourceIdentity;
        private RawImageDescriptor? _committed;
        private RawImageDescriptor? _preview;
        private T? _selected;
        private bool _available;

        public BufferDiagnosisViewModel(Func<T, RawImageDescriptor> descriptor)
        {
            _descriptor = descriptor;
            DiagnoseCommand = new ViewCommand(_ => Start(), () => CanDiagnose);
            ApplyCandidateCommand = new ViewCommand(_ => Commit(_preview!), () => HasPreview && CanDiagnose);
            CancelPreviewCommand = new ViewCommand(_ => CancelPreview(), () => HasPreview && CanDiagnose);
            CloseCommand = new ViewCommand(_ => { if (!HasPreview || CancelPreview()) { IsVisible = false; Candidates = Array.Empty<T>(); _selected = null; Status = string.Empty; Notify(); } }, () => IsVisible && !IsRunning);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action<BufferDiagnosisViewModel<T>>? DiagnoseRequested;
        public event Action<BufferDiagnosisViewModel<T>, RawImageDescriptor>? DescriptorRequested;
        public event Action<BufferDiagnosisViewModel<T>, string>? Failed;
        public bool IsVisible { get; private set; }
        public bool IsRunning { get; private set; }
        public bool CanDiagnose => _available && _committed != null && !IsRunning;
        public bool HasPreview => _preview != null;
        public string Status { get; private set; } = string.Empty;
        public string Details { get; private set; } = string.Empty;
        public IReadOnlyList<T> Candidates { get; private set; } = Array.Empty<T>();
        public T? SelectedCandidate { get => _selected; set { if (ReferenceEquals(_selected, value)) return; if (value == null) { _selected = null; Notify(); } else Preview(value); } }
        public ICommand DiagnoseCommand { get; }
        public ICommand ApplyCandidateCommand { get; }
        public ICommand CancelPreviewCommand { get; }
        public ICommand CloseCommand { get; }

        public bool IsForSource(object identity) => ReferenceEquals(_sourceIdentity, identity);

        public void SetSource(RawImageDescriptor descriptor, object identity, bool available)
        {
            if (IsForSource(identity) && _available == available) return;
            Clear();
            _sourceIdentity = identity;
            _committed = descriptor.Clone();
            _available = available;
            Notify();
        }

        public void Clear()
        {
            _sourceIdentity = null;
            _committed = _preview = null;
            _selected = null;
            _available = IsVisible = IsRunning = false;
            Candidates = Array.Empty<T>();
            Status = Details = string.Empty;
            Notify();
        }

        public void Complete(IReadOnlyList<T> candidates, int errors)
        {
            if (!IsRunning) return;
            IsRunning = false;
            Candidates = candidates;
            _selected = _preview == null ? null : candidates.FirstOrDefault(c => SameLayout(_descriptor(c), _preview));
            Status = GetResultStatus();
            Details = errors > 0 ? "Buffer diagnostics report " + errors.ToString(CultureInfo.InvariantCulture) + " error(s)." : string.Empty;
            Notify();
        }

        public void Fail(string message)
        {
            IsRunning = false;
            Status = "Diagnosis failed: " + message;
            Failed?.Invoke(this, message);
            Notify();
        }

        public void Cancelled()
        {
            if (!IsRunning) return;
            IsRunning = false;
            IsVisible = HasPreview || Candidates.Count > 0;
            Status = string.Empty;
            Notify();
        }

        public void Commit(RawImageDescriptor descriptor)
        {
            _committed = descriptor.Clone();
            _preview = null;
            _selected = null;
            if (Candidates.Count > 0) Status = GetResultStatus();
            Notify();
        }

        public bool IsCurrent(T candidate) => _committed != null && SameLayout(_descriptor(candidate), _committed);

        private void Start()
        {
            IsVisible = IsRunning = true;
            Candidates = Array.Empty<T>();
            _selected = null;
            Status = "Diagnosing buffer interpretations...";
            Details = string.Empty;
            Notify();
            try { DiagnoseRequested?.Invoke(this); }
            catch (Exception ex) { Fail(ex.Message); }
        }

        private void Preview(T candidate)
        {
            if (!CanDiagnose || !Candidates.Contains(candidate)) { Notify(); return; }
            var previous = _preview;
            var selected = _selected;
            _preview = _descriptor(candidate).Clone();
            _selected = candidate;
            try { DescriptorRequested?.Invoke(this, _preview.Clone()); Status = GetResultStatus(); Notify(); }
            catch (Exception ex) { _preview = previous; _selected = selected; Fail(ex.Message); }
        }

        private bool CancelPreview()
        {
            var preview = _preview;
            var selected = _selected;
            _preview = null;
            _selected = null;
            try { DescriptorRequested?.Invoke(this, _committed!.Clone()); Status = GetResultStatus(); Notify(); return true; }
            catch (Exception ex) { _preview = preview; _selected = selected; Fail(ex.Message); return false; }
        }

        private string GetResultStatus() => string.Format(CultureInfo.InvariantCulture, "Suggests {0} ranked interpretations. It can't detect the correct format automatically; select a row to preview it, then Apply.", Candidates.Count);

        private static bool SameLayout(RawImageDescriptor left, RawImageDescriptor right) => left.Width == right.Width && left.Height == right.Height && left.Stride == right.Stride && left.PixelFormat == right.PixelFormat && left.ValidBits == right.ValidBits && left.ByteOrder == right.ByteOrder;

        private void Notify()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            foreach (var command in new[] { DiagnoseCommand, ApplyCandidateCommand, CancelPreviewCommand, CloseCommand }) ((ViewCommand)command).Refresh();
        }
    }
}
