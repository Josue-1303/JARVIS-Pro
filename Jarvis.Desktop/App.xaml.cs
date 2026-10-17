using System.Windows;

namespace Jarvis.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try { var window = new MainWindow(); MainWindow = window; window.Show(); }
        catch (Exception ex)
        {
            MessageBox.Show("J.A.R.V.I.S. no pudo abrir la interfaz.\n\n" + ex.Message,
                "J.A.R.V.I.S. Pro", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}