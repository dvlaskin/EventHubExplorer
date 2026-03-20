using Application.Utils;
using Azure.Core;
using Azure.Storage.Queues;
using Domain.Configs;
using Domain.Interfaces.Providers;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

public sealed class StorageQueueConsumerProvider : IMessageConsumerProvider
{
    private readonly ILogger<StorageQueueConsumerProvider> logger;
    private readonly StorageQueueConfig config;

    private QueueClient? queueClient;
    private Func<EventHubMessage, Task>? runMessageProcessing;
    private volatile bool isProcessing;
    private volatile bool disposed;

    public StorageQueueConsumerProvider(ILogger<StorageQueueConsumerProvider> logger, StorageQueueConfig config)
    {
        this.logger = logger;
        this.config = config;
    }

    public async Task StartReceiveMessageAsync(Func<EventHubMessage, Task> onMessageReceived, CancellationToken cancellationToken)
    {
        CreateQueueClientIfNotExist();

        if (queueClient is null)
            throw new ApplicationException("QueueClient is not initialized");

        runMessageProcessing = onMessageReceived;
        isProcessing = true;

        logger.LogInformation("Start receiving messages from queue {QueueName}", config.QueueName);

        await ReceiveMessagesAsync(cancellationToken);
    }

    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        while (isProcessing && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var messages = await queueClient!.ReceiveMessagesAsync(
                    maxMessages: 1,
                    visibilityTimeout: TimeSpan.FromSeconds(30),
                    cancellationToken: cancellationToken
                );

                if (messages.Value.Length == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                    continue;
                }

                foreach (var message in messages.Value)
                {
                    if (!isProcessing)
                        break;

                    var msgData = new EventHubMessage
                    {
                        Message = CompressingEncoding.DecodeMessage(message.Body, config),
                        EnqueuedTime = message.InsertedOn ?? DateTimeOffset.UtcNow
                    };

                    if (runMessageProcessing is not null)
                    {
                        await runMessageProcessing(msgData);
                    }

                    await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error while receiving messages from queue {QueueName}", config.QueueName);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }

    public Task StopReceiveMessageAsync()
    {
        isProcessing = false;
        logger.LogInformation("Stop receiving messages from queue {QueueName}", config.QueueName);
        return Task.CompletedTask;
    }

    private void CreateQueueClientIfNotExist()
    {
        if (queueClient is null)
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
            queueClient = queueServiceClient.GetQueueClient(config.QueueName);
            if (!queueClient.Exists())
                throw new InvalidOperationException($"Queue {config.QueueName} does not exist");
        }
    }
    
    
    public ValueTask DisposeAsync()
    {
        if (disposed)
            return ValueTask.CompletedTask;

        disposed = true;
        isProcessing = false;
        GC.SuppressFinalize(this);
        logger.LogInformation("StorageQueueConsumerProvider is Disposed");
        return ValueTask.CompletedTask;
    }
}
