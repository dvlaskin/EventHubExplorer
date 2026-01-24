namespace Domain.Configs;

public class StorageQueueConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Title { get; set; }
    public required string ConnectionString { get; set; }
    public required string QueueName { get; set; }
    public bool UseGzipCompression { get; set; }
    public bool UseBase64Coding { get; set; }
    public Dictionary<string, bool> MessageFormatters { get; set; } = new();
}
