using Azure.Messaging.ServiceBus;
using Domain.Configs;
using Domain.Interfaces.Providers;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

public sealed class ServiceBusProducerProvider : IMessageProducerProvider
{
    private readonly ILogger<ServiceBusProducerProvider> logger;
    private readonly ServiceBusConfig config;
    private readonly Lazy<ServiceBusClient> client;
    private readonly Lazy<ServiceBusSender> sender;
    private bool disposed;


    public ServiceBusProducerProvider(ILogger<ServiceBusProducerProvider> logger, ServiceBusConfig config)
    {
        this.logger = logger;
        this.config = config;
        client = new Lazy<ServiceBusClient>(CreateClient);
        sender = new Lazy<ServiceBusSender>(() => client.Value.CreateSender(config.EntityName));
    }


    public async Task SendMessageAsync(
        OutgoingMessage message, CancellationToken ct = default
    )
    {
        var sbMessage = CreateServiceBusMessage(message);
        await sender.Value.SendMessageAsync(sbMessage, ct);

        logger.LogInformation("Single message sent to Service Bus {EntityType} {EntityName} with {PropertyCount} properties", config.EntityType, config.EntityName, message.Properties?.Count ?? 0);
    }

    public async Task SendMessagesAsync(
        OutgoingMessage message,
        uint numberOfMessages = 1,
        CancellationToken ct = default
    )
    {
        var messageBatch = await sender.Value.CreateMessageBatchAsync(ct);
        try
        {
            for (var i = 0; i < numberOfMessages; i++)
            {
                var sbMessage = CreateServiceBusMessage(message);

                if (messageBatch.TryAddMessage(sbMessage))
                    continue;

                if (messageBatch.Count == 0)
                {
                    throw new InvalidOperationException("The message is too large and cannot be sent");
                }

                await sender.Value.SendMessagesAsync(messageBatch, ct);
                messageBatch.Dispose();

                messageBatch = await sender.Value.CreateMessageBatchAsync(ct);
                if (!messageBatch.TryAddMessage(sbMessage))
                {
                    throw new InvalidOperationException("The message is too large and cannot be sent");
                }
            }

            if (messageBatch.Count > 0)
            {
                await sender.Value.SendMessagesAsync(messageBatch, ct);
            }

            logger.LogInformation("Sent {MsgCount} messages to Service Bus {EntityType} {EntityName} with {PropertyCount} properties", numberOfMessages, config.EntityType, config.EntityName, message.Properties?.Count ?? 0);
        }
        finally
        {
            messageBatch.Dispose();
        }
    }

    public async Task SendMessagesWithDelayAsync(
        OutgoingMessage message,
        uint numberOfMessages = 1,
        TimeSpan sendDelay = default,
        CancellationToken ct = default
    )
    {
        for (var i = 0; i < numberOfMessages; i++)
        {
            var sbMessage = CreateServiceBusMessage(message);
            await sender.Value.SendMessageAsync(sbMessage, ct);

            logger.LogInformation("Message number {MessageNumber} from {TotalMessages} sent", i + 1, numberOfMessages);

            if (sendDelay != TimeSpan.Zero)
                await Task.Delay(sendDelay, ct);
        }

        logger.LogInformation("Sent all {MsgCount} messages to Service Bus {EntityType} {EntityName} with {PropertyCount} properties", numberOfMessages, config.EntityType, config.EntityName, message.Properties?.Count ?? 0);
    }


    private static ServiceBusMessage CreateServiceBusMessage(OutgoingMessage message)
    {
        var binaryData = message.MessageModifier is null
            ? BinaryData.FromString(message.Message)
            : message.MessageModifier(message.Message);

        var sbMessage = new ServiceBusMessage(binaryData);

        if (message.Properties is null || message.Properties.Count == 0)
            return sbMessage;

        foreach (var (key, value) in message.Properties)
        {
            try
            {
                sbMessage.ApplicationProperties.TryAdd(key, value);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to map property '{key}'.", ex);
            }
        }

        return sbMessage;
    }

    private ServiceBusClient CreateClient()
    {
        logger.LogInformation("Creating ServiceBusClient for {EntityName}", config.EntityName);

        var options = new ServiceBusClientOptions
        {
            TransportType = config.ConnectionString.Contains("UseDevelopmentEmulator=true")
                ? ServiceBusTransportType.AmqpTcp
                : ServiceBusTransportType.AmqpWebSockets,
            RetryOptions = new ServiceBusRetryOptions
            {
                MaxRetries = 3,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(1),
                Mode = ServiceBusRetryMode.Fixed
            }
        };

        return new ServiceBusClient(config.ConnectionString, options);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        if (sender.IsValueCreated)
        {
            await sender.Value.DisposeAsync();
        }

        if (client.IsValueCreated)
        {
            await client.Value.DisposeAsync();
        }

        logger.LogInformation("ServiceBusProducerProvider is Disposed");

        GC.SuppressFinalize(this);
        disposed = true;
    }

    ~ServiceBusProducerProvider() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
