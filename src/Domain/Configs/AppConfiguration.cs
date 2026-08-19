namespace Domain.Configs;

public sealed class AppConfiguration
{
    public List<EventHubConfig> EventHubsConfigs { get; set; } = [];
    public List<StorageQueueConfig> StorageQueuesConfigs { get; set; } = [];
    public List<ServiceBusConfig> ServiceBusConfigs { get; set; } = [];
    public List<RabbitMqConfig> RabbitMqConfigs { get; set; } = [];
}
