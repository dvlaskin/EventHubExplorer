using Application.Services;
using Domain.Configs;
using Domain.Interfaces.Factories;
using Domain.Interfaces.Providers;
using Domain.Interfaces.Services;
using Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Factories;

public class MessageConsumerFactory : IMessageConsumerFactory
{
    private readonly ILogger<MessageConsumerFactory> logger;
    private readonly IOptionsMonitor<AppConfiguration> config;
    private readonly IServiceProvider serviceProvider;

    public MessageConsumerFactory(
        ILogger<MessageConsumerFactory> logger, 
        IOptionsMonitor<AppConfiguration> config,
        IServiceProvider serviceProvider
    )
    {
        this.logger = logger;
        this.config = config;
        this.serviceProvider = serviceProvider;
    }
    
    public IMessageConsumerService CreateConsumer(Guid configId)
    {
        logger.LogInformation("Creating producer for configId: {ConfigId}", configId);
        var eventHubConfig = config.CurrentValue.EventHubsConfigs.First(x => x.Id == configId);

        IMessageConsumerProvider ehConsumerProvider = eventHubConfig.UseCheckpoints 
            ? CreateConsumerWithStorage(eventHubConfig) 
            : CreateConsumerWithoutStorage(eventHubConfig);
        
        var ehConsumerLogger = serviceProvider.GetRequiredService<ILogger<EventHubConsumerService>>();
        return new EventHubConsumerService(ehConsumerLogger, ehConsumerProvider);
    }
    
    private EventHubConsumerProviderWithStorage CreateConsumerWithStorage(EventHubConfig eventHubConfig)
    {
        return ActivatorUtilities.CreateInstance<EventHubConsumerProviderWithStorage>(
            serviceProvider,
            eventHubConfig
        );
    }
    
    private EventHubConsumerProviderWithoutStorage CreateConsumerWithoutStorage(EventHubConfig eventHubConfig)
    {
        return ActivatorUtilities.CreateInstance<EventHubConsumerProviderWithoutStorage>(
            serviceProvider,
            eventHubConfig
        );
    }
}