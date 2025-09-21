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
    
    
    public async Task SendMessageAsync(string message, CancellationToken cancellationToken)
    {
        CreateProducerIfNotExist();
        using var eventBatch = await producerClient!.CreateBatchAsync(cancellationToken);

        var msg = new EventData(message);
        if (!eventBatch.TryAdd(msg))
        {
            throw new Exception("The message is too large to fit in the batch.");
        }
        
        await producerClient.SendAsync(eventBatch, cancellationToken);
        logger.LogInformation("Sent message: {Message}", message);
    }
    
    public async Task SendMessageAsync(byte[] message, CancellationToken cancellationToken)
    {
        CreateProducerIfNotExist();
        using var eventBatch = await producerClient!.CreateBatchAsync(cancellationToken);

        var msg = new EventData(message);
        if (!eventBatch.TryAdd(msg))
        {
            throw new Exception("The message is too large to fit in the batch.");
        }
        
        await producerClient.SendAsync(eventBatch, cancellationToken);
        logger.LogInformation("Sent binary message of length: {MessageLength}", message.Length);
    }
    
    
    public async Task SendMessagesAsync(IReadOnlyList<string> messages, CancellationToken cancellationToken)
    {
        CreateProducerIfNotExist();
        using var eventBatch = await producerClient!.CreateBatchAsync(cancellationToken);

        foreach (var msg in messages)
        {
            if (!eventBatch.TryAdd(new EventData(msg)))
            {
                throw new Exception("The message is too large to fit in the batch.");
            }
        }
        
        await producerClient.SendAsync(eventBatch, cancellationToken);
        logger.LogInformation("Sent {MsgCount} messages", messages.Count);
    }
    
    public async Task SendMessagesAsync(IReadOnlyList<byte[]> messages, CancellationToken cancellationToken)
    {
        CreateProducerIfNotExist();
        using var eventBatch = await producerClient!.CreateBatchAsync(cancellationToken);

        foreach (var msg in messages)
        {
            if (!eventBatch.TryAdd(new EventData(msg)))
            {
                throw new Exception("The message is too large to fit in the batch.");
            }
        }
        
        await producerClient.SendAsync(eventBatch, cancellationToken);
        logger.LogInformation("Sent {MsgCount} messages", messages.Count);
    }
    
    
    public async Task SendMessagesAsync(IReadOnlyList<string> messages, TimeSpan timeDelay, CancellationToken cancellationToken)
    {
        CreateProducerIfNotExist();

        foreach (var item in messages.Select((msg, indx) => (msg, indx)))
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            await producerClient!.SendAsync([new EventData(item.msg)], cancellationToken);
            logger.LogInformation("Message number {MessageNumber} for {TotalMessages} is sent", item.indx + 1, messages.Count);
            await Task.Delay(timeDelay, cancellationToken);
        }
        
        logger.LogInformation("Sent {MsgCount} messages", messages.Count);
    }
    
    public async Task SendMessagesAsync(IReadOnlyList<byte[]> messages, TimeSpan timeDelay, CancellationToken cancellationToken)
    {
        CreateProducerIfNotExist();

        foreach (var item in messages.Select((msg, indx) => (msg, indx)))
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            await producerClient!.SendAsync([new EventData(item.msg)], cancellationToken);
            logger.LogInformation("Message number {MessageNumber} for {TotalMessages} is sent", item.indx + 1, messages.Count);
            await Task.Delay(timeDelay, cancellationToken);
        }
        
        logger.LogInformation("Sent {MsgCount} messages", messages.Count);
    }
    
    
    private void CreateProducerIfNotExist()
    {
        if (producerClient is null)
            logger.LogInformation("Creating producer, EventHubName: {EventHubName}", 
                config.Name
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