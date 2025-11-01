using Application.Services;
using Domain.Configs;
using Domain.Enums;
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
        logger.LogInformation("Creating consumer for configId: {ConfigId}", configId);
        var eventHubConfig = config.CurrentValue.EventHubsConfigs.First(x => x.Id == configId);

        IMessageConsumerProvider ehConsumerProvider = eventHubConfig.UseCheckpoints
            ? CreateConsumerWithStorage(eventHubConfig) 
            : CreateConsumerWithoutStorage(eventHubConfig);

        var textProcessingPipeline = GetTextProcessingPipeline(eventHubConfig);

        return ActivatorUtilities.CreateInstance<EventHubConsumerService>(
                serviceProvider, ehConsumerProvider, textProcessingPipeline
        );
    }
    
    private EventHubConsumerProviderWithStorage CreateConsumerWithStorage(EventHubConfig eventHubConfig)
    {
        return ActivatorUtilities.CreateInstance<EventHubConsumerProviderWithStorage>(serviceProvider, eventHubConfig);
    }
    
    private EventHubConsumerProviderWithoutStorage CreateConsumerWithoutStorage(EventHubConfig eventHubConfig)
    {
        return ActivatorUtilities.CreateInstance<EventHubConsumerProviderWithoutStorage>(serviceProvider, eventHubConfig);
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
            .ToHashSet();

        var messageFormattersList = serviceProvider
            .GetServices<IMessageFormatter>()
            .Where(w => 
                w.Type == MessageFormatterType.AfterReceive
                && ehMessageFormattersNames.Contains(w.Name)
            ).ToArray();
        
        return messageFormattersList;
    }
}