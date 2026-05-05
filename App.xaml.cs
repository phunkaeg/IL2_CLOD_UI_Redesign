using System;
using System.Windows;

namespace PlaneLoadoutWpfTest;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var w = new MainWindow();
            MainWindow = w;
            w.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Startup failed", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }
}
