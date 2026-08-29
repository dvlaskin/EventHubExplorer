using Application.Services.MessageProducers;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Interfaces.Factories;
using Domain.Interfaces.Providers;
using Domain.Interfaces.Services;
using Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Factories.Producers;

public abstract class BaseProducerFactory : IMessageProducerFactory
{
    private readonly IServiceProvider serviceProvider;


    protected BaseProducerFactory(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }


    public IMessageProducerService CreateProducer(Guid configId)
    {
        var producerConfig = GetProducerConfig(configId);
        var textProcessingPipeline = GetTextProcessingPipeline(producerConfig.FormattingConfig);

        var msgOptions = new MessageOptions
        {
            UseGzipCompression = producerConfig.UseGzipCompression,
            UseBase64Coding = producerConfig.UseBase64Coding,
            TextProcessingPipeline = textProcessingPipeline
        };

        if (msgOptions is { UseGzipCompression: true, UseBase64Coding: false })
        {
            return ActivatorUtilities.CreateInstance<BytesMessageProducer>(
                serviceProvider, producerConfig.ProducerProvider, msgOptions
            );
        }

        return ActivatorUtilities.CreateInstance<StringMessageProducer>(
            serviceProvider, producerConfig.ProducerProvider, msgOptions
        );
    }

    protected abstract ProducerConfig GetProducerConfig(Guid configId);


    private ITextProcessingPipeline GetTextProcessingPipeline(IFormattingConfig formattingConfig)
    {
        var activeMessageFormatters = GetActiveMessageFormatters(formattingConfig);
        var textProcessingPipeline = serviceProvider.GetRequiredService<ITextProcessingPipeline>();
        textProcessingPipeline.AddFormatters(activeMessageFormatters);

        return textProcessingPipeline;
    }

    private IMessageFormatter[] GetActiveMessageFormatters(IFormattingConfig formattingConfig)
    {
        var messageFormattersNames = formattingConfig
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


    protected sealed record ProducerConfig
    (
        IFormattingConfig FormattingConfig,
        IMessageProducerProvider ProducerProvider,
        bool UseGzipCompression,
        bool UseBase64Coding
    );
}
