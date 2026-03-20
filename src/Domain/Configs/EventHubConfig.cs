using Domain.Interfaces;

namespace Domain.Configs;

public class EventHubConfig : IFormattingConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Title { get; set; }
    public required string ConnectionString { get; set; }
    public required string Name { get; set; }
    public bool UseCheckpoints { get; set; }
    public BlobConfig? StorageConfig { get; set; }
    
    public bool UseGzipCompression { get; set; }
    public bool UseBase64Coding { get; set; }
    public Dictionary<string, bool> MessageFormatters { get; set; } = new();
}