namespace Domain.Models;

public sealed class IncomingMessage
{
    public string? Message { get; set; }
    public DateTimeOffset EnqueuedTime { get; set; }
    public string? PartitionId { get; set; }
    public long SequenceNumber { get; set; }
    public IReadOnlyDictionary<string, object>? Properties { get; set; }
}