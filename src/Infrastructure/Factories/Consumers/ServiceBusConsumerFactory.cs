using Domain.Configs;
using Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Factories.Consumers;

public sealed class ServiceBusConsumerFactory : BaseConsumerFactory
{
    private readonly ILogger<ServiceBusConsumerFactory> logger;
    private readonly IOptionsMonitor<AppConfiguration> config;
    private readonly IServiceProvider serviceProvider;


    public ServiceBusConsumerFactory(
        ILogger<ServiceBusConsumerFactory> logger,
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
        logger.LogInformation("Creating consumer for ServiceBus configId: {ConfigId}", configId);
        var sbConfig = config.CurrentValue.ServiceBusConfigs.First(x => x.Id == configId);

        var sbConsumerProvider = ActivatorUtilities.CreateInstance<ServiceBusConsumerProvider>(
            serviceProvider, sbConfig
        );

        return new ConsumerConfig(sbConfig, sbConsumerProvider);
    }
}
