using Domain.Configs;
using Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Factories.Producers;

public sealed class RabbitMqProducerFactory : BaseProducerFactory
{
    private readonly ILogger<RabbitMqProducerFactory> logger;
    private readonly IOptionsMonitor<AppConfiguration> config;
    private readonly IServiceProvider serviceProvider;


    public RabbitMqProducerFactory(
        ILogger<RabbitMqProducerFactory> logger,
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
        logger.LogInformation("Creating producer for RabbitMQ configId: {ConfigId}", configId);
        var rabbitConfig = config.CurrentValue.RabbitMqConfigs.First(x => x.Id == configId);
        var rabbitProducerProvider = ActivatorUtilities.CreateInstance<RabbitMqProducerProvider>(
            serviceProvider, rabbitConfig
        );

        return new ProducerConfig(
            rabbitConfig, rabbitProducerProvider, rabbitConfig.UseGzipCompression, rabbitConfig.UseBase64Coding
        );
    }
}
