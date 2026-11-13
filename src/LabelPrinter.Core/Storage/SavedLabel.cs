using LabelPrinter.Core.Layout;

namespace LabelPrinter.Core.Storage;

/// <summary>One entry of the saved-text database: a label the user printed or added in the editor.</summary>
public sealed record SavedLabel
{
    /// <summary>Database id (stable while the entry exists).</summary>
    public required long Id { get; init; }

    /// <summary>The text and its layout settings (the ones used most recently).</summary>
    public required LabelContent Content { get; init; }

    /// <summary>Optional free-text remark shown only in the database editor.</summary>
    public string? Note { get; init; }

    /// <summary>How many times the label was printed (see <see cref="LabelTextStore.RecordUse"/>).</summary>
    public int UseCount { get; init; }

    /// <summary>When the entry was created (UTC).</summary>
    public DateTime CreatedUtc { get; init; }

    /// <summary>When the label was last printed (UTC); null if it was only added in the editor and never printed.</summary>
    public DateTime? LastUsedUtc { get; init; }

    /// <summary>Shortcut for <see cref="LabelContent.Text"/>.</summary>
    public string Text => Content.Text;
}
