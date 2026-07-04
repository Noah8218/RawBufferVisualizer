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

            if (e.Args.Length > 0)
            {
                window.OpenPath(e.Args[0]);
            }
        }
    }
}

