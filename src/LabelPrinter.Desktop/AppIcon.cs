using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LabelPrinter.Desktop;

/// <summary>The application icon (Resources/app.ico) for window title bars.</summary>
internal static class AppIcon
{
    private static ImageSource? _icon;
    private static bool _loaded;

    /// <summary>Sets the window icon; a missing or unreadable icon is ignored (Windows then shows the exe icon).</summary>
    public static void ApplyTo(Window window)
    {
        if (!_loaded)
        {
            _loaded = true;
            try
            {
                var frame = BitmapFrame.Create(new Uri("pack://application:,,,/Resources/app.ico", UriKind.Absolute));
                frame.Freeze();
                _icon = frame;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Icon could not be loaded: " + ex);
            }
        }

        if (_icon != null)
        {
            window.Icon = _icon;
        }
    }
}
