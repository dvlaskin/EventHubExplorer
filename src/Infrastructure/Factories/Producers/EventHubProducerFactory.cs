using Domain.Configs;
using Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Factories.Producers;

public sealed class EventHubProducerFactory : BaseProducerFactory
{
    private readonly ILogger<EventHubProducerFactory> logger;
    private readonly IOptionsMonitor<AppConfiguration> config;
    private readonly IServiceProvider serviceProvider;


    public EventHubProducerFactory(
        ILogger<EventHubProducerFactory> logger,
        IOptionsMonitor<AppConfiguration> config,
        IServiceProvider serviceProvider
    ) : base(serviceProvider)
    {
        this.logger = logger;
        this.config = config;
        this.serviceProvider = serviceProvider;
    }


    protected override ProducerConfig GetProducerConfig(Guid configId)
    {
        logger.LogInformation("Creating producer for configId: {ConfigId}", configId);
        var eventHubConfig = config.CurrentValue.EventHubsConfigs.First(x => x.Id == configId);
        var ehProducerProvider = ActivatorUtilities.CreateInstance<EventHubProducerProvider>(
            serviceProvider, eventHubConfig
        );

        return new ProducerConfig(
            eventHubConfig, ehProducerProvider, eventHubConfig.UseGzipCompression, eventHubConfig.UseBase64Coding
        );
    }
}