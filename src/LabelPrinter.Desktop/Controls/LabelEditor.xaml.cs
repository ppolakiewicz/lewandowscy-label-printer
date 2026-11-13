using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LabelPrinter.Core.Layout;
using LabelPrinter.Core.Presentation;
using LabelPrinter.Core.Storage;

namespace LabelPrinter.Desktop.Controls;

/// <summary>
/// Edits a <see cref="LabelContent"/>: the text (one wrapped text or 2–3 separate lines) and the layout options.
/// Optionally suggests saved texts while typing (<see cref="SuggestionSource"/>).
/// </summary>
/// <remarks>
/// Keyboard in the text boxes: ↓/↑ move in the suggestion list (↓ also opens it), Enter or Tab accept the selected
/// suggestion; Enter with the list closed raises <see cref="SubmitRequested"/> (print / save). Enter with the list
/// open but nothing selected only closes the list, so a half-typed text is never printed by accident.
/// </remarks>
public partial class LabelEditor : UserControl
{
    private const int SuggestionLimit = 10;

    private const string DefaultHint =
        "Zacznij pisać – pod polem pojawią się podpowiedzi z bazy (↓ otwiera listę). Enter – drukuj.";

    private const string NoSuggestionsHint = "Enter – zatwierdź.";

    private static readonly string[] PreferredFonts =
    [
        "Arial", "Arial Narrow", "Calibri", "Segoe UI", "Tahoma", "Verdana", "Bahnschrift",
        "Trebuchet MS", "Times New Roman", "Georgia", "Consolas",
    ];

    private readonly TextBox[] _manualBoxes;
    private readonly ObservableCollection<string> _fonts = [];

    // True while controls are changed from code: no ContentChanged, no suggestions.
    private bool _updating;
    private IReadOnlyList<string>? _wrappedLines;
    private double? _autoSizeDots;
    private Window? _window;
    private Func<string, IReadOnlyList<SavedLabel>>? _suggestionSource;

    public LabelEditor()
    {
        InitializeComponent();
        _manualBoxes = [Line1Box, Line2Box, Line3Box];

        foreach (var font in AvailableFonts())
        {
            _fonts.Add(font);
        }

        FontCombo.ItemsSource = _fonts;

        _updating = true;
        AutoModeRadio.IsChecked = true;
        CenterRadio.IsChecked = true;
        AutoSizeRadio.IsChecked = true;
        FontCombo.SelectedItem = LabelContent.DefaultFontFamily;
        _updating = false;
        UpdateModeVisibility();
        UpdateSizeBox();
        HintText.Text = NoSuggestionsHint;

        // Events are wired after the initial state is set, so nothing fires half-initialized.
        AutoModeRadio.Checked += Mode_Checked;
        ManualModeRadio.Checked += Mode_Checked;
        CenterRadio.Checked += Option_Changed;
        LeftRadio.Checked += Option_Changed;
        BoldCheck.Checked += Option_Changed;
        BoldCheck.Unchecked += Option_Changed;
        FontCombo.SelectionChanged += Option_Changed;
        AutoSizeRadio.Checked += Size_Checked;
        FixedSizeRadio.Checked += Size_Checked;
        SizeBox.TextChanged += Option_Changed;
        SizeBox.PreviewKeyDown += SizeBox_PreviewKeyDown;

        foreach (var box in TextBoxes)
        {
            box.TextChanged += Text_TextChanged;
            box.PreviewKeyDown += Text_PreviewKeyDown;
            box.LostKeyboardFocus += Text_LostKeyboardFocus;
        }

        SuggestionList.PreviewMouseLeftButtonDown += SuggestionList_PreviewMouseLeftButtonDown;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Raised whenever the text or an option changes (not while <see cref="SetContent"/> runs; once after it).</summary>
    public event EventHandler? ContentChanged;

    /// <summary>Enter was pressed in a text box with the suggestion list closed.</summary>
    public event EventHandler? SubmitRequested;

    /// <summary>A suggestion was accepted and its content loaded into the editor.</summary>
    public event EventHandler<SavedLabel>? SuggestionAccepted;

    /// <summary>Returns saved texts matching a query; null disables suggestions. Exceptions are ignored.</summary>
    public Func<string, IReadOnlyList<SavedLabel>>? SuggestionSource
    {
        get => _suggestionSource;
        set
        {
            _suggestionSource = value;
            HintText.Text = value is null ? NoSuggestionsHint : DefaultHint;
            if (value is null)
            {
                CloseSuggestions();
            }
        }
    }

    /// <summary>The hint under the text box when suggestions are on.</summary>
    public string SuggestionsHint { get; set; } = DefaultHint;

    public bool IsManualMode => ManualModeRadio.IsChecked == true;

    /// <summary>The label as currently edited. An invalid manual size counts as automatic (see <see cref="InputError"/>).</summary>
    public LabelContent CurrentContent
    {
        get
        {
            double? size = FixedSizeRadio.IsChecked == true && FontSizeInput.TryParseMm(SizeBox.Text, out var mm)
                ? FontSizeInput.MmToDots(mm)
                : null;
            return new LabelContent
            {
                Mode = IsManualMode ? LayoutMode.Manual : LayoutMode.Auto,
                Lines = IsManualMode ? _manualBoxes.Select(b => b.Text).ToArray() : new[] { AutoTextBox.Text },
                Alignment = LeftRadio.IsChecked == true ? LabelAlignment.Left : LabelAlignment.Center,
                Bold = BoldCheck.IsChecked == true,
                FontSizeDots = size,
                FontFamily = FontCombo.SelectedItem as string ?? LabelContent.DefaultFontFamily,
            };
        }
    }

    /// <summary>A Polish message when an option cannot be used (bad manual size), otherwise null.</summary>
    public string? InputError =>
        FixedSizeRadio.IsChecked == true && !FontSizeInput.TryParseMm(SizeBox.Text, out _) ? FontSizeInput.RangeMessage : null;

    /// <summary>True while the suggestion list is shown.</summary>
    public bool IsSuggestionListOpen => SuggestionPopup.IsOpen;

    /// <summary>Loads a label into the editor (text, mode and every option) and raises <see cref="ContentChanged"/> once.</summary>
    public void SetContent(LabelContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        CloseSuggestions();
        _updating = true;
        try
        {
            if (content.Mode == LayoutMode.Manual)
            {
                var lines = content.Lines.Select(l => (l ?? "").Trim()).Where(l => l.Length > 0).ToList();
                if (lines.Count > LabelContent.MaxLines)
                {
                    lines = [.. lines.Take(LabelContent.MaxLines - 1), ModeConversion.ToAutoText(lines.Skip(LabelContent.MaxLines - 1))];
                }

                for (int i = 0; i < _manualBoxes.Length; i++)
                {
                    _manualBoxes[i].Text = i < lines.Count ? lines[i] : "";
                }

                AutoTextBox.Text = ModeConversion.ToAutoText(lines);
                ManualModeRadio.IsChecked = true;
            }
            else
            {
                AutoTextBox.Text = string.Join(' ', content.Lines.Select(l => (l ?? "").Trim()).Where(l => l.Length > 0));
                foreach (var box in _manualBoxes)
                {
                    box.Text = "";
                }

                AutoModeRadio.IsChecked = true;
            }

            (content.Alignment == LabelAlignment.Left ? LeftRadio : CenterRadio).IsChecked = true;
            BoldCheck.IsChecked = content.Bold;

            var family = string.IsNullOrWhiteSpace(content.FontFamily) ? LabelContent.DefaultFontFamily : content.FontFamily.Trim();
            var known = _fonts.FirstOrDefault(f => string.Equals(f, family, StringComparison.OrdinalIgnoreCase));
            if (known is null)
            {
                _fonts.Add(family);
                known = family;
            }

            FontCombo.SelectedItem = known;

            if (content.FontSizeDots is { } dots && double.IsFinite(dots) && dots > 0)
            {
                SizeBox.Text = FontSizeInput.FormatMm(FontSizeInput.DotsToMm(dots));
                FixedSizeRadio.IsChecked = true;
            }
            else
            {
                AutoSizeRadio.IsChecked = true;
            }
        }
        finally
        {
            _updating = false;
        }

        _wrappedLines = null;
        UpdateModeVisibility();
        UpdateSizeBox();
        RaiseContentChanged();
    }

    /// <summary>Puts the keyboard focus into the text (first box), selecting all of it or placing the caret at the end.</summary>
    public void FocusText(bool selectAll)
    {
        var box = IsManualMode ? _manualBoxes.LastOrDefault(b => b.Text.Length > 0) ?? Line1Box : AutoTextBox;
        if (selectAll && IsManualMode)
        {
            box = Line1Box;
        }

        // Focus after layout, in case the box has just become visible.
        Dispatcher.InvokeAsync(
            () =>
            {
                box.Focus();
                Keyboard.Focus(box);
                if (selectAll)
                {
                    box.SelectAll();
                }
                else
                {
                    box.CaretIndex = box.Text.Length;
                }
            },
            DispatcherPriority.Input);
    }

    /// <summary>
    /// Tells the editor how the current content was laid out: used to show the automatic size and to keep the same
    /// line breaks when switching to separate lines.
    /// </summary>
    public void ShowLayout(LabelLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!IsManualMode)
        {
            _wrappedLines = layout.Lines.Select(l => l.Text).ToList();
        }

        _autoSizeDots = layout.AutoSized && !layout.IsEmpty ? layout.FontSizeDots : null;
        AutoSizeInfo.Text = _autoSizeDots is { } dots
            ? $"(teraz {FontSizeInput.FormatMm(Math.Round(FontSizeInput.DotsToMm(dots), 1))} mm)"
            : "";
    }

    public void CloseSuggestions()
    {
        if (SuggestionPopup.IsOpen)
        {
            SuggestionPopup.IsOpen = false;
        }

        SuggestionList.ItemsSource = null;
    }

    private IEnumerable<TextBox> TextBoxes => _manualBoxes.Prepend(AutoTextBox);

    private static List<string> AvailableFonts()
    {
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var family in Fonts.SystemFontFamilies)
            {
                installed.Add(family.Source);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine("Fonts could not be listed: " + ex);
        }

        var result = PreferredFonts.Where(f => installed.Contains(f)).ToList();
        if (!result.Contains(LabelContent.DefaultFontFamily))
        {
            result.Insert(0, LabelContent.DefaultFontFamily);
        }

        return result;
    }

    private void RaiseContentChanged()
    {
        if (!_updating)
        {
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateModeVisibility()
    {
        bool manual = IsManualMode;
        AutoTextBox.Visibility = manual ? Visibility.Collapsed : Visibility.Visible;
        ManualPanel.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateSizeBox()
    {
        SizeBox.IsEnabled = FixedSizeRadio.IsChecked == true;
    }

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        CloseSuggestions();
        _updating = true;
        try
        {
            if (IsManualMode)
            {
                var lines = ModeConversion.ToManualLines(AutoTextBox.Text, _wrappedLines);
                for (int i = 0; i < _manualBoxes.Length; i++)
                {
                    _manualBoxes[i].Text = lines[i];
                }
            }
            else
            {
                AutoTextBox.Text = ModeConversion.ToAutoText(_manualBoxes.Select(b => b.Text));
            }
        }
        finally
        {
            _updating = false;
        }

        UpdateModeVisibility();
        RaiseContentChanged();
        FocusText(selectAll: false);
    }

    private void Option_Changed(object sender, RoutedEventArgs e) => RaiseContentChanged();

    private void Size_Checked(object sender, RoutedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        UpdateSizeBox();
        if (FixedSizeRadio.IsChecked == true)
        {
            if (!FontSizeInput.TryParseMm(SizeBox.Text, out _))
            {
                // Start from the size that was chosen automatically, so the label does not jump.
                var mm = _autoSizeDots is { } dots ? FontSizeInput.RoundToHalfMm(FontSizeInput.DotsToMm(dots)) : 5;
                _updating = true;
                SizeBox.Text = FontSizeInput.FormatMm(mm);
                _updating = false;
            }

            SizeBox.Focus();
            SizeBox.SelectAll();
        }

        RaiseContentChanged();
    }

    private void SizeBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        int step = e.Key switch { Key.Up => 1, Key.Down => -1, _ => 0 };
        if (step == 0)
        {
            return;
        }

        var current = FontSizeInput.TryParseMm(SizeBox.Text, out var mm) ? mm : 5;
        var next = Math.Clamp(FontSizeInput.RoundToHalfMm(current) + step * 0.5, FontSizeInput.MinMm, FontSizeInput.MaxMm);
        SizeBox.Text = FontSizeInput.FormatMm(next);
        SizeBox.CaretIndex = SizeBox.Text.Length;
        e.Handled = true;
    }

    private void Text_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        if (_suggestionSource != null)
        {
            HintText.Text = SuggestionsHint;
        }

        RaiseContentChanged();
        if (sender is TextBox box && box.IsKeyboardFocusWithin)
        {
            ShowSuggestions(box, evenIfEmpty: false);
        }
    }

    private void Text_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => CloseSuggestions();

    private void Text_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            return;
        }

        bool open = SuggestionPopup.IsOpen;
        switch (e.Key)
        {
            case Key.Down when Keyboard.Modifiers == ModifierKeys.None:
                if (open)
                {
                    MoveSelection(+1);
                }
                else
                {
                    ShowSuggestions(box, evenIfEmpty: true);
                }

                e.Handled = SuggestionPopup.IsOpen;
                break;

            case Key.Up when open:
                MoveSelection(-1);
                e.Handled = true;
                break;

            case Key.PageDown when open:
                MoveSelection(+5);
                e.Handled = true;
                break;

            case Key.PageUp when open:
                MoveSelection(-5);
                e.Handled = true;
                break;

            case Key.Enter:
                e.Handled = true;
                if (open)
                {
                    if (SuggestionList.SelectedItem is SuggestionItem selected)
                    {
                        Accept(selected);
                    }
                    else
                    {
                        CloseSuggestions();
                        HintText.Text = "Lista podpowiedzi zamknięta. Naciśnij Enter jeszcze raz, aby wydrukować ten tekst.";
                    }
                }
                else if (Keyboard.Modifiers == ModifierKeys.None)
                {
                    SubmitRequested?.Invoke(this, EventArgs.Empty);
                }

                break;

            case Key.Tab when open:
                if (SuggestionList.SelectedItem is SuggestionItem item)
                {
                    Accept(item);
                    e.Handled = true;
                }
                else
                {
                    CloseSuggestions();
                }

                break;

            case Key.Escape when open:
                CloseSuggestions();
                e.Handled = true;
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        int count = SuggestionList.Items.Count;
        if (count == 0)
        {
            return;
        }

        int index = SuggestionList.SelectedIndex;
        int next = index < 0 ? (delta > 0 ? 0 : -1) : index + delta;

        // Moving up from the first item returns to the typed text (no selection).
        next = next < 0 ? -1 : Math.Min(next, count - 1);
        SuggestionList.SelectedIndex = next;
        if (next >= 0)
        {
            SuggestionList.ScrollIntoView(SuggestionList.Items[next]);
        }
    }

    private void ShowSuggestions(TextBox target, bool evenIfEmpty)
    {
        if (_suggestionSource is null)
        {
            return;
        }

        var query = ModeConversion.ToAutoText(IsManualMode ? _manualBoxes.Select(b => b.Text) : new[] { AutoTextBox.Text });
        if (query.Length == 0 && !evenIfEmpty)
        {
            CloseSuggestions();
            return;
        }

        IReadOnlyList<SavedLabel> results;
        try
        {
            results = _suggestionSource(query);
        }
        catch (Exception ex)
        {
            Trace.WriteLine("Suggestions failed: " + ex);
            CloseSuggestions();
            return;
        }

        if (results.Count == 0)
        {
            CloseSuggestions();
            return;
        }

        SuggestionList.ItemsSource = results.Take(SuggestionLimit).Select(r => new SuggestionItem(r)).ToList();
        SuggestionList.SelectedIndex = -1;
        SuggestionPopup.PlacementTarget = target;
        SuggestionBorder.MinWidth = Math.Max(200, target.ActualWidth);
        if (!SuggestionPopup.IsOpen)
        {
            SuggestionPopup.IsOpen = true;
        }
    }

    private void Accept(SuggestionItem item)
    {
        SetContent(item.Label.Content);
        HintText.Text = "Wstawiono tekst z bazy. Enter – drukuj.";
        FocusText(selectAll: false);
        SuggestionAccepted?.Invoke(this, item.Label);
    }

    private void SuggestionList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && ItemsControl.ContainerFromElement(SuggestionList, source) is ListBoxItem { DataContext: SuggestionItem item })
        {
            e.Handled = true;
            Accept(item);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _window = Window.GetWindow(this);
        if (_window != null)
        {
            _window.Deactivated += Window_Moved;
            _window.LocationChanged += Window_Moved;
            _window.SizeChanged += Window_Moved;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CloseSuggestions();
        if (_window != null)
        {
            _window.Deactivated -= Window_Moved;
            _window.LocationChanged -= Window_Moved;
            _window.SizeChanged -= Window_Moved;
            _window = null;
        }
    }

    // The popup is a separate window: close it when ours moves, resizes or loses focus.
    private void Window_Moved(object? sender, EventArgs e) => CloseSuggestions();
}
