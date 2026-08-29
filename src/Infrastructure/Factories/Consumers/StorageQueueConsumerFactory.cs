using Domain.Configs;
using Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Factories.Consumers;

public sealed class StorageQueueConsumerFactory : BaseConsumerFactory
{
    private readonly ILogger<StorageQueueConsumerFactory> logger;
    private readonly IOptionsMonitor<AppConfiguration> config;
    private readonly IServiceProvider serviceProvider;


    public StorageQueueConsumerFactory(
        ILogger<StorageQueueConsumerFactory> logger,
        IOptionsMonitor<AppConfiguration> config,
        IServiceProvider serviceProvider
    ) : base(serviceProvider)
    {
        this.logger = logger;
        this.config = config;
        this.serviceProvider = serviceProvider;
    }


    protected override ConsumerConfig GetConsumerConfig(Guid configId)
    {
        logger.LogInformation("Creating consumer for StorageQueue configId: {ConfigId}", configId);
        var queueConfig = config.CurrentValue.StorageQueuesConfigs.First(x => x.Id == configId);

        var queueConsumerProvider = ActivatorUtilities.CreateInstance<StorageQueueConsumerProvider>(
            serviceProvider, queueConfig
        );

        return new ConsumerConfig(queueConfig, queueConsumerProvider);
    }
}
