namespace Domain.Configs;

public class EventHubConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Title { get; set; }
    public required string ConnectionString { get; set; }
    public required string Name { get; set; }
    public bool UseGzipCompression { get; set; }
    public bool UseBase64Coding { get; set; }
    public bool UseCheckpoints { get; set; }
    public BlobConfig? StorageConfig { get; set; }
}