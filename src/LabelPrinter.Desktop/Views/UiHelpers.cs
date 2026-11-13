using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LabelPrinter.Desktop.Printing;

namespace LabelPrinter.Desktop.Views;

/// <summary>Colours of a status line (border + text) for each <see cref="StatusKind"/>.</summary>
internal static class StatusPresenter
{
    private static readonly Brush ErrorBackground = Frozen(0xFD, 0xE7, 0xE9);
    private static readonly Brush ErrorForeground = Frozen(0xA4, 0x26, 0x2C);
    private static readonly Brush WarningBackground = Frozen(0xFF, 0xF4, 0xCE);
    private static readonly Brush WarningForeground = Frozen(0x6B, 0x4E, 0x00);
    private static readonly Brush SuccessBackground = Frozen(0xDF, 0xF6, 0xDD);
    private static readonly Brush SuccessForeground = Frozen(0x10, 0x5E, 0x10);

    public static void Show(Border border, TextBlock text, string message, StatusKind kind)
    {
        text.Text = message;
        switch (kind)
        {
            case StatusKind.Error:
                border.Background = ErrorBackground;
                text.Foreground = ErrorForeground;
                text.FontWeight = FontWeights.SemiBold;
                break;
            case StatusKind.Warning:
                border.Background = WarningBackground;
                text.Foreground = WarningForeground;
                text.FontWeight = FontWeights.SemiBold;
                break;
            case StatusKind.Success:
                border.Background = SuccessBackground;
                text.Foreground = SuccessForeground;
                text.FontWeight = FontWeights.Normal;
                break;
            default:
                border.Background = Brushes.Transparent;
                text.Foreground = SystemColors.ControlTextBrush;
                text.FontWeight = FontWeights.Normal;
                break;
        }
    }

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// <summary>Shows the wait cursor until disposed.</summary>
internal sealed class WaitCursor : IDisposable
{
    private readonly Cursor? _previous = Mouse.OverrideCursor;

    public WaitCursor()
    {
        Mouse.OverrideCursor = Cursors.Wait;
    }

    public void Dispose() => Mouse.OverrideCursor = _previous;
}
