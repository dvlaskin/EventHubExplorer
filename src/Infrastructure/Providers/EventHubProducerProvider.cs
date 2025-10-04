using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Domain.Configs;
using Domain.Interfaces.Providers;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

public sealed class EventHubProducerProvider : IMessageProducerProvider
{
    private readonly ILogger<EventHubProducerProvider> logger;
    private readonly Lazy<EventHubProducerClient> producerClient;
    private bool disposed;

    
    public EventHubProducerProvider(ILogger<EventHubProducerProvider> logger, EventHubConfig config)
    {
        this.logger = logger;
        producerClient = new Lazy<EventHubProducerClient>(CreateProducerExist(config));
    }
    
    
    public async Task SendMessageAsync(string message, CancellationToken cancellationToken)
    {
        await producerClient.Value.SendAsync([new EventData(message)], cancellationToken);
        logger.LogInformation("Sent string message: {Message}", message);
    }
    
    public async Task SendMessageAsync(byte[] message, CancellationToken cancellationToken)
    {
        await producerClient.Value.SendAsync([new EventData(message)], cancellationToken);
        logger.LogInformation("Sent binary message: {MessageLength}", message.Length);
    }
    
    
    public async Task SendMessagesAsync(IReadOnlyList<string> messages, CancellationToken cancellationToken)
    {
        using var eventBatch = await producerClient.Value.CreateBatchAsync(cancellationToken);

        foreach (var msg in messages)
        {
            if (eventBatch.TryAdd(new EventData(msg))) 
                continue;
            
            logger.LogWarning("The message is too large or too many messages to fit in the batch.");
            break;
        }
        
        await producerClient.Value.SendAsync(eventBatch, cancellationToken);
        logger.LogInformation("Sent {MsgCount} string messages", eventBatch.Count);
    }
    
    public async Task SendMessagesAsync(IReadOnlyList<byte[]> messages, CancellationToken cancellationToken)
    {
        using var eventBatch = await producerClient.Value.CreateBatchAsync(cancellationToken);

        foreach (var msg in messages)
        {
            if (eventBatch.TryAdd(new EventData(msg))) 
                continue;
            
            logger.LogWarning("The message is too large or too many messages to fit in the batch.");
            break;
        }
        
        await producerClient.Value.SendAsync(eventBatch, cancellationToken);
        logger.LogInformation("Sent {MsgCount} binary messages", messages.Count);
    }
    
    
    public async Task SendMessagesAsync(IReadOnlyList<string> messages, TimeSpan timeDelay, CancellationToken cancellationToken)
    {
        foreach (var item in messages.Select((msg, indx) => (msg, indx)))
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            await producerClient.Value.SendAsync([new EventData(item.msg)], cancellationToken);
            
            logger.LogInformation("Message number {MessageNumber} from {TotalMessages} total is sent", item.indx + 1, messages.Count);
            await Task.Delay(timeDelay, cancellationToken);
        }
        
        logger.LogInformation("Sent all {MsgCount} string messages", messages.Count);
    }
    
    public async Task SendMessagesAsync(IReadOnlyList<byte[]> messages, TimeSpan timeDelay, CancellationToken cancellationToken)
    {
        foreach (var item in messages.Select((msg, indx) => (msg, indx)))
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            await producerClient.Value.SendAsync([new EventData(item.msg)], cancellationToken);
            
            logger.LogInformation("Message number {MessageNumber} from {TotalMessages} total is sent", item.indx + 1, messages.Count);
            await Task.Delay(timeDelay, cancellationToken);
        }
        
        logger.LogInformation("Sent all {MsgCount} messages", messages.Count);
    }
    
    
    private EventHubProducerClient CreateProducerExist(EventHubConfig config)
    {
        logger.LogInformation("Creating producer, EventHubName: {EventHubName}", config.Name);
        
        return new EventHubProducerClient(
            config.ConnectionString, 
            config.Name,
            new EventHubProducerClientOptions
            {
                ConnectionOptions = new EventHubConnectionOptions
                {
                    TransportType = config.ConnectionString.Contains("UseDevelopmentEmulator=true")
                        ? EventHubsTransportType.AmqpTcp
                        : EventHubsTransportType.AmqpWebSockets,
                },
                RetryOptions = new EventHubsRetryOptions
                {
                    MaximumRetries = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    TryTimeout = TimeSpan.FromSeconds(3)
                },
            });
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;
        
        if (producerClient.IsValueCreated)
        {
            await producerClient.Value.DisposeAsync();
            logger.LogInformation("EventHubProducer is Disposed");
        }
        
        GC.SuppressFinalize(this);
        disposed = true;
    }
    
    ~EventHubProducerProvider() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}