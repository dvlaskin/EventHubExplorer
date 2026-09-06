namespace Domain.Models;

/// <summary>
/// Single message history entry: last sent body snapshot and its properties snapshot.
/// </summary>
public sealed class MessageHistoryRecord
{
    /// <summary>Stable identity of the entry, used for selection and updates.</summary>
    public Guid Id { get; set; }

    /// <summary>Message body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Last sent properties snapshot (string-to-string only).</summary>
    public Dictionary<string, string> Properties { get; set; } = new();

    /// <summary>Creation UTC time of the entry.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
