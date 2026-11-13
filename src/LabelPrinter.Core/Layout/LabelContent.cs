namespace LabelPrinter.Core.Layout;

public enum LayoutMode
{
    /// <summary>One text, wrapped automatically (after a space, '-' or '_') into at most three lines.</summary>
    Auto,

    /// <summary>Two or three lines entered separately; no further wrapping.</summary>
    Manual,
}

public enum LabelAlignment
{
    Center,
    Left,
}

/// <summary>What the user wants on one label. Shared by the layout engine, the text database and the UI.</summary>
public sealed record LabelContent
{
    public const int MaxLines = 3;
    public const string DefaultFontFamily = "Arial";

    public LayoutMode Mode { get; init; } = LayoutMode.Auto;

    /// <summary>Auto: exactly one entry (the whole text). Manual: one entry per line, at most <see cref="MaxLines"/>.</summary>
    public IReadOnlyList<string> Lines { get; init; } = [];

    public LabelAlignment Alignment { get; init; } = LabelAlignment.Center;

    public bool Bold { get; init; }

    /// <summary>Font size in printer dots (8 per mm); null means the largest size that fits.</summary>
    public double? FontSizeDots { get; init; }

    public string FontFamily { get; init; } = DefaultFontFamily;

    /// <summary>The text as one string, lines joined with '\n' and trimmed; used as the database key and for display.</summary>
    public string Text => string.Join('\n', Lines.Select(l => l.Trim()).Where(l => l.Length > 0));

    public bool IsEmpty => Text.Length == 0;

    public static LabelContent FromText(string text) => new() { Mode = LayoutMode.Auto, Lines = [text] };
}
