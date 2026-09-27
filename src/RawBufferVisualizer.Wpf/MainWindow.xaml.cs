using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace RawBufferVisualizer.Wpf
{
    public partial class MainWindow : Window
    {
        private readonly ViewerViewModel _model;
        private readonly ICommand _openCommand;

        public MainWindow()
        {
            InitializeComponent();
            _model = new ViewerViewModel(new ViewerCanvas(OpenGlImageView), new ViewerDialogHost(this));
            DataContext = _model;
            _openCommand = _model.OpenCommand;
        }

        // Public command-line/test entry point; admission and file work belong to the ViewModel.
        public void OpenPath(string path) => _openCommand.Execute(path);

        protected override void OnClosed(EventArgs e)
        {
            _model.Dispose();
            base.OnClosed(e);
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) && _openCommand.CanExecute(null) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            foreach (var path in (string[])e.Data.GetData(DataFormats.FileDrop)) _openCommand.Execute(path);
            e.Handled = true;
        }

        private void DocumentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e) => RevealSelectedTab();
        private void DocumentTabs_SizeChanged(object sender, SizeChangedEventArgs e) => RevealSelectedTab();
        private void RevealSelectedTab()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                var selected = DocumentTabs.SelectedItem;
                if (selected == null || !IsVisible) return;
                DocumentTabs.ScrollIntoView(selected);
                (DocumentTabs.ItemContainerGenerator.ContainerFromItem(selected) as FrameworkElement)?.BringIntoView();
                ImageList.ScrollIntoView(selected);
            }));
        }
    }
}
