using Azure.Messaging.ServiceBus;
using Domain.Configs;
using Domain.Interfaces.Providers;
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
        string message, Func<string, BinaryData>? messageModifier = null, CancellationToken cancellationToken = default
    )
    {
        var binaryData = messageModifier is null
            ? BinaryData.FromString(message)
            : messageModifier(message);

        var sbMessage = new ServiceBusMessage(binaryData);
        await sender.Value.SendMessageAsync(sbMessage, cancellationToken);
        
        logger.LogInformation("Single message sent to Service Bus {EntityType} {EntityName}", config.EntityType, config.EntityName);
    }

    public async Task SendMessagesAsync(
        string message,
        Func<string, BinaryData>? messageModifier = null,
        uint numberOfMessages = 1,
        CancellationToken cancellationToken = default
    )
    {
        var messageBatch = await sender.Value.CreateMessageBatchAsync(cancellationToken);
        try
        {
            for (var i = 0; i < numberOfMessages; i++)
            {
                var binaryData = messageModifier is null
                    ? BinaryData.FromString(message)
                    : messageModifier(message);

                var sbMessage = new ServiceBusMessage(binaryData);

                if (messageBatch.TryAddMessage(sbMessage))
                    continue;

                if (messageBatch.Count == 0)
                {
                    throw new InvalidOperationException("The message is too large and cannot be sent");
                }

                await sender.Value.SendMessagesAsync(messageBatch, cancellationToken);
                messageBatch.Dispose();

                messageBatch = await sender.Value.CreateMessageBatchAsync(cancellationToken);
                if (!messageBatch.TryAddMessage(sbMessage))
                {
                    throw new InvalidOperationException("The message is too large and cannot be sent");
                }
            }

            if (messageBatch.Count > 0)
            {
                await sender.Value.SendMessagesAsync(messageBatch, cancellationToken);
            }

            logger.LogInformation("Sent {MsgCount} messages to Service Bus {EntityType} {EntityName}", numberOfMessages, config.EntityType, config.EntityName);
        }
        finally
        {
            messageBatch.Dispose();
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
        for (var i = 0; i < numberOfMessages; i++)
        {
            var binaryData = messageModifier is null
                ? BinaryData.FromString(message)
                : messageModifier(message);

            var sbMessage = new ServiceBusMessage(binaryData);
            await sender.Value.SendMessageAsync(sbMessage, cancellationToken);
            
            logger.LogInformation("Message number {MessageNumber} from {TotalMessages} sent", i + 1, numberOfMessages);

            if (sendDelay != TimeSpan.Zero)
                await Task.Delay(sendDelay, cancellationToken);
        }

        logger.LogInformation("Sent all {MsgCount} messages to Service Bus {EntityType} {EntityName}", numberOfMessages, config.EntityType, config.EntityName);
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
