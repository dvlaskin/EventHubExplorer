using Domain.Entities;
using Domain.Interfaces;
using Domain.Interfaces.Factories;
using Domain.Interfaces.Providers;
using Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Factories;

public class EvenHubProducerFactory : IMessageProducerFactory
{
    private readonly ILogger<EvenHubProducerFactory> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly AppConfiguration config;

    public EvenHubProducerFactory(
        ILogger<EvenHubProducerFactory> logger, 
        IOptionsMonitor<AppConfiguration> config,
        IServiceProvider serviceProvider
    )
    {
        this.logger = logger;
        this.serviceProvider = serviceProvider;
        this.config = config.CurrentValue;
    }
    
    public IMessageProducerProvider CreateProducer(Guid configId)
    {
        logger.LogInformation("Creating producer for configId: {ConfigId}", configId);
        var eventHubConfig = config.EventHubsConfigs.First(x => x.Id == configId);
        var ehProducerLogger = serviceProvider.GetRequiredService<ILogger<EventHubProducerProvider>>();
        return new EventHubProducerProvider(ehProducerLogger, eventHubConfig);
    }
}