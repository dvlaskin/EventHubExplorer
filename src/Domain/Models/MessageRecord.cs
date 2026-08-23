namespace Domain.Models;

public sealed class MessageRecord
{
    public string? Message { get; set; }
    public DateTimeOffset EnqueuedTime { get; set; }
    public string? PartitionId { get; set; }
    public long SequenceNumber { get; set; }
}