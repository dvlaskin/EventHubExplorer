using Azure.Storage.Blobs;
using Domain.Configs;
using Domain.Interfaces.Factories;
using Domain.Interfaces.Providers;
using Domain.Models;
using Infrastructure.Factories;
using Infrastructure.Providers.FileStorageProviders;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.IoC;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IFileStorageProvider<AppConfiguration>, AppConfigurationProvider>();
        services.AddSingleton<IFileStorageProvider<MessagesHistory>, MessageHistoryProvider>();
        
        services.AddSingleton<IMessageProducerFactory, MessageProducerFactory>();
        services.AddSingleton<IMessageConsumerFactory, MessageConsumerFactory>();
        services.AddSingleton<IStorageClientFactory<BlobConfig, BlobContainerClient>, BlobStorageFactory>();
        
        return services;
    }
}