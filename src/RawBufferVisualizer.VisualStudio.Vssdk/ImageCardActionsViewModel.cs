using System;
using System.ComponentModel;
using System.Windows.Input;
using RawBufferVisualizer.Presentation;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    internal enum ImageCardAction { SaveVisiblePng, SaveSnapshot, CopyPointer, CopyPixels, SetA, SetB, Remove }

    internal sealed class ImageCardActionsViewModel<T> : INotifyPropertyChanged where T : class
    {
        private readonly CompareSelectionViewModel<T> _comparison;
        private readonly Func<T, bool> _hasPixels;
        private readonly Func<T, bool> _hasPointer;
        private readonly Func<T, bool> _hasPixelAddress;
        private readonly Func<T, bool> _canExportRaw;
        private T? _target;
        private bool _isOpen;

        public ImageCardActionsViewModel(
            CompareSelectionViewModel<T> comparison,
            Func<T, bool> hasPixels,
            Func<T, bool> hasPointer,
            Func<T, bool> hasPixelAddress,
            Func<T, bool>? canExportRaw = null)
        {
            _comparison = comparison;
            _hasPixels = hasPixels;
            _hasPointer = hasPointer;
            _hasPixelAddress = hasPixelAddress;
            _canExportRaw = canExportRaw ?? hasPixels;
            OpenCommand = new ViewCommand(parameter => { if (parameter is T target) { _target = target; _isOpen = true; Notify(); } }, () => true);
            ActionCommand = new ViewCommand(Execute, CanExecute);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action<T, ImageCardAction>? ActionRequested;
        public T? Target => _target;
        public bool IsOpen { get => _isOpen; set { if (_isOpen == value) return; _isOpen = value; if (!value) _target = null; Notify(); } }
        public ICommand OpenCommand { get; }
        public ICommand ActionCommand { get; }

        public void Refresh() => Notify();

        public void Clear() { _target = null; _isOpen = false; Notify(); }

        private bool CanExecute(object? parameter)
        {
            if (_target == null || parameter is not string name || !Enum.TryParse(name, out ImageCardAction action)) return false;
            switch (action)
            {
                case ImageCardAction.SaveSnapshot: return _hasPixels(_target) && _canExportRaw(_target);
                case ImageCardAction.SaveVisiblePng:
                case ImageCardAction.SetA:
                case ImageCardAction.SetB: return _hasPixels(_target);
                case ImageCardAction.CopyPointer: return _hasPointer(_target);
                case ImageCardAction.CopyPixels: return _hasPixelAddress(_target);
                case ImageCardAction.Remove: return true;
                default: return false;
            }
        }

        private void Execute(object? parameter)
        {
            var target = _target!;
            var action = (ImageCardAction)Enum.Parse(typeof(ImageCardAction), (string)parameter!);
            Clear();
            if (action == ImageCardAction.SetA) _comparison.A = target;
            else if (action == ImageCardAction.SetB) _comparison.B = target;
            else ActionRequested?.Invoke(target, action);
        }

        private void Notify()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            ((ViewCommand)ActionCommand).Refresh();
        }
    }
}
