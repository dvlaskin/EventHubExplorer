using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Domain.Configs;
using Domain.Interfaces.Providers;
using Domain.Models;
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
        producerClient = new Lazy<EventHubProducerClient>(CreateProducer(config));
    }


    public async Task SendMessageAsync(
        OutgoingMessage message, CancellationToken ct = default
    )
    {
        var eventData = CreateEventData(message);

        await producerClient.Value.SendAsync([eventData], ct);
        logger.LogInformation("The single message sent with {PropertyCount} properties", message.Properties?.Count ?? 0);
    }

    public async Task SendMessagesAsync(
        OutgoingMessage message,
        uint numberOfMessages = 1,
        CancellationToken ct = default
    )
    {
        var eventBatch = await producerClient.Value.CreateBatchAsync(ct);
        try
        {
            for (var num = 0; num < numberOfMessages; num++)
            {
                var eventData = CreateEventData(message);

                if (eventBatch.TryAdd(eventData))
                    continue;

                await producerClient.Value.SendAsync(eventBatch, ct);
                eventBatch.Dispose();

                eventBatch = await producerClient.Value.CreateBatchAsync(ct);
                if (!eventBatch.TryAdd(eventData))
                {
                    throw new InvalidOperationException("The message is too large and cannot be sent");
                }
            }

            await producerClient.Value.SendAsync(eventBatch, ct);
            logger.LogInformation("Sent all {MsgCount} messages with {PropertyCount} properties", numberOfMessages, message.Properties?.Count ?? 0);
        }
        finally
        {
            eventBatch.Dispose();
        }
    }

    public async Task SendMessagesWithDelayAsync(
        OutgoingMessage message,
        uint numberOfMessages = 1,
        TimeSpan sendDelay = default,
        CancellationToken ct = default
    )
    {
        for (var num = 0; num < numberOfMessages; num++)
        {
            var eventData = CreateEventData(message);

            await producerClient.Value.SendAsync([eventData], ct);

            logger.LogInformation("Message number {MessageNumber} from {TotalMessages} total is sent", num + 1, numberOfMessages);

            if (sendDelay != TimeSpan.Zero)
                await Task.Delay(sendDelay, ct);
        }

        logger.LogInformation("Sent all {MsgCount} messages with {PropertyCount} properties", numberOfMessages, message.Properties?.Count ?? 0);
    }


    private static EventData CreateEventData(OutgoingMessage message)
    {
        var eventData = message.MessageModifier is null
            ? new EventData(message.Message)
            : new EventData(message.MessageModifier(message.Message));

        if (message.Properties is null || message.Properties.Count == 0)
            return eventData;

        foreach (var (key, value) in message.Properties)
        {
            try
            {
                eventData.Properties.TryAdd(key, value);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to map property '{key}'.", ex);
            }
        }

        return eventData;
    }


    private EventHubProducerClient CreateProducer(EventHubConfig config)
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
                    MaximumDelay = TimeSpan.FromSeconds(1),
                    Delay = TimeSpan.FromSeconds(1),
                    TryTimeout = TimeSpan.FromSeconds(1),
                    Mode = EventHubsRetryMode.Fixed,
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