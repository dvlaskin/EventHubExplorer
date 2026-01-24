using Azure.Core;
using Azure.Storage.Queues;
using Domain.Configs;
using Domain.Interfaces.Providers;
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

    public async Task SendMessageAsync(
        string message, Func<string, BinaryData>? messageModifier = null, CancellationToken cancellationToken = default
    )
    {
        var messageContent = messageModifier is null
            ? BinaryData.FromString(message)
            : messageModifier(message);

        await queueClient.Value.SendMessageAsync(messageContent, cancellationToken: cancellationToken);
        logger.LogInformation("Single message sent to queue {QueueName}", config.QueueName);
    }

    public async Task SendMessagesAsync(
        string message,
        Func<string, BinaryData>? messageModifier = null,
        uint numberOfMessages = 1,
        CancellationToken cancellationToken = default
    )
    {
        for (var i = 0; i < numberOfMessages; i++)
        {
            var messageContent = messageModifier is null
                ? BinaryData.FromString(message)
                : messageModifier(message);

            await queueClient.Value.SendMessageAsync(messageContent, cancellationToken: cancellationToken);
        }

        logger.LogInformation("Sent {MsgCount} messages to queue {QueueName}", numberOfMessages, config.QueueName);
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
            var messageContent = messageModifier is null
                ? BinaryData.FromString(message)
                : messageModifier(message);

            await queueClient.Value.SendMessageAsync(messageContent, cancellationToken: cancellationToken);
            logger.LogInformation("Message number {MessageNumber} from {TotalMessages} sent", i + 1, numberOfMessages);

            if (sendDelay != TimeSpan.Zero)
                await Task.Delay(sendDelay, cancellationToken);
        }

        logger.LogInformation("Sent all {MsgCount} messages to queue {QueueName}", numberOfMessages, config.QueueName);
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
                MaxDelay =  TimeSpan.FromSeconds(1),
                Mode = RetryMode.Fixed,
            }
        };
        var queueServiceClient = new QueueServiceClient(config.ConnectionString, opt);
        var client = queueServiceClient.GetQueueClient(config.QueueName);
        
        return client.Exists() ? client : throw new InvalidOperationException($"Queue {config.QueueName} does not exist");
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
