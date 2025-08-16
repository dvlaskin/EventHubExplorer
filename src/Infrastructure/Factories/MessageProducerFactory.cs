using Application.Services.MessageProducers;
using Domain.Configs;
using Domain.Interfaces.Factories;
using Domain.Interfaces.Services;
using Domain.Models;
using Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Factories;

public class MessageProducerFactory : IMessageProducerFactory
{
    private readonly ILogger<MessageProducerFactory> logger;
    private readonly IOptionsMonitor<AppConfiguration> config;
    private readonly IServiceProvider serviceProvider;

    public MessageProducerFactory(
        ILogger<MessageProducerFactory> logger, 
        IOptionsMonitor<AppConfiguration> config,
        IServiceProvider serviceProvider
    )
    {
        this.logger = logger;
        this.config = config;
        this.serviceProvider = serviceProvider;
    }

    public IMessageProducerService CreateProducerService(Guid configId)
    {
        logger.LogInformation("Creating producer for configId: {ConfigId}", configId);
        var eventHubConfig = config.CurrentValue.EventHubsConfigs.First(x => x.Id == configId);
        var ehProducerProvider = ActivatorUtilities.CreateInstance<EventHubProducerProvider>(
            serviceProvider, eventHubConfig
        );
        var msgOptions = new MessageOptions
        {
            UseGzipCompression = eventHubConfig.UseGzipCompression,
            UseBase64Coding = eventHubConfig.UseBase64Coding
        };
        
        if (msgOptions is { UseGzipCompression: true, UseBase64Coding: false })
            return ActivatorUtilities.CreateInstance<BytesMessageProducer>(
                serviceProvider, ehProducerProvider, msgOptions
            );
        
        return ActivatorUtilities.CreateInstance<StringMessageProducer>(
            serviceProvider, ehProducerProvider, msgOptions
        );
    }
}