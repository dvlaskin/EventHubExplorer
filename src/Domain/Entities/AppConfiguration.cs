namespace Domain.Entities;

public class AppConfiguration
{
    public List<EventHubConfig> EventHubsConfigs { get; set; } = new();
}