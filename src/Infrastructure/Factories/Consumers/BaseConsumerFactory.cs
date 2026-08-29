using Application.Services;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Interfaces.Factories;
using Domain.Interfaces.Providers;
using Domain.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Factories.Consumers;

public abstract class BaseConsumerFactory : IMessageConsumerFactory
{
    private readonly IServiceProvider serviceProvider;


    protected BaseConsumerFactory(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }


    public IMessageConsumerService CreateConsumer(Guid configId)
    {
        var consumerConfig = GetConsumerConfig(configId);
        var textProcessingPipeline = GetTextProcessingPipeline(consumerConfig.FormattingConfig);

        return ActivatorUtilities.CreateInstance<MessageConsumerService>(
            serviceProvider, consumerConfig.ConsumerProvider, textProcessingPipeline
        );
    }

    protected abstract ConsumerConfig GetConsumerConfig(Guid configId);


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
            .ToHashSet();

        var messageFormattersList = serviceProvider
            .GetServices<IMessageFormatter>()
            .Where(w =>
                w.Type == MessageFormatterType.AfterReceive
                && messageFormattersNames.Contains(w.Name)
            ).ToArray();

        return messageFormattersList;
    }


    protected sealed record ConsumerConfig
    (
        IFormattingConfig FormattingConfig,
        IMessageConsumerProvider ConsumerProvider
    );
}
