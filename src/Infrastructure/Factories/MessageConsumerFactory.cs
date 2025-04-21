using Application.Services;
using Azure.Storage.Blobs;
using Domain.Entities;
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
    private readonly AppConfiguration config;
    private readonly IServiceProvider serviceProvider;

    public MessageConsumerFactory(
        ILogger<MessageConsumerFactory> logger, 
        IOptionsMonitor<AppConfiguration> config,
        IServiceProvider serviceProvider
    )
    {
        this.logger = logger;
        this.config = config.CurrentValue;
        this.serviceProvider = serviceProvider;
    }
    
    public IMessageConsumerService CreateConsumer(Guid configId)
    {
        logger.LogInformation("Creating producer for configId: {ConfigId}", configId);
        var eventHubConfig = config.EventHubsConfigs.First(x => x.Id == configId);

        IMessageConsumerProvider ehConsumerProvider = eventHubConfig.UseCheckpoints 
            ? CreateConsumerWithStorage(eventHubConfig) 
            : CreateConsumerWithoutStorage(eventHubConfig);
        
        var ehConsumerLogger = serviceProvider.GetRequiredService<ILogger<EventHubConsumerService>>();
        return new EventHubConsumerService(ehConsumerLogger, ehConsumerProvider);
    }
    
    private EventHubConsumerProviderWithStorage CreateConsumerWithStorage(EventHubConfig eventHubConfig)
    {
        var ehProducerLogger = serviceProvider.GetRequiredService<ILogger<EventHubConsumerProviderWithStorage>>();
        var storageClientFactory = serviceProvider.GetRequiredService<IStorageClientFactory<BlobConfig, BlobContainerClient>>();
        return new EventHubConsumerProviderWithStorage(ehProducerLogger, eventHubConfig, storageClientFactory);
    }
    
    private EventHubConsumerProviderWithoutStorage CreateConsumerWithoutStorage(EventHubConfig eventHubConfig)
    {
        var ehProducerLogger = serviceProvider.GetRequiredService<ILogger<EventHubConsumerProviderWithoutStorage>>();
        return new EventHubConsumerProviderWithoutStorage(ehProducerLogger, eventHubConfig);
    }
}