namespace Domain.Configs;

public class AppConfiguration
{
    public List<EventHubConfig> EventHubsConfigs { get; set; } = [];
    public List<StorageQueueConfig> StorageQueuesConfigs { get; set; } = [];
}