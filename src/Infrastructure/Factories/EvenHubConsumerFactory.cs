using Azure.Storage.Blobs;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Interfaces.Factories;
using Domain.Interfaces.Providers;
using Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Factories;

public class EvenHubConsumerFactory : IMessageConsumerFactory
{
    private readonly ILogger<EvenHubConsumerFactory> logger;
    private readonly AppConfiguration config;
    private readonly IServiceProvider serviceProvider;

    public EvenHubConsumerFactory(
        ILogger<EvenHubConsumerFactory> logger, 
        IOptionsMonitor<AppConfiguration> config,
        IServiceProvider serviceProvider
    )
    {
        this.logger = logger;
        this.config = config.CurrentValue;
        this.serviceProvider = serviceProvider;
    }
    
    public IMessageConsumerProvider CreateConsumer(Guid configId)
    {
        logger.LogInformation("Creating producer for configId: {ConfigId}", configId);
        var eventHubConfig = config.EventHubsConfigs.First(x => x.Id == configId);

        if (eventHubConfig.UseCheckpoints)
        {
            return CreateConsumerWithStorage(eventHubConfig);
        }

        return CreateConsumerWithoutStorage(eventHubConfig);
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