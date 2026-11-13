using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Input;
using LabelPrinter.Core.Layout;
using LabelPrinter.Core.Presentation;
using LabelPrinter.Core.Storage;
using LabelPrinter.Desktop.Printing;

namespace LabelPrinter.Desktop.Views;

/// <summary>
/// The main window: type a text (with suggestions from the database), choose the layout, see the preview, print.
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppState _state;
    private readonly LabelPrintService _printService;
    private TextDatabaseWindow? _databaseWindow;

    internal MainWindow(AppState state)
    {
        _state = state;
        _printService = new LabelPrintService(state);
        InitializeComponent();
        AppIcon.ApplyTo(this);

        CopiesBox.Text = "1";
        CopiesBox.PreviewKeyDown += CopiesBox_PreviewKeyDown;
        CopiesBox.PreviewTextInput += CopiesBox_PreviewTextInput;
        DataObject.AddPastingHandler(CopiesBox, CopiesBox_Pasting);
        CopiesBox.GotKeyboardFocus += (_, _) => CopiesBox.SelectAll();

        if (state.Store is { } store)
        {
            Editor.SuggestionSource = query => store.Search(query, 10);
        }
        else
        {
            DatabaseMenu.IsEnabled = false;
            DatabaseBannerText.Text = (state.StoreError ?? "Baza tekstów jest niedostępna.")
                + "\nMożna drukować, ale bez podpowiedzi i bez zapamiętywania tekstów.";
            DatabaseBanner.Visibility = Visibility.Visible;
        }

        Editor.ContentChanged += (_, _) => SchedulePreview();
        Editor.SubmitRequested += (_, _) => PrintCurrent();
        Editor.SuggestionAccepted += (_, label) =>
            SetStatus($"Wstawiono z bazy: „{LabelTextFormatter.Shorten(label.Text)}”. Enter – drukuj.", StatusKind.Info);
        Preview.Rendered += (_, rendered) => Editor.ShowLayout(rendered.Layout);

        _state.ConfigChanged += State_ConfigChanged;
        PreviewKeyDown += Window_PreviewKeyDown;
        Loaded += Window_Loaded;
        Closed += (_, _) => _state.ConfigChanged -= State_ConfigChanged;

        UpdatePrinterInfo();
        SchedulePreview();
        SetStatus(state.Store is null
            ? "Gotowy (bez bazy tekstów)."
            : "Gotowy. Wpisz tekst etykiety – podpowiedzi pojawią się pod polem.", StatusKind.Info);
    }

    // ---- startup ------------------------------------------------------------------------------------------

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Editor.FocusText(selectAll: true);
        if (!_printService.HasPrinter)
        {
            // Let the window appear first, then guide the user to the printer settings.
            Dispatcher.InvokeAsync(() =>
            {
                var answer = MessageBox.Show(
                    this,
                    "Nie wybrano jeszcze drukarki etykiet.\n\nCzy otworzyć teraz ustawienia drukarki? Tam wybierzesz drukarkę "
                    + "(Zebra LP2844 dodana w Windows jako „Generic / Text Only”) i zrobisz wydruk testowy.",
                    "Etykiety – pierwsze uruchomienie",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information,
                    MessageBoxResult.Yes);
                if (answer == MessageBoxResult.Yes)
                {
                    OpenSettings();
                }
            });
        }
    }

    // ---- printing -----------------------------------------------------------------------------------------

    private void PrintButton_Click(object sender, RoutedEventArgs e) => PrintCurrent();

    private void PrintCurrent()
    {
        Editor.CloseSuggestions();
        var content = Editor.CurrentContent;
        if (content.IsEmpty)
        {
            Fail("Wpisz tekst etykiety.");
            Editor.FocusText(selectAll: false);
            return;
        }

        if (Editor.InputError is { } inputError)
        {
            Fail(inputError);
            return;
        }

        if (!CopiesInput.TryParse(CopiesBox.Text, out var copies))
        {
            Fail(CopiesInput.RangeMessage);
            CopiesBox.Focus();
            return;
        }

        if (!_printService.HasPrinter)
        {
            Fail("Nie wybrano drukarki – otwórz „Ustawienia drukarki…”.");
            if (Ask("Nie wybrano drukarki. Czy otworzyć ustawienia drukarki?", MessageBoxImage.Warning))
            {
                OpenSettings();
            }

            return;
        }

        if (!ConfirmLayout(_printService.Layout(content)) || !ConfirmCopies(copies))
        {
            SetStatus("Nie drukowano.", StatusKind.Info);
            return;
        }

        PrintOutcome outcome;
        using (new WaitCursor())
        {
            outcome = _printService.Print(content, copies);
        }

        SetStatus(outcome.Message, outcome.Kind);
        if (!outcome.Success)
        {
            SystemSounds.Exclamation.Play();
            return;
        }

        // Ready for the next label: one copy, the text selected so typing replaces it.
        CopiesBox.Text = "1";
        Editor.FocusText(selectAll: true);
        _databaseWindow?.Reload();
    }

    private bool ConfirmLayout(LabelLayout layout)
    {
        if (!layout.Overflow)
        {
            return true;
        }

        return Ask(
            "Tekst nie mieści się na etykiecie – część zostanie obcięta (zobacz podgląd).\n\nCzy mimo to drukować?",
            MessageBoxImage.Warning);
    }

    private bool ConfirmCopies(int copies) =>
        copies <= CopiesInput.ConfirmAbove
        || Ask($"Czy na pewno wydrukować {copies} kopii?", MessageBoxImage.Question);

    private bool Ask(string question, MessageBoxImage image) =>
        MessageBox.Show(this, question, "Etykiety", MessageBoxButton.YesNo, image, MessageBoxResult.No) == MessageBoxResult.Yes;

    // ---- copies box ---------------------------------------------------------------------------------------

    private void LessButton_Click(object sender, RoutedEventArgs e) => StepCopies(-1);

    private void MoreButton_Click(object sender, RoutedEventArgs e) => StepCopies(+1);

    private void StepCopies(int delta)
    {
        CopiesBox.Text = CopiesInput.Step(CopiesBox.Text, delta).ToString(System.Globalization.CultureInfo.InvariantCulture);
        CopiesBox.CaretIndex = CopiesBox.Text.Length;
    }

    private void CopiesBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                StepCopies(+1);
                e.Handled = true;
                break;
            case Key.Down:
                StepCopies(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                e.Handled = true;
                PrintCurrent();
                break;
        }
    }

    private void CopiesBox_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !e.Text.All(char.IsAsciiDigit);

    private void CopiesBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text || !text.Trim().All(char.IsAsciiDigit))
        {
            e.CancelCommand();
        }
    }

    // ---- menu and shortcuts -------------------------------------------------------------------------------

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        if (e.Key == Key.P && modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            PrintCurrent();
        }
        else if (e.Key == Key.B && modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            OpenDatabase();
        }
        else if (e.Key == Key.F1 && modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            ShowHelp();
        }
    }

    private void DatabaseMenu_Click(object sender, RoutedEventArgs e) => OpenDatabase();

    private void SettingsMenu_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void HelpMenu_Click(object sender, RoutedEventArgs e) => ShowHelp();

    private void DataFolderMenu_Click(object sender, RoutedEventArgs e) => OpenDataFolder(this);

    private void OpenDatabase()
    {
        Editor.CloseSuggestions();
        if (_state.Store is null)
        {
            MessageBox.Show(this, _state.StoreError ?? "Baza tekstów jest niedostępna.", "Baza tekstów",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_databaseWindow != null)
        {
            if (_databaseWindow.WindowState == WindowState.Minimized)
            {
                _databaseWindow.WindowState = WindowState.Normal;
            }

            _databaseWindow.Activate();
            return;
        }

        var window = new TextDatabaseWindow(_state, _printService) { Owner = this };
        window.UseRequested += (_, label) =>
        {
            Editor.SetContent(label.Content);
            Activate();
            Editor.FocusText(selectAll: false);
            SetStatus($"Wstawiono z bazy: „{LabelTextFormatter.Shorten(label.Text)}”.", StatusKind.Info);
        };
        window.Closed += (_, _) => _databaseWindow = null;
        _databaseWindow = window;
        window.Show();
    }

    private void OpenSettings()
    {
        Editor.CloseSuggestions();
        var dialog = new PrinterSettingsWindow(_state) { Owner = this };
        dialog.ShowDialog();
        Editor.FocusText(selectAll: false);
    }

    private void ShowHelp()
    {
        Editor.CloseSuggestions();
        new HelpWindow { Owner = this }.ShowDialog();
    }

    /// <summary>Opens the folder with the settings and the database in Explorer.</summary>
    internal static void OpenDataFolder(Window owner)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.DataDirectory}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"Nie udało się otworzyć folderu {AppPaths.DataDirectory}: {ex.Message}", "Etykiety",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ---- state --------------------------------------------------------------------------------------------

    private void State_ConfigChanged(object? sender, EventArgs e)
    {
        UpdatePrinterInfo();
        SchedulePreview();
    }

    private void UpdatePrinterInfo()
    {
        var config = _state.Config;
        PrinterBanner.Visibility = _printService.HasPrinter ? Visibility.Collapsed : Visibility.Visible;
        PrinterText.Text = _printService.HasPrinter
            ? $"Drukarka: {config.PrinterName} · etykieta {config.LabelWidthMm:0.#} × {config.LabelHeightMm:0.#} mm"
            : "Drukarka: nie wybrano";
    }

    private void SchedulePreview() =>
        Preview.Schedule(Editor.CurrentContent, _state.Config, _state.Renderer, Editor.InputError);

    private void Fail(string message)
    {
        SetStatus(message, StatusKind.Error);
        SystemSounds.Exclamation.Play();
    }

    private void SetStatus(string message, StatusKind kind) =>
        StatusPresenter.Show(StatusBorder, StatusText, message, kind);
}
