using Domain.Configs;
using Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Factories.Producers;

public sealed class ServiceBusProducerFactory : BaseProducerFactory
{
    private readonly ILogger<ServiceBusProducerFactory> logger;
    private readonly IOptionsMonitor<AppConfiguration> config;
    private readonly IServiceProvider serviceProvider;


    public ServiceBusProducerFactory(
        ILogger<ServiceBusProducerFactory> logger,
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
        logger.LogInformation("Creating producer for ServiceBus configId: {ConfigId}", configId);
        var sbConfig = config.CurrentValue.ServiceBusConfigs.First(x => x.Id == configId);
        var sbProducerProvider = ActivatorUtilities.CreateInstance<ServiceBusProducerProvider>(
            serviceProvider, sbConfig
        );

        return new ProducerConfig(
            sbConfig, sbProducerProvider, sbConfig.UseGzipCompression, sbConfig.UseBase64Coding
        );
    }
}
