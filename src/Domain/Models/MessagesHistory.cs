namespace Domain.Models;

/// <summary>
/// Persisted message history per configuration.
/// </summary>
public sealed class MessagesHistory
{
    /// <summary>
    /// Storage format version. Default <c>0</c> means legacy file without version field;
    /// new format is always written with <c>2</c> by the history service/provider.
    /// </summary>
    public int Version { get; set; } = 0;

    /// <summary>History entries per configuration.</summary>
    public Dictionary<Guid, List<MessageHistoryRecord>> Messages { get; set; } = new();
}