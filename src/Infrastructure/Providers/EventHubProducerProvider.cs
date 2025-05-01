using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Domain.Configs;
using Domain.Interfaces.Providers;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

public sealed class EventHubProducerProvider : IMessageProducerProvider
{
    private readonly ILogger<EventHubProducerProvider> logger;
    private readonly EventHubConfig config;
    private EventHubProducerClient? producerClient;

    public EventHubProducerProvider(ILogger<EventHubProducerProvider> logger, EventHubConfig config)
    {
        this.logger = logger;
        this.config = config;
    }
    
    public async Task SendMessagesAsync(string messageText, CancellationToken cancellationToken)
    {
        CreateProducerIfNotExist();
        using var eventBatch = await producerClient!.CreateBatchAsync(cancellationToken);

        var message = new EventData(messageText);
        if (!eventBatch.TryAdd(message))
        {
            throw new Exception("The message is too large to fit in the batch.");
        }
        
        await producerClient.SendAsync(eventBatch, cancellationToken);
        logger.LogInformation("Sent message: {Message}", messageText);
    }
    
    public async Task SendMessagesAsync(ICollection<string> messagesText, CancellationToken cancellationToken)
    {
        CreateProducerIfNotExist();
        using var eventBatch = await producerClient!.CreateBatchAsync(cancellationToken);

        foreach (var msg in messagesText)
        {
            if (!eventBatch.TryAdd(new EventData(msg)))
            {
                throw new Exception("The message is too large to fit in the batch.");
            }
        }
        
        await producerClient.SendAsync(eventBatch, cancellationToken);
        logger.LogInformation("Sent {MsgCount} messages", messagesText.Count);
    }
    
    private void CreateProducerIfNotExist()
    {
        logger.LogInformation("Creating producer, EventHubName: {EventHubName}, ConnectionString: {ConnectionString}", 
            config.Name,
            config.ConnectionString
        );
        producerClient ??= new EventHubProducerClient(
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
        if (producerClient is not null)
        {
            await producerClient.DisposeAsync();
            logger.LogInformation("EventHubProducer is Disposed");
        }
    }
}