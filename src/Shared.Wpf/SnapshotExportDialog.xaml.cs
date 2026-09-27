using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.Presentation
{
    internal partial class SnapshotExportDialog : Window
    {
        private readonly SnapshotExportViewModel _model;
        private readonly ICommand _cancelCommand;

        public SnapshotExportDialog(SnapshotExportViewModel model)
        {
            InitializeComponent();
            DataContext = _model = model;
            _cancelCommand = model.CancelCommand;
            model.Completed += OnCompleted;
            model.CloseRequested += OnCloseRequested;
        }

        // The modal host pumps the dispatcher; the worker also settles when the owner closes early.
        public static async Task<RawBufferSnapshotReference?> ShowAsync(Window? owner, SnapshotExportViewModel model)
        {
            var dialog = new SnapshotExportDialog(model) { Owner = owner };
            if (owner != null)
            {
                dialog.MaxWidth = Math.Max(dialog.MinWidth, owner.ActualWidth);
                dialog.MaxHeight = Math.Max(180, owner.ActualHeight);
            }
            var closeOwner = false;
            CancelEventHandler onOwnerClosing = (sender, args) =>
            {
                if (args.Cancel || model.IsFinished) return;
                args.Cancel = true;
                closeOwner = true;
                model.CancelCommand.Execute(null);
            };
            if (owner != null) owner.Closing += onOwnerClosing;
            var completion = model.RunAsync();
            try
            {
                if (!completion.IsCompleted || model.Error != null) dialog.ShowDialog();
            }
            finally
            {
                if (!model.IsFinished) model.CancelCommand.Execute(null);
                dialog.Detach();
                await completion;
                if (owner != null)
                {
                    owner.Closing -= onOwnerClosing;
                    // Let the viewer's awaiting handler release its borrowed source lease first.
#pragma warning disable VSTHRD001 // Both WPF hosts need this UI-priority ordering; there is no thread switch or blocking invoke.
                    if (closeOwner) _ = owner.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(owner.Close));
#pragma warning restore VSTHRD001
                }
            }
            return await completion;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var handle = new System.Windows.Interop.WindowInteropHelper(Owner ?? this).Handle;
            var screen = System.Windows.Forms.Screen.FromHandle(handle);
            var scale = System.Windows.Media.VisualTreeHelper.GetDpi(this);
            MaxHeight = Math.Min(MaxHeight, screen.WorkingArea.Height / scale.DpiScaleY);
            MaxWidth = Math.Min(MaxWidth, screen.WorkingArea.Width / scale.DpiScaleX);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_model.IsFinished)
            {
                e.Cancel = true;
                _cancelCommand.Execute(null);
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (!_model.IsFinished) _cancelCommand.Execute(null);
            Detach();
            base.OnClosed(e);
        }

        private void OnCompleted(object? sender, EventArgs e) { if (_model.Error == null && IsVisible) Close(); }
        private void OnCloseRequested(object? sender, EventArgs e) { if (IsVisible) Close(); }
        private void Detach()
        {
            _model.Completed -= OnCompleted;
            _model.CloseRequested -= OnCloseRequested;
        }
    }
}
