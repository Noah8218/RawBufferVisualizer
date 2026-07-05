using System.Windows;

namespace RawBufferVisualizer.Wpf
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var window = new MainWindow();
            MainWindow = window;
            window.Show();

            foreach (var path in e.Args)
            {
                window.OpenPath(path);
            }
        }
    }
}

