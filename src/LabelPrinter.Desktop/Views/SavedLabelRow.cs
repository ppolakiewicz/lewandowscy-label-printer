using LabelPrinter.Core.Presentation;
using LabelPrinter.Core.Storage;

namespace LabelPrinter.Desktop.Views;

/// <summary>One row of the database grid (public for WPF bindings).</summary>
public sealed class SavedLabelRow(SavedLabel label)
{
    public SavedLabel Label { get; } = label;

    public long Id => Label.Id;

    public string Display { get; } = LabelTextFormatter.OneLine(label.Content);

    public string ModeName { get; } = LabelTextFormatter.ModeName(label.Content.Mode);

    public int UseCount => Label.UseCount;

    /// <summary>Sort key for the "last used" column (never used = oldest).</summary>
    public DateTime LastUsedSort => Label.LastUsedUtc ?? DateTime.MinValue;

    public string LastUsedText { get; } = label.LastUsedUtc is null ? "nigdy" : LabelTextFormatter.FormatLocal(label.LastUsedUtc);

    public string Note => Label.Note ?? "";
}
