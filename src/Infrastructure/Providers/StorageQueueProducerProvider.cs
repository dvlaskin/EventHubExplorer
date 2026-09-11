using Azure.Core;
using Azure.Storage.Queues;
using Domain.Configs;
using Domain.Interfaces.Providers;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

public sealed class StorageQueueProducerProvider : IMessageProducerProvider
{
    private readonly ILogger<StorageQueueProducerProvider> logger;
    private readonly StorageQueueConfig config;
    private readonly Lazy<QueueClient> queueClient;


    public StorageQueueProducerProvider(ILogger<StorageQueueProducerProvider> logger, StorageQueueConfig config)
    {
        this.logger = logger;
        this.config = config;
        queueClient = new Lazy<QueueClient>(CreateQueueClient);
    }


    public async Task SendMessageAsync(OutgoingMessage message, CancellationToken ct = default)
    {
        LogPropertiesIgnored(message);
        var messageContent = CreateMessageContent(message);

        await queueClient.Value.SendMessageAsync(messageContent, cancellationToken: ct);
        logger.LogInformation("Single message sent to queue {QueueName}", config.QueueName);
    }

    public async Task SendMessagesAsync(OutgoingMessage message, uint numberOfMessages = 1, CancellationToken ct = default)
    {
        LogPropertiesIgnored(message);

        for (var i = 0; i < numberOfMessages; i++)
        {
            var messageContent = CreateMessageContent(message);
            await queueClient.Value.SendMessageAsync(messageContent, cancellationToken: ct);
        }

        logger.LogInformation("Sent {MsgCount} messages to queue {QueueName}", numberOfMessages, config.QueueName);
    }

    public async Task SendMessagesWithDelayAsync(
        OutgoingMessage message, uint numberOfMessages = 1, TimeSpan sendDelay = default, CancellationToken ct = default
    )
    {
        LogPropertiesIgnored(message);

        for (var i = 0; i < numberOfMessages; i++)
        {
            var messageContent = CreateMessageContent(message);

            await queueClient.Value.SendMessageAsync(messageContent, cancellationToken: ct);
            logger.LogInformation("Message number {MessageNumber} from {TotalMessages} sent", i + 1, numberOfMessages);

            if (sendDelay != TimeSpan.Zero)
                await Task.Delay(sendDelay, ct);
        }

        logger.LogInformation("Sent all {MsgCount} messages to queue {QueueName}", numberOfMessages, config.QueueName);
    }


    private void LogPropertiesIgnored(OutgoingMessage message)
    {
        if (message.Properties is { Count: > 0 })
            logger.LogWarning(
                "Storage Queue {QueueName} does not support properties — ignoring {PropertyCount} properties.",
                config.QueueName, message.Properties.Count
            );
    }

    private static BinaryData CreateMessageContent(OutgoingMessage message)
    {
        return message.MessageModifier is null
            ? BinaryData.FromString(message.Message)
            : message.MessageModifier(message.Message);
    }

    private QueueClient CreateQueueClient()
    {
        logger.LogInformation("Creating QueueClient for queue: {QueueName}", config.QueueName);

        var opt = new QueueClientOptions()
        {
            Retry =
            {
                MaxRetries = 3,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(1),
                Mode = RetryMode.Fixed,
            }
        };
        var queueServiceClient = new QueueServiceClient(config.ConnectionString, opt);
        var client = queueServiceClient.GetQueueClient(config.QueueName);

        return client.Exists() is { HasValue: true, Value: true }
            ? client
            : throw new InvalidOperationException($"Queue {config.QueueName} does not exist");
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
