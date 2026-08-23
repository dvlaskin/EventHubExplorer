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

public sealed class RabbitMqConsumerFactory : IMessageConsumerFactory
{
    private readonly ILogger<RabbitMqConsumerFactory> logger;
    private readonly IOptionsMonitor<AppConfiguration> config;
    private readonly IServiceProvider serviceProvider;


    public RabbitMqConsumerFactory(
        ILogger<RabbitMqConsumerFactory> logger,
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
        // TODO: have same logic as other services can be base class
        logger.LogInformation("Creating consumer for RabbitMQ configId: {ConfigId}", configId);
        var rabbitConfig = config.CurrentValue.RabbitMqConfigs.First(x => x.Id == configId);

        var rabbitConsumerProvider = ActivatorUtilities.CreateInstance<RabbitMqConsumerProvider>(
            serviceProvider, rabbitConfig
        );

        var textProcessingPipeline = GetTextProcessingPipeline(rabbitConfig);

        return ActivatorUtilities.CreateInstance<MessageConsumerService>(
            serviceProvider, rabbitConsumerProvider, textProcessingPipeline
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
