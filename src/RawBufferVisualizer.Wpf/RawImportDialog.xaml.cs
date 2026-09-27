using System;
using System.Windows;
using System.Windows.Media;

namespace RawBufferVisualizer.Wpf
{
    internal partial class RawImportDialog : Window
    {
        private readonly RawImportViewModel _model;

        public RawImportDialog(RawImportViewModel model)
        {
            InitializeComponent();
            DataContext = _model = model;
            model.CloseRequested += OnCloseRequested;
            Loaded += OnLoaded;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var handle = new System.Windows.Interop.WindowInteropHelper(Owner ?? this).Handle;
            var screen = System.Windows.Forms.Screen.FromHandle(handle);
            var scale = VisualTreeHelper.GetDpi(this);
            MaxHeight = screen.WorkingArea.Height / scale.DpiScaleY;
            MaxWidth = screen.WorkingArea.Width / scale.DpiScaleX;
        }

        protected override void OnClosed(EventArgs e)
        {
            _model.CloseRequested -= OnCloseRequested;
            Loaded -= OnLoaded;
            base.OnClosed(e);
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => RawWidth.Focus();
        private void OnCloseRequested(bool confirmed) => DialogResult = confirmed;
    }
}
