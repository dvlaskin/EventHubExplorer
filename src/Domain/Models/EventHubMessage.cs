namespace Domain.Models;

public class EventHubMessage
{
    public string? Message { get; set; }
    public DateTimeOffset EnqueuedTime { get; set; }
    public string? PartitionId { get; set; }
    public long SequenceNumber { get; set; }
}