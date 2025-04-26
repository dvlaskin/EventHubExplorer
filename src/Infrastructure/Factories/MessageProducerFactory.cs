using Application.Services;
using Domain.Configs;
using Domain.Interfaces.Factories;
using Domain.Interfaces.Services;
using Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Factories;

public class MessageProducerFactory : IMessageProducerFactory
{
    private readonly ILogger<MessageProducerFactory> logger;
    private readonly AppConfiguration config;
    private readonly IServiceProvider serviceProvider;

    public MessageProducerFactory(
        ILogger<MessageProducerFactory> logger, 
        IOptionsMonitor<AppConfiguration> config,
        IServiceProvider serviceProvider
    )
    {
        this.logger = logger;
        this.config = config.CurrentValue;
        this.serviceProvider = serviceProvider;
    }

    public IMessageProducerService CreateProducerService(Guid configId)
    {
        logger.LogInformation("Creating producer for configId: {ConfigId}", configId);
        var eventHubConfig = config.EventHubsConfigs.First(x => x.Id == configId);
        var ehProducerProvider = ActivatorUtilities.CreateInstance<EventHubProducerProvider>(
            serviceProvider, eventHubConfig);
        
        return ActivatorUtilities.CreateInstance<MessageProducerService>(
            serviceProvider, ehProducerProvider);
    }
}