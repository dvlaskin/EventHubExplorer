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
    
    
    public async Task SendMessageAsync(
        string message, Func<string, BinaryData>? messageModifier = null, CancellationToken cancellationToken = default
    )
    {
        var eventData = messageModifier is null
            ? new EventData(message)
            : new EventData(messageModifier(message));

        await producerClient.Value.SendAsync([eventData], cancellationToken);
        logger.LogInformation("The single message sent");
    }

    public async Task SendMessagesAsync(
        string message, 
        Func<string, BinaryData>? messageModifier = null, 
        uint numberOfMessages = 1, 
        CancellationToken cancellationToken = default
    )
    {
        var eventBatch = await producerClient.Value.CreateBatchAsync(cancellationToken);
        try
        {
            for (var num = 0; num < numberOfMessages; num++)
            {
                var eventData = messageModifier is null 
                    ? new EventData(message)
                    : new EventData(messageModifier(message));

                if (eventBatch.TryAdd(eventData)) 
                    continue;
                
                await producerClient.Value.SendAsync(eventBatch, cancellationToken);
                eventBatch.Dispose();
                
                eventBatch = await producerClient.Value.CreateBatchAsync(cancellationToken);
                if (!eventBatch.TryAdd(eventData))
                {
                    throw new InvalidOperationException("The message is too large and cannot be sent");
                }
            }
            
            await producerClient.Value.SendAsync(eventBatch, cancellationToken);
            logger.LogInformation("Sent all {MsgCount} messages", numberOfMessages);
        }
        finally
        {
            eventBatch.Dispose();
        }
    }
    
    public async Task SendMessagesWithDelayAsync(
        string message, 
        Func<string, BinaryData>? messageModifier = null, 
        uint numberOfMessages = 1,
        TimeSpan sendDelay = default,
        CancellationToken cancellationToken = default
    )
    {
        for (var num = 0; num < numberOfMessages; num++)
        {
            var eventData = messageModifier is null 
                ? new EventData(message)
                : new EventData(messageModifier(message));
            
            await producerClient.Value.SendAsync([eventData], cancellationToken);
            
            logger.LogInformation("Message number {MessageNumber} from {TotalMessages} total is sent", num + 1, numberOfMessages);
            
            if (sendDelay != TimeSpan.Zero)
                await Task.Delay(sendDelay, cancellationToken);
        }
        
        logger.LogInformation("Sent all {MsgCount} messages", numberOfMessages);
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