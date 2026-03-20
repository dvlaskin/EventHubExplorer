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

public class ServiceBusProducerFactory : IMessageProducerFactory
{
    private readonly ILogger<ServiceBusProducerFactory> logger;
    private readonly IOptionsMonitor<AppConfiguration> config;
    private readonly IServiceProvider serviceProvider;

    public ServiceBusProducerFactory(
        ILogger<ServiceBusProducerFactory> logger,
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
        logger.LogInformation("Creating producer for ServiceBus configId: {ConfigId}", configId);
        var sbConfig = config.CurrentValue.ServiceBusConfigs.First(x => x.Id == configId);
        var sbProducerProvider = ActivatorUtilities.CreateInstance<ServiceBusProducerProvider>(
            serviceProvider, sbConfig
        );
        var textProcessingPipeline = GetTextProcessingPipeline(sbConfig);

        var msgOptions = new MessageOptions
        {
            UseGzipCompression = sbConfig.UseGzipCompression,
            UseBase64Coding = sbConfig.UseBase64Coding,
            TextProcessingPipeline = textProcessingPipeline
        };

        if (msgOptions is { UseGzipCompression: true, UseBase64Coding: false })
            return ActivatorUtilities.CreateInstance<BytesMessageProducer>(
                serviceProvider, sbProducerProvider, msgOptions
            );

        return ActivatorUtilities.CreateInstance<StringMessageProducer>(
            serviceProvider, sbProducerProvider, msgOptions
        );
    }

    private ITextProcessingPipeline GetTextProcessingPipeline(ServiceBusConfig sbConfig)
    {
        var activeMessageFormatters = GetActiveMessageFormatters(sbConfig);
        var textProcessingPipeline = serviceProvider.GetRequiredService<ITextProcessingPipeline>();
        textProcessingPipeline.AddFormatters(activeMessageFormatters);

        return textProcessingPipeline;
    }

    private IMessageFormatter[] GetActiveMessageFormatters(ServiceBusConfig sbConfig)
    {
        var messageFormattersNames = sbConfig
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
