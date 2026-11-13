using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LabelPrinter.Core;
using LabelPrinter.Core.Layout;
using LabelPrinter.Core.Presentation;
using LabelPrinter.Desktop.Rendering;

namespace LabelPrinter.Desktop.Controls;

/// <summary>
/// Large preview of a label: the exact bitmap that will be printed on a label-like card, the chosen font size and
/// warnings (text does not fit, word broken, invalid option). Renders lazily: many changes in a row give one render.
/// </summary>
public partial class LabelPreview : UserControl
{
    private static readonly Brush SeriousBrush = Frozen(Color.FromRgb(0xFD, 0xE7, 0xE9));
    private static readonly Brush SeriousBorderBrush = Frozen(Color.FromRgb(0xC4, 0x2B, 0x1C));

    private readonly Brush _warningBackground;
    private readonly Brush _warningBorderBrush;

    private LabelContent? _content;
    private PrinterConfig? _config;
    private LabelRenderer? _renderer;
    private string? _inputWarning;
    private bool _queued;

    public LabelPreview()
    {
        InitializeComponent();
        _warningBackground = WarningBorder.Background;
        _warningBorderBrush = WarningBorder.BorderBrush;
    }

    /// <summary>Raised after each successful render (on the UI thread).</summary>
    public event EventHandler<RenderedLabel>? Rendered;

    /// <summary>Width of the label card in device-independent pixels; the height follows the label proportions.</summary>
    public double PreviewWidth { get; set; } = 440;

    /// <summary>Asks for a new preview; the newest request is rendered once the UI is idle.</summary>
    /// <param name="inputWarning">An extra warning to show (e.g. an invalid manual font size), or null.</param>
    public void Schedule(LabelContent content, PrinterConfig config, LabelRenderer renderer, string? inputWarning = null)
    {
        _content = content;
        _config = config;
        _renderer = renderer;
        _inputWarning = inputWarning;
        if (_queued)
        {
            return;
        }

        _queued = true;
        Dispatcher.InvokeAsync(RenderPending, DispatcherPriority.Background);
    }

    private void RenderPending()
    {
        _queued = false;
        if (_content is not { } content || _config is not { } config || _renderer is not { } renderer)
        {
            return;
        }

        int width = config.LabelWidthDots, height = config.LabelHeightDots;
        LabelFrame.Width = PreviewWidth;
        LabelFrame.Height = Math.Round(PreviewWidth * height / Math.Max(1, width));

        try
        {
            var rendered = renderer.Render(content, width, height);
            PreviewImage.Source = rendered.Bitmap.ToBitmapSource();
            Placeholder.Visibility = rendered.Layout.IsEmpty ? Visibility.Visible : Visibility.Collapsed;

            var summary = LayoutDescription.Describe(rendered.Layout);
            InfoText.Text = summary.Info;
            var warnings = new[] { _inputWarning, summary.Warning }.Where(w => !string.IsNullOrEmpty(w));
            ShowWarning(string.Join("\n", warnings), serious: summary.IsSerious || _inputWarning != null);
            Rendered?.Invoke(this, rendered);
        }
        catch (Exception ex)
        {
            PreviewImage.Source = null;
            InfoText.Text = "";
            ShowWarning("Nie udało się przygotować podglądu: " + ex.Message, serious: true);
        }
    }

    private void ShowWarning(string text, bool serious)
    {
        WarningText.Text = text;
        WarningBorder.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        WarningBorder.Background = serious ? SeriousBrush : _warningBackground;
        WarningBorder.BorderBrush = serious ? SeriousBorderBrush : _warningBorderBrush;
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
