using Domain.Configs;
using Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Factories.Consumers;

public sealed class RabbitMqConsumerFactory : BaseConsumerFactory
{
    private readonly ILogger<RabbitMqConsumerFactory> logger;
    private readonly IOptionsMonitor<AppConfiguration> config;
    private readonly IServiceProvider serviceProvider;


    public RabbitMqConsumerFactory(
        ILogger<RabbitMqConsumerFactory> logger,
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
        logger.LogInformation("Creating consumer for RabbitMQ configId: {ConfigId}", configId);
        var rabbitConfig = config.CurrentValue.RabbitMqConfigs.First(x => x.Id == configId);

        var rabbitConsumerProvider = ActivatorUtilities.CreateInstance<RabbitMqConsumerProvider>(
            serviceProvider, rabbitConfig
        );

        return new ConsumerConfig(rabbitConfig, rabbitConsumerProvider);
    }
}
