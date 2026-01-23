using Application.Services;
using Domain.Configs;
using Domain.Enums;
using Domain.Interfaces.Factories;
using Domain.Interfaces.Services;
using Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Factories;

public class StorageQueueConsumerFactory : IMessageConsumerFactory
{
    private readonly ILogger<StorageQueueConsumerFactory> logger;
    private readonly IOptionsMonitor<AppConfiguration> config;
    private readonly IServiceProvider serviceProvider;

    public StorageQueueConsumerFactory(
        ILogger<StorageQueueConsumerFactory> logger,
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
        logger.LogInformation("Creating consumer for StorageQueue configId: {ConfigId}", configId);
        var queueConfig = config.CurrentValue.StorageQueuesConfigs.First(x => x.Id == configId);

        var queueConsumerProvider = ActivatorUtilities.CreateInstance<StorageQueueConsumerProvider>(
            serviceProvider, queueConfig
        );

        var textProcessingPipeline = GetTextProcessingPipeline(queueConfig);

        return ActivatorUtilities.CreateInstance<EventHubConsumerService>(
            serviceProvider, queueConsumerProvider, textProcessingPipeline
        );
    }

    private ITextProcessingPipeline GetTextProcessingPipeline(StorageQueueConfig queueConfig)
    {
        var activeMessageFormatters = GetActiveMessageFormatters(queueConfig);
        var textProcessingPipeline = serviceProvider.GetRequiredService<ITextProcessingPipeline>();
        textProcessingPipeline.AddFormatters(activeMessageFormatters);

        return textProcessingPipeline;
    }

    private IMessageFormatter[] GetActiveMessageFormatters(StorageQueueConfig queueConfig)
    {
        var messageFormattersNames = queueConfig
            .MessageFormatters
            .Where(x => x.Value)
            .Select(s => s.Key)
            .ToHashSet();

        var messageFormattersList = serviceProvider
            .GetServices<IMessageFormatter>()
            .Where(w =>
                w.Type == MessageFormatterType.AfterReceive
                && messageFormattersNames.Contains(w.Name)
            ).ToArray();

        return messageFormattersList;
    }
}
