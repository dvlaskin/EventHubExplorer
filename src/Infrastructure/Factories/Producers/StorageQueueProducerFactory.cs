using Domain.Configs;
using Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Factories.Producers;

public sealed class StorageQueueProducerFactory : BaseProducerFactory
{
    private readonly ILogger<StorageQueueProducerFactory> logger;
    private readonly IOptionsMonitor<AppConfiguration> config;
    private readonly IServiceProvider serviceProvider;


    public StorageQueueProducerFactory(
        ILogger<StorageQueueProducerFactory> logger,
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
        logger.LogInformation("Creating producer for StorageQueue configId: {ConfigId}", configId);
        var queueConfig = config.CurrentValue.StorageQueuesConfigs.First(x => x.Id == configId);
        var queueProducerProvider = ActivatorUtilities.CreateInstance<StorageQueueProducerProvider>(
            serviceProvider, queueConfig
        );

        return new ProducerConfig(
            queueConfig, queueProducerProvider, queueConfig.UseGzipCompression, queueConfig.UseBase64Coding
        );
    }
}
