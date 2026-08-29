using Domain.Configs;
using Domain.Interfaces.Providers;
using Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Factories.Consumers;

public sealed class EventHubConsumerFactory : BaseConsumerFactory
{
    private readonly ILogger<EventHubConsumerFactory> logger;
    private readonly IOptionsMonitor<AppConfiguration> config;
    private readonly IServiceProvider serviceProvider;


    public EventHubConsumerFactory(
        ILogger<EventHubConsumerFactory> logger,
        IOptionsMonitor<AppConfiguration> config,
        IServiceProvider serviceProvider
    ) : base(serviceProvider)
    {
        this.logger = logger;
        this.config = config;
        this.serviceProvider = serviceProvider;
    }


    protected override ConsumerConfig GetConsumerConfig(Guid configId)
    {
        logger.LogInformation("Creating consumer for configId: {ConfigId}", configId);
        var eventHubConfig = config.CurrentValue.EventHubsConfigs.First(x => x.Id == configId);

        IMessageConsumerProvider ehConsumerProvider = eventHubConfig.UseCheckpoints
            ? CreateConsumerWithStorage(eventHubConfig)
            : CreateConsumerWithoutStorage(eventHubConfig);

        return new ConsumerConfig(eventHubConfig, ehConsumerProvider);
    }


    private EventHubConsumerProviderWithStorage CreateConsumerWithStorage(EventHubConfig eventHubConfig)
    {
        return ActivatorUtilities.CreateInstance<EventHubConsumerProviderWithStorage>(serviceProvider, eventHubConfig);
    }

    private EventHubConsumerProviderWithoutStorage CreateConsumerWithoutStorage(EventHubConfig eventHubConfig)
    {
        return ActivatorUtilities.CreateInstance<EventHubConsumerProviderWithoutStorage>(serviceProvider, eventHubConfig);
    }
}