namespace LabelPrinter.Core.Storage;

/// <summary>
/// Thrown by <see cref="LabelTextStore.Add"/> and <see cref="LabelTextStore.Update"/> when another entry
/// already has the same text (ignoring case and whitespace) and layout mode.
/// </summary>
public sealed class DuplicateLabelException : InvalidOperationException
{
    /// <summary>Creates the exception for the entry that already exists.</summary>
    public DuplicateLabelException(long existingId, string text)
        : base($"An entry with the same text already exists (id {existingId}): \"{text}\".")
    {
        ExistingId = existingId;
        Text = text;
    }

    /// <summary>Id of the entry that already has this text and mode.</summary>
    public long ExistingId { get; }

    /// <summary>The conflicting text.</summary>
    public string Text { get; }
}
