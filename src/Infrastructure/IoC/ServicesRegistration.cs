using Azure.Storage.Blobs;
using Domain.Entities;
using Domain.Interfaces.Factories;
using Domain.Interfaces.Providers;
using Infrastructure.Factories;
using Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.IoC;

public static class ServicesRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddMemoryCache();

        services.AddSingleton<ICacheProvider, InMemoryCacheProvider>();
        services.AddSingleton<IConfigProvider, ConfigProvider>();
        
        services.AddSingleton<IMessageProducerFactory, EvenHubProducerFactory>();
        services.AddSingleton<IMessageConsumerFactory, EvenHubConsumerFactory>();
        services.AddSingleton<IStorageClientFactory<BlobConfig, BlobContainerClient>, BlobStorageFactory>();
        
        return services;
    }
}