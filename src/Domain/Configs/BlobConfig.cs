namespace Domain.Configs;

public class BlobConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public string BlobContainerName { get; set; } = string.Empty;
}