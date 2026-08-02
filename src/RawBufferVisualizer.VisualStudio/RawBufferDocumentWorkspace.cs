using System;
using System.Collections.ObjectModel;

namespace RawBufferVisualizer.VisualStudio
{
    internal sealed class RawBufferDocumentWorkspace<TDocument> : IDisposable
        where TDocument : class, IDisposable
    {
        private readonly ObservableCollection<TDocument> _documents =
            new ObservableCollection<TDocument>();
        private bool _disposed;

        public ObservableCollection<TDocument> Documents
        {
            get { return _documents; }
        }

        public TDocument? ActiveDocument { get; private set; }

        public void Add(TDocument document)
        {
            ThrowIfDisposed();
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            _documents.Add(document);
        }

        public bool Activate(TDocument document)
        {
            ThrowIfDisposed();
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            if (!_documents.Contains(document))
            {
                throw new InvalidOperationException(
                    "Only a document owned by the workspace can be activated.");
            }

            if (ReferenceEquals(ActiveDocument, document))
            {
                return false;
            }

            ActiveDocument = document;
            return true;
        }

        public TDocument? RemoveAt(int index, bool chooseReplacement)
        {
            ThrowIfDisposed();
            if (index < 0 || index >= _documents.Count)
            {
                throw new ArgumentOutOfRangeException("index");
            }

            var document = _documents[index];
            var wasActive = ReferenceEquals(ActiveDocument, document);
            _documents.RemoveAt(index);
            if (wasActive)
            {
                ActiveDocument = null;
            }

            document.Dispose();
            if (!wasActive || !chooseReplacement || _documents.Count == 0)
            {
                return ActiveDocument;
            }

            return _documents[Math.Min(index, _documents.Count - 1)];
        }

        public void Clear()
        {
            if (_disposed)
            {
                return;
            }

            for (var index = _documents.Count - 1; index >= 0; index--)
            {
                var document = _documents[index];
                _documents.RemoveAt(index);
                document.Dispose();
            }

            ActiveDocument = null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Clear();
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }
}
