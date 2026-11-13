using System.Windows;
using LabelPrinter.Core.Layout;
using LabelPrinter.Core.Storage;

namespace LabelPrinter.Desktop.Views;

/// <summary>
/// Adds or edits one database entry: text, layout options and note, with a small preview. Saving is done by the
/// caller's <c>save</c> callback, which may ask about duplicates and return null to keep the dialog open.
/// </summary>
public partial class LabelEditDialog : Window
{
    private readonly AppState _state;
    private readonly Func<LabelContent, string?, SavedLabel?> _save;

    internal LabelEditDialog(AppState state, string title, LabelContent? content, string? note,
        Func<LabelContent, string?, SavedLabel?> save)
    {
        _state = state;
        _save = save;
        InitializeComponent();
        AppIcon.ApplyTo(this);
        Title = title;

        if (content is not null)
        {
            Editor.SetContent(content);
        }

        NoteBox.Text = note ?? "";
        Editor.ContentChanged += (_, _) => SchedulePreview();
        Editor.SubmitRequested += (_, _) => Save();
        Preview.Rendered += (_, rendered) => Editor.ShowLayout(rendered.Layout);
        Loaded += (_, _) => Editor.FocusText(selectAll: false);
        SchedulePreview();
    }

    /// <summary>The saved entry after the dialog closed with OK.</summary>
    public SavedLabel? Saved { get; private set; }

    private void SchedulePreview() =>
        Preview.Schedule(Editor.CurrentContent, _state.Config, _state.Renderer, Editor.InputError);

    private void Save_Click(object sender, RoutedEventArgs e) => Save();

    private void Save()
    {
        var content = Editor.CurrentContent;
        if (content.IsEmpty)
        {
            MessageBox.Show(this, "Wpisz tekst etykiety.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            Editor.FocusText(selectAll: false);
            return;
        }

        if (Editor.InputError is { } error)
        {
            MessageBox.Show(this, error, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SavedLabel? saved;
        try
        {
            saved = _save(content, NoteBox.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Nie udało się zapisać: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (saved is null)
        {
            return;
        }

        Saved = saved;
        DialogResult = true;
    }
}
