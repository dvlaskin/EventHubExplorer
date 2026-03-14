using Azure.Storage.Blobs;
using Domain.Configs;
using Domain.Enums;
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
        
        // event hub
        services.AddKeyedSingleton<IMessageProducerFactory, MessageProducerFactory>(MessageBusType.EventHub);
        services.AddKeyedSingleton<IMessageConsumerFactory, MessageConsumerFactory>(MessageBusType.EventHub);
        
        // storage queue
        services.AddKeyedSingleton<IMessageProducerFactory, StorageQueueProducerFactory>(MessageBusType.StorageQueue);
        services.AddKeyedSingleton<IMessageConsumerFactory, StorageQueueConsumerFactory>(MessageBusType.StorageQueue);
        
        // service bus
        services.AddKeyedSingleton<IMessageProducerFactory, ServiceBusProducerFactory>(MessageBusType.ServiceBus);
        services.AddKeyedSingleton<IMessageConsumerFactory, ServiceBusConsumerFactory>(MessageBusType.ServiceBus);
        
        services.AddSingleton<IStorageClientFactory<BlobConfig, BlobContainerClient>, BlobStorageFactory>();
        
        return services;
    }
}