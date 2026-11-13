using LabelPrinter.Core.Presentation;
using LabelPrinter.Core.Storage;

namespace LabelPrinter.Desktop.Controls;

/// <summary>One row of the suggestion list (public for WPF bindings).</summary>
public sealed class SuggestionItem(SavedLabel label)
{
    public SavedLabel Label { get; } = label;

    /// <summary>The text on one row, lines separated with " ⏎ ".</summary>
    public string Display { get; } = LabelTextFormatter.OneLine(label.Content);

    /// <summary>Mode, non-default settings and use count.</summary>
    public string Details { get; } = LabelTextFormatter.Details(label);
}
