using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using LabelPrinter.Core.Layout;
using LabelPrinter.Core.Presentation;
using LabelPrinter.Core.Storage;
using LabelPrinter.Desktop.Printing;
using Microsoft.Win32;

namespace LabelPrinter.Desktop.Views;

/// <summary>
/// The saved-text database: search, add, edit, delete, print, put into the main window, CSV import/export.
/// </summary>
public partial class TextDatabaseWindow : Window
{
    private readonly AppState _state;
    private readonly LabelTextStore _store;
    private readonly LabelPrintService _printService;
    private readonly DispatcherTimer _filterTimer;

    internal TextDatabaseWindow(AppState state, LabelPrintService printService)
    {
        _state = state;
        _store = state.Store ?? throw new InvalidOperationException("The text database is not available.");
        _printService = printService;
        _filterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _filterTimer.Tick += (_, _) =>
        {
            _filterTimer.Stop();
            Reload();
        };

        InitializeComponent();
        AppIcon.ApplyTo(this);

        CopiesBox.Text = "1";
        CopiesBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsAsciiDigit);
        FilterBox.TextChanged += (_, _) =>
        {
            _filterTimer.Stop();
            _filterTimer.Start();
        };
        FilterBox.PreviewKeyDown += FilterBox_PreviewKeyDown;
        LabelsGrid.PreviewKeyDown += LabelsGrid_PreviewKeyDown;
        PreviewKeyDown += Window_PreviewKeyDown;
        Loaded += (_, _) => FilterBox.Focus();
        Closed += (_, _) => _filterTimer.Stop();

        Reload();
        SetStatus("Zaznacz tekst na liście. Kilka tekstów zaznaczysz z Ctrl lub Shift.", StatusKind.Info);
    }

    /// <summary>"Użyj": the user wants this label in the main window (the database window then closes).</summary>
    public event EventHandler<SavedLabel>? UseRequested;

    /// <summary>Reloads the list (keeping the filter, sorting and selection, or selecting <paramref name="selectIds"/>).</summary>
    public void Reload(IEnumerable<long>? selectIds = null)
    {
        var keep = (selectIds ?? SelectedLabels().Select(l => l.Id)).ToHashSet();
        var sorting = LabelsGrid.Columns
            .Where(c => c.SortDirection is not null)
            .Select(c => (Column: c, Direction: c.SortDirection!.Value))
            .ToList();

        IReadOnlyList<SavedLabel> labels;
        int total;
        try
        {
            labels = _store.GetAll(FilterBox.Text);
            total = _store.Count();
        }
        catch (Exception ex)
        {
            SetStatus("Nie udało się odczytać bazy: " + ex.Message, StatusKind.Error);
            return;
        }

        var rows = labels.Select(l => new SavedLabelRow(l)).ToList();
        LabelsGrid.ItemsSource = rows;

        var view = CollectionViewSource.GetDefaultView(LabelsGrid.ItemsSource);
        foreach (var (column, direction) in sorting)
        {
            view.SortDescriptions.Add(new SortDescription(column.SortMemberPath, direction));
            column.SortDirection = direction;
        }

        LabelsGrid.SelectedItems.Clear();
        foreach (var row in rows.Where(r => keep.Contains(r.Id)))
        {
            LabelsGrid.SelectedItems.Add(row);
        }

        if (LabelsGrid.SelectedItem is { } selected)
        {
            LabelsGrid.ScrollIntoView(selected);
        }

        CountText.Text = string.IsNullOrWhiteSpace(FilterBox.Text)
            ? $"Wpisów w bazie: {total}"
            : $"Znaleziono: {rows.Count} z {total}";
        UpdateButtons();
    }

    private List<SavedLabel> SelectedLabels() =>
        LabelsGrid.SelectedItems.OfType<SavedLabelRow>()
            .OrderBy(r => LabelsGrid.Items.IndexOf(r))
            .Select(r => r.Label)
            .ToList();

    private SavedLabel? SingleSelected()
    {
        var selected = SelectedLabels();
        return selected.Count == 1 ? selected[0] : null;
    }

    private void UpdateButtons()
    {
        int count = LabelsGrid.SelectedItems.Count;
        EditButton.IsEnabled = count == 1;
        UseButton.IsEnabled = count == 1;
        DeleteButton.IsEnabled = count > 0;
        PrintButton.IsEnabled = count > 0;
    }

    // ---- keyboard and mouse -------------------------------------------------------------------------------

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            FilterBox.Focus();
            FilterBox.SelectAll();
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.Control && e.Key == Key.N)
        {
            e.Handled = true;
            AddLabel();
        }
        else if (modifiers == ModifierKeys.Control && e.Key == Key.P)
        {
            e.Handled = true;
            PrintSelected();
        }
        else if (modifiers == ModifierKeys.None && e.Key == Key.F2)
        {
            e.Handled = true;
            EditSelected();
        }
        else if (modifiers == ModifierKeys.None && e.Key == Key.Escape)
        {
            e.Handled = true;
            if (FilterBox.IsKeyboardFocused && FilterBox.Text.Length > 0)
            {
                FilterBox.Clear();
            }
            else
            {
                Close();
            }
        }
    }

    private void FilterBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Enter && LabelsGrid.Items.Count > 0)
        {
            e.Handled = true;
            if (_filterTimer.IsEnabled)
            {
                _filterTimer.Stop();
                Reload();
            }

            if (LabelsGrid.Items.Count == 0)
            {
                return;
            }

            if (LabelsGrid.SelectedIndex < 0)
            {
                LabelsGrid.SelectedIndex = 0;
            }

            FocusSelectedRow();
        }
    }

    private void FocusSelectedRow()
    {
        var item = LabelsGrid.SelectedItem;
        if (item is null)
        {
            return;
        }

        LabelsGrid.ScrollIntoView(item);
        LabelsGrid.UpdateLayout();
        if (LabelsGrid.ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row)
        {
            row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
        else
        {
            LabelsGrid.Focus();
        }
    }

    private void LabelsGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                EditSelected();
                break;
            case Key.Delete:
                e.Handled = true;
                DeleteSelected();
                break;
        }
    }

    private void LabelsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(LabelsGrid, source) is DataGridRow)
        {
            e.Handled = true;
            EditSelected();
        }
    }

    private void LabelsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void ClearFilter_Click(object sender, RoutedEventArgs e)
    {
        FilterBox.Clear();
        FilterBox.Focus();
    }

    // ---- add / edit / delete ------------------------------------------------------------------------------

    private void Add_Click(object sender, RoutedEventArgs e) => AddLabel();

    private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();

    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteSelected();

    private void AddLabel()
    {
        LabelEditDialog? dialog = null;
        dialog = new LabelEditDialog(_state, "Nowy tekst", LabelContent.FromText(FilterBox.Text.Trim()), note: null,
            save: (content, note) => SaveNew(dialog!, content, note))
        {
            Owner = this,
        };

        if (dialog.ShowDialog() == true && dialog.Saved is { } saved)
        {
            FilterBox.Text = "";
            _filterTimer.Stop();
            Reload([saved.Id]);
            FocusSelectedRow();
            SetStatus($"Dodano: „{LabelTextFormatter.Shorten(saved.Text)}”.", StatusKind.Success);
        }
    }

    private SavedLabel? SaveNew(Window owner, LabelContent content, string? note)
    {
        try
        {
            return _store.Add(content, note);
        }
        catch (DuplicateLabelException ex)
        {
            var existing = _store.Get(ex.ExistingId);
            var answer = MessageBox.Show(
                owner,
                $"Tekst „{LabelTextFormatter.Shorten(ex.Text, 60)}” jest już w bazie"
                + (existing is null ? "." : $" (drukowany {existing.UseCount} razy).")
                + "\n\nCzy zastąpić zapisane ustawienia tego tekstu nowymi? Licznik użyć zostanie zachowany."
                + "\n\nTak – zapisz nowe ustawienia w istniejącym wpisie.\nNie – wróć do edycji.",
                "Tekst już istnieje",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes)
            {
                return null;
            }

            return _store.Update(ex.ExistingId, content, string.IsNullOrWhiteSpace(note) ? existing?.Note : note);
        }
    }

    private void EditSelected()
    {
        if (SingleSelected() is not { } label)
        {
            if (LabelsGrid.SelectedItems.Count > 1)
            {
                SetStatus("Do edycji zaznacz tylko jeden tekst.", StatusKind.Warning);
            }

            return;
        }

        LabelEditDialog? dialog = null;
        dialog = new LabelEditDialog(_state, "Edycja tekstu", label.Content, label.Note,
            save: (content, note) => SaveExisting(dialog!, label, content, note))
        {
            Owner = this,
        };

        if (dialog.ShowDialog() == true && dialog.Saved is { } saved)
        {
            Reload([saved.Id]);
            FocusSelectedRow();
            SetStatus($"Zapisano: „{LabelTextFormatter.Shorten(saved.Text)}”.", StatusKind.Success);
        }
    }

    private SavedLabel? SaveExisting(Window owner, SavedLabel label, LabelContent content, string? note)
    {
        try
        {
            return _store.Update(label.Id, content, note);
        }
        catch (DuplicateLabelException ex)
        {
            var other = _store.Get(ex.ExistingId);
            var answer = MessageBox.Show(
                owner,
                $"W bazie jest już inny wpis z tekstem „{LabelTextFormatter.Shorten(ex.Text, 60)}”"
                + (other is null ? "." : $" (drukowany {other.UseCount} razy).")
                + "\n\nCzy połączyć oba wpisy w jeden? Zostaną nowe ustawienia, a liczniki użyć zostaną zsumowane."
                + "\n\nTak – połącz.\nNie – wróć do edycji.",
                "Taki tekst już istnieje",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);
            return answer == MessageBoxResult.Yes ? _store.Update(label.Id, content, note, mergeIfDuplicate: true) : null;
        }
        catch (KeyNotFoundException)
        {
            MessageBox.Show(owner, "Ten wpis został w międzyczasie usunięty z bazy.", "Baza tekstów",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
    }

    private void DeleteSelected()
    {
        var selected = SelectedLabels();
        if (selected.Count == 0)
        {
            return;
        }

        var question = selected.Count == 1
            ? $"Usunąć z bazy tekst „{LabelTextFormatter.Shorten(selected[0].Text, 60)}”?"
            : $"Usunąć z bazy zaznaczone teksty ({selected.Count})?";
        if (MessageBox.Show(this, question + "\n\nTej operacji nie można cofnąć.", "Usuwanie", MessageBoxButton.YesNo,
                MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        int deleted = 0;
        try
        {
            foreach (var label in selected)
            {
                if (_store.Delete(label.Id))
                {
                    deleted++;
                }
            }
        }
        catch (Exception ex)
        {
            SetStatus("Błąd podczas usuwania: " + ex.Message, StatusKind.Error);
        }

        int index = LabelsGrid.SelectedIndex;
        Reload([]);
        if (LabelsGrid.Items.Count > 0)
        {
            LabelsGrid.SelectedIndex = Math.Clamp(index, 0, LabelsGrid.Items.Count - 1);
            FocusSelectedRow();
        }

        if (deleted > 0)
        {
            SetStatus(deleted == 1 ? "Usunięto 1 tekst." : $"Usunięto teksty: {deleted}.", StatusKind.Success);
        }
    }

    // ---- use / print --------------------------------------------------------------------------------------

    private void Use_Click(object sender, RoutedEventArgs e)
    {
        if (SingleSelected() is not { } label)
        {
            return;
        }

        UseRequested?.Invoke(this, label);
        Close();
    }

    private void Print_Click(object sender, RoutedEventArgs e) => PrintSelected();

    private void PrintSelected()
    {
        var selected = SelectedLabels();
        if (selected.Count == 0)
        {
            SetStatus("Zaznacz teksty do wydrukowania.", StatusKind.Warning);
            return;
        }

        if (!CopiesInput.TryParse(CopiesBox.Text, out var copies))
        {
            SetStatus(CopiesInput.RangeMessage, StatusKind.Error);
            CopiesBox.Focus();
            return;
        }

        if (!_printService.HasPrinter)
        {
            SetStatus("Nie wybrano drukarki – otwórz „Ustawienia drukarki…” w oknie głównym.", StatusKind.Error);
            return;
        }

        int total = selected.Count * copies;
        if (selected.Count > 1 || copies > CopiesInput.ConfirmAbove)
        {
            var question = selected.Count == 1
                ? $"Wydrukować {copies} kopii tekstu „{LabelTextFormatter.Shorten(selected[0].Text, 60)}”?"
                : $"Wydrukować {selected.Count} różnych etykiet, po {copies} szt. (razem {total})?";
            if (!Ask(question, MessageBoxImage.Question))
            {
                return;
            }
        }

        var overflowing = selected.Where(l => _printService.Layout(l.Content).Overflow).ToList();
        if (overflowing.Count > 0)
        {
            var names = string.Join("\n", overflowing.Take(5).Select(l => "• " + LabelTextFormatter.Shorten(l.Text, 60)));
            if (!Ask($"Te teksty nie mieszczą się na etykiecie i zostaną obcięte:\n{names}\n\nCzy mimo to drukować?",
                    MessageBoxImage.Warning))
            {
                return;
            }
        }

        int printed = 0;
        PrintOutcome? last = null;
        using (new WaitCursor())
        {
            foreach (var label in selected)
            {
                last = _printService.Print(label.Content, copies);
                if (!last.Success)
                {
                    break;
                }

                printed++;
            }
        }

        Reload();
        if (last is { Success: false })
        {
            SetStatus(printed > 0 ? $"Wydrukowano {printed} z {selected.Count}. {last.Message}" : last.Message, StatusKind.Error);
        }
        else if (selected.Count == 1 && last is not null)
        {
            SetStatus(last.Message, last.Kind);
        }
        else
        {
            SetStatus(
                $"{DateTime.Now:HH:mm:ss} – wysłano do drukarki {printed} etykiet po {copies.ToString(CultureInfo.InvariantCulture)} szt.",
                last?.Kind ?? StatusKind.Success);
        }

        CopiesBox.Text = "1";
    }

    private bool Ask(string question, MessageBoxImage image) =>
        MessageBox.Show(this, question, "Baza tekstów", MessageBoxButton.YesNo, image, MessageBoxResult.No) == MessageBoxResult.Yes;

    // ---- CSV ----------------------------------------------------------------------------------------------

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import tekstów z pliku CSV",
            Filter = "Pliki CSV (*.csv;*.txt)|*.csv;*.txt|Wszystkie pliki (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        CsvImportResult result;
        try
        {
            using (new WaitCursor())
            {
                result = _store.ImportCsv(dialog.FileName);
            }
        }
        catch (IOException ex)
        {
            ShowError($"Nie udało się odczytać pliku. Jeśli jest otwarty w Excelu, zamknij go i spróbuj ponownie.\n\n{ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            ShowError("Import nie powiódł się: " + ex.Message);
            return;
        }

        Reload();
        SetStatus($"Zaimportowano plik {Path.GetFileName(dialog.FileName)}: dodano {result.Added}, zaktualizowano {result.Updated}.",
            result.Issues.Count > 0 ? StatusKind.Warning : StatusKind.Success);
        MessageBox.Show(this, CsvImportReport.Format(result), "Import zakończony", MessageBoxButton.OK,
            result.Issues.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Eksport bazy tekstów do pliku CSV",
            Filter = "Plik CSV (*.csv)|*.csv",
            DefaultExt = ".csv",
            AddExtension = true,
            FileName = $"etykiety-{DateTime.Now:yyyy-MM-dd}.csv",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            using (new WaitCursor())
            {
                _store.ExportCsv(dialog.FileName);
            }

            SetStatus($"Zapisano {_store.Count()} tekstów do pliku {dialog.FileName}.", StatusKind.Success);
        }
        catch (Exception ex)
        {
            ShowError($"Nie udało się zapisać pliku. Jeśli jest otwarty w Excelu, zamknij go i spróbuj ponownie.\n\n{ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        SetStatus(message.Split('\n')[0], StatusKind.Error);
        MessageBox.Show(this, message, "Baza tekstów", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void SetStatus(string message, StatusKind kind) =>
        StatusPresenter.Show(StatusBorder, StatusText, message, kind);
}
