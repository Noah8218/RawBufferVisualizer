using System;
using System.ComponentModel;
using System.Windows.Input;
using RawBufferVisualizer.Presentation;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    internal sealed class CompareSelectionViewModel<T> : INotifyPropertyChanged where T : class
    {
        private T? _a;
        private T? _b;

        public CompareSelectionViewModel()
        {
            SwapCommand = new ViewCommand(_ => { var previous = _a; _a = _b; _b = previous; Notify(); }, () => _a != null || _b != null);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public T? A { get => _a; set { if (ReferenceEquals(_a, value)) return; _a = value; Notify(); } }
        public T? B { get => _b; set { if (ReferenceEquals(_b, value)) return; _b = value; Notify(); } }
        public ICommand SwapCommand { get; }

        private void Notify()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            ((ViewCommand)SwapCommand).Refresh();
        }
    }
}
