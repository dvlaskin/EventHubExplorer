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

public class StorageQueueProducerFactory : IMessageProducerFactory
{
    private readonly ILogger<StorageQueueProducerFactory> logger;
    private readonly IOptionsMonitor<AppConfiguration> config;
    private readonly IServiceProvider serviceProvider;

    public StorageQueueProducerFactory(
        ILogger<StorageQueueProducerFactory> logger,
        IOptionsMonitor<AppConfiguration> config,
        IServiceProvider serviceProvider
    )
    {
        this.logger = logger;
        this.config = config;
        this.serviceProvider = serviceProvider;
    }

    public IMessageProducerService CreateProducer(Guid configId)
    {
        logger.LogInformation("Creating producer for StorageQueue configId: {ConfigId}", configId);
        var queueConfig = config.CurrentValue.StorageQueuesConfigs.First(x => x.Id == configId);
        var queueProducerProvider = ActivatorUtilities.CreateInstance<StorageQueueProducerProvider>(
            serviceProvider, queueConfig
        );
        var textProcessingPipeline = GetTextProcessingPipeline(queueConfig);

        var msgOptions = new MessageOptions
        {
            UseGzipCompression = queueConfig.UseGzipCompression,
            UseBase64Coding = queueConfig.UseBase64Coding,
            TextProcessingPipeline = textProcessingPipeline
        };

        if (msgOptions is { UseGzipCompression: true, UseBase64Coding: false })
            return ActivatorUtilities.CreateInstance<BytesMessageProducer>(
                serviceProvider, queueProducerProvider, msgOptions
            );

        return ActivatorUtilities.CreateInstance<StringMessageProducer>(
            serviceProvider, queueProducerProvider, msgOptions
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
            .ToArray();

        var messageFormattersList = serviceProvider
            .GetServices<IMessageFormatter>()
            .Where(w =>
                w.Type == MessageFormatterType.BeforeSend
                && messageFormattersNames.Contains(w.Name)
            ).ToArray();

        return messageFormattersList;
    }
}
