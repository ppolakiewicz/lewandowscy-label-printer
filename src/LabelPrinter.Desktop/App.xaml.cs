using System.Windows;
using System.Windows.Threading;
using LabelPrinter.Desktop.Views;

namespace LabelPrinter.Desktop;

public partial class App : Application
{
    private AppState? _state;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);

        _state = AppState.Load();
        var window = new MainWindow(_state);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _state?.Dispose();
        _state = null;
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "Wystąpił nieoczekiwany błąd:\n\n" + e.Exception.Message,
            "Etykiety – błąd",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
