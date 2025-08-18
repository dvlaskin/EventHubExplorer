using Application.Services.MessageProducers;
using Domain.Configs;
using Domain.Enums;
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
        var textProcessingPipeline = GetTextProcessingPipeline(eventHubConfig);

        var msgOptions = new MessageOptions
        {
            UseGzipCompression = eventHubConfig.UseGzipCompression,
            UseBase64Coding = eventHubConfig.UseBase64Coding,
            TextProcessingPipeline = textProcessingPipeline
        };
        
        if (msgOptions is { UseGzipCompression: true, UseBase64Coding: false })
            return ActivatorUtilities.CreateInstance<BytesMessageProducer>(
                serviceProvider, ehProducerProvider, msgOptions
            );
        
        return ActivatorUtilities.CreateInstance<StringMessageProducer>(
            serviceProvider, ehProducerProvider, msgOptions
        );
    }

    


    private ITextProcessingPipeline GetTextProcessingPipeline(EventHubConfig eventHubConfig)
    {
        var activeMessageFormatters = GetActiveMessageFormatters(eventHubConfig);
        var textProcessingPipeline = serviceProvider.GetRequiredService<ITextProcessingPipeline>();
        textProcessingPipeline.AddFormatters(activeMessageFormatters);
        
        return textProcessingPipeline;
    }
    
    private IMessageFormatter[] GetActiveMessageFormatters(EventHubConfig eventHubConfig)
    {
        var ehMessageFormattersNames = eventHubConfig
            .MessageFormatters
            .Where(x => x.Value)
            .Select(s => s.Key)
            .ToArray();

        var messageFormattersList = serviceProvider
            .GetServices<IMessageFormatter>()
            .Where(w => 
                w.Type == MessageFormatterType.BeforeSend
                && ehMessageFormattersNames.Contains(w.Name)
            ).ToArray();
        
        return messageFormattersList;
    }
}