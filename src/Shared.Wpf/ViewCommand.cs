using System;
using System.Windows.Input;

namespace RawBufferVisualizer.Presentation
{
    internal sealed class ViewCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<bool> _canExecute;

        public ViewCommand(Action<object?> execute, Func<bool> canExecute)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => _canExecute();
        public void Execute(object? parameter) { if (CanExecute(parameter)) _execute(parameter); }
        public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
