using System.Windows;
using System.Windows.Threading;

namespace LabelPrinter.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
