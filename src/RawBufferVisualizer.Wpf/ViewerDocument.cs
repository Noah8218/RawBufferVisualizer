using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.OpenGlCanvas;

namespace RawBufferVisualizer.Wpf
{
    internal sealed class ViewerDocument : INotifyPropertyChanged, IDisposable
    {
        private readonly HashSet<string> _paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int _readers;
        private bool _closed, _released;

        public ViewerDocument(string? path, RawImageSource source)
        {
            Source = source;
            Descriptor = source.Descriptor;
            IsExample = path == null;
            SetPath(path);
            Thumbnail = ViewerFiles.CreateThumbnail(source);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public string DisplayPath { get; private set; } = string.Empty;
        public string Title { get; private set; } = string.Empty;
        public bool IsExample { get; }
        public RawImageSource Source { get; }
        public RawImageDescriptor Descriptor { get; }
        public RenderedImage? Rendered { get; set; }
        public string? ReadError { get; set; }
        public RawOpenGlViewState? ViewState { get; set; }
        public BitmapSource? Thumbnail { get; }
        public string Summary => $"{Descriptor.Width} × {Descriptor.Height}  {Descriptor.PixelFormat}";
        public bool MatchesPath(string path) => _paths.Contains(path);

        public void SetPath(string? path)
        {
            DisplayPath = path == null ? "In-memory example (not saved)" : Path.GetFullPath(path);
            if (path != null) _paths.Add(DisplayPath);
            Title = path == null ? "Example — Mono8 gradient" : Path.GetFileName(DisplayPath);
            if (Title.EndsWith(".rbuf.json", StringComparison.OrdinalIgnoreCase)) Title = Title.Substring(0, Title.Length - 10);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }

        // Histogram and export borrow the document; closing releases the source only after the last read settles.
        public IDisposable AcquireRead()
        {
            if (_closed) throw new ObjectDisposedException(nameof(ViewerDocument));
            _readers++;
            return new ReadLease(this);
        }

        public void Dispose()
        {
            _closed = true;
            ReleaseIfUnused();
        }

        private void ReleaseIfUnused()
        {
            if (!_closed || _readers != 0 || _released) return;
            _released = true;
            Rendered = null;
            Source.Dispose();
        }

        private sealed class ReadLease : IDisposable
        {
            private ViewerDocument? _owner;
            public ReadLease(ViewerDocument owner) { _owner = owner; }
            public void Dispose()
            {
                var owner = Interlocked.Exchange(ref _owner, null);
                if (owner == null) return;
                owner._readers--;
                owner.ReleaseIfUnused();
            }
        }
    }
}
