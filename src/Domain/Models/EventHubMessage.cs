namespace Domain.Models;

public class EventHubMessage
{
    public string? Message { get; set; }
    public DateTime SenderDate { get; set; }
    public int PartitionId { get; set; }
    public int SequenceNumber { get; set; }
}