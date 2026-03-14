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

public class ServiceBusConsumerFactory : IMessageConsumerFactory
{
    private readonly ILogger<ServiceBusConsumerFactory> logger;
    private readonly IOptionsMonitor<AppConfiguration> config;
    private readonly IServiceProvider serviceProvider;

    public ServiceBusConsumerFactory(
        ILogger<ServiceBusConsumerFactory> logger,
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
        logger.LogInformation("Creating consumer for ServiceBus configId: {ConfigId}", configId);
        var sbConfig = config.CurrentValue.ServiceBusConfigs.First(x => x.Id == configId);

        var sbConsumerProvider = ActivatorUtilities.CreateInstance<ServiceBusConsumerProvider>(
            serviceProvider, sbConfig
        );

        var textProcessingPipeline = GetTextProcessingPipeline(sbConfig);

        return ActivatorUtilities.CreateInstance<EventHubConsumerService>(
            serviceProvider, sbConsumerProvider, textProcessingPipeline
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
