using System.Text;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using Domain.Entities;
using Domain.Interfaces.Factories;
using Domain.Interfaces.Providers;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

public class EventHubConsumerProviderWithStorage : IMessageConsumerProvider
{
    private readonly ILogger<EventHubConsumerProviderWithStorage> logger;
    private readonly EventHubConfig config;
    private readonly IStorageClientFactory<BlobConfig, BlobContainerClient> storageClientFactory;
    
    private BlobContainerClient? storageClient;
    private EventProcessorClient? eventProcessorClient;
    private Func<string, Task>? messageProcessor;

    
    public EventHubConsumerProviderWithStorage(        
        ILogger<EventHubConsumerProviderWithStorage> logger, 
        EventHubConfig config, 
        IStorageClientFactory<BlobConfig, BlobContainerClient> storageClientFactory
    )
    {
        this.logger = logger;
        this.config = config;
        this.storageClientFactory = storageClientFactory;
    }
    
    
    public async Task StartReceiveMessageAsync(Func<string, Task> onMessageReceived, CancellationToken cancellationToken)
    {
        CreateBlobStorageClientIfNotExist();
        CreateEventProcessorClientIfNotExist();
        
        if (eventProcessorClient is null)
            throw new ApplicationException("EventProcessorClient is not initialized");
        
        messageProcessor = onMessageReceived;
        eventProcessorClient.ProcessEventAsync += OnProcessEventAsync;
        eventProcessorClient.ProcessErrorAsync += OnProcessErrorAsync;
        
        await eventProcessorClient.StartProcessingAsync(cancellationToken);
    }
    

    public async Task StopReceiveMessageAsync()
    {
        if (eventProcessorClient is null)
            return;
        
        await eventProcessorClient.StopProcessingAsync();
        try
        {
            eventProcessorClient.ProcessEventAsync -= OnProcessEventAsync;
            eventProcessorClient.ProcessErrorAsync -= OnProcessErrorAsync;
        }
        catch (ArgumentException)
        {
            logger.LogInformation("EventProcessorClient handlers are already removed");
        }
    }
    
    
    private void CreateBlobStorageClientIfNotExist()
    {
        if (config.StorageConfig is not null)
            storageClient ??= storageClientFactory.CreateStorageClient(config.StorageConfig);
    }
    
    private void CreateEventProcessorClientIfNotExist()
    {
        if (storageClient is null)
            throw new ApplicationException("StorageClient for EventProcessorClient is not initialized");
        
        eventProcessorClient ??= new EventProcessorClient(
            storageClient,
            "$Default",
            config.ConnectionString,
            config.Name,
            new ()
            {
                ConnectionOptions = new()
                {
                    TransportType = config.ConnectionString.Contains("UseDevelopmentEmulator=true") 
                        ? EventHubsTransportType.AmqpTcp 
                        : EventHubsTransportType.AmqpWebSockets,
                }
            }
        );
    }
    
    protected virtual async Task OnProcessEventAsync(ProcessEventArgs eventArgs)
    {
        logger.LogInformation(
            "Received message from Partition {Partition}, SequenceNumber {SequenceNumber}, EnqueuedTime {EnqueuedTime}\r\n{Message}", 
            eventArgs.Partition.PartitionId, 
            eventArgs.Data.SequenceNumber,
            eventArgs.Data.EnqueuedTime,
            Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray())
        );
        
        if (messageProcessor is not null)
        {
            await messageProcessor(Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray()));
        }

        await eventArgs.UpdateCheckpointAsync(eventArgs.CancellationToken);
    }
    
    private Task OnProcessErrorAsync(ProcessErrorEventArgs arg)
    {
        logger.LogError(arg.Exception, "Error while processing event: {Message}", arg.Exception.Message);
        return Task.CompletedTask;
    }
    
}