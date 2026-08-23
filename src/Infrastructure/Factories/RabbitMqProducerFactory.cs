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

public sealed class RabbitMqProducerFactory : IMessageProducerFactory
{
    private readonly ILogger<RabbitMqProducerFactory> logger;
    private readonly IOptionsMonitor<AppConfiguration> config;
    private readonly IServiceProvider serviceProvider;


    public RabbitMqProducerFactory(
        ILogger<RabbitMqProducerFactory> logger,
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
        // TODO: have same logic as other services can be base class
        logger.LogInformation("Creating producer for RabbitMQ configId: {ConfigId}", configId);
        var rabbitConfig = config.CurrentValue.RabbitMqConfigs.First(x => x.Id == configId);
        var rabbitProducerProvider = ActivatorUtilities.CreateInstance<RabbitMqProducerProvider>(
            serviceProvider, rabbitConfig
        );
        var textProcessingPipeline = GetTextProcessingPipeline(rabbitConfig);

        var msgOptions = new MessageOptions
        {
            UseGzipCompression = rabbitConfig.UseGzipCompression,
            UseBase64Coding = rabbitConfig.UseBase64Coding,
            TextProcessingPipeline = textProcessingPipeline
        };

        if (msgOptions is { UseGzipCompression: true, UseBase64Coding: false })
            return ActivatorUtilities.CreateInstance<BytesMessageProducer>(
                serviceProvider, rabbitProducerProvider, msgOptions
            );

        return ActivatorUtilities.CreateInstance<StringMessageProducer>(
            serviceProvider, rabbitProducerProvider, msgOptions
        );
    }


    private ITextProcessingPipeline GetTextProcessingPipeline(RabbitMqConfig rabbitConfig)
    {
        // TODO: have same logic as other services can be base class
        var activeMessageFormatters = GetActiveMessageFormatters(rabbitConfig);
        var textProcessingPipeline = serviceProvider.GetRequiredService<ITextProcessingPipeline>();
        textProcessingPipeline.AddFormatters(activeMessageFormatters);

        return textProcessingPipeline;
    }

    private IMessageFormatter[] GetActiveMessageFormatters(RabbitMqConfig rabbitConfig)
    {
        // TODO: have same logic as other services can be base class
        var messageFormattersNames = rabbitConfig
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
