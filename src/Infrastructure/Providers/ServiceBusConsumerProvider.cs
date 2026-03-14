using Application.Utils;
using Azure.Messaging.ServiceBus;
using Domain.Configs;
using Domain.Enums;
using Domain.Interfaces.Providers;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

public sealed class ServiceBusConsumerProvider : IMessageConsumerProvider
{
    private readonly ILogger<ServiceBusConsumerProvider> logger;
    private readonly ServiceBusConfig config;
    private readonly Lazy<ServiceBusClient> client;
    private readonly Lazy<ServiceBusReceiver> receiver;
    
    private Func<EventHubMessage, Task>? runMessageProcessing;
    private bool isProcessing;
    private bool disposed;

    
    public ServiceBusConsumerProvider(ILogger<ServiceBusConsumerProvider> logger, ServiceBusConfig config)
    {
        this.logger = logger;
        this.config = config;
        client = new Lazy<ServiceBusClient>(CreateClient);
        receiver = new Lazy<ServiceBusReceiver>(CreateReceiver);
    }

    
    public async Task StartReceiveMessageAsync(Func<EventHubMessage, Task> onMessageReceived, CancellationToken cancellationToken)
    {
        runMessageProcessing = onMessageReceived;
        isProcessing = true;
        
        logger.LogInformation("Start receiving messages from Service Bus {EntityType} {EntityName}", config.EntityType, config.EntityName);
        
        await ReceiveMessagesAsync(cancellationToken);
    }

    public Task StopReceiveMessageAsync()
    {
        isProcessing = false;
        logger.LogInformation("Stop receiving messages from Service Bus {EntityName}", config.EntityName);
        return Task.CompletedTask;
    }

    
    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        while (isProcessing && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var messages = await receiver.Value.ReceiveMessagesAsync(
                    maxMessages: 10,
                    maxWaitTime: TimeSpan.FromSeconds(5),
                    cancellationToken: cancellationToken
                );

                if (messages.Count == 0)
                {
                    continue;
                }

                foreach (var message in messages)
                {
                    if (!isProcessing)
                        break;

                    var msgData = new EventHubMessage
                    {
                        Message = CompressingEncoding.DecodeMessage(message.Body, config),
                        EnqueuedTime = message.EnqueuedTime,
                        SequenceNumber = message.SequenceNumber
                    };

                    if (runMessageProcessing is not null)
                    {
                        await runMessageProcessing(msgData);
                    }

                    await receiver.Value.CompleteMessageAsync(message, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error while receiving messages from Service Bus {EntityName}", config.EntityName);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
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

    private ServiceBusReceiver CreateReceiver()
    {
        if (config.EntityType == ServiceBusEntityType.Queue)
        {
            return client.Value.CreateReceiver(config.EntityName);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(config.SubscriptionName))
            {
                throw new InvalidOperationException("Subscription name is required for Topic entity type");
            }
            return client.Value.CreateReceiver(config.EntityName, config.SubscriptionName);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        isProcessing = false;

        if (receiver.IsValueCreated)
        {
            await receiver.Value.DisposeAsync();
        }

        if (client.IsValueCreated)
        {
            await client.Value.DisposeAsync();
        }

        logger.LogInformation("ServiceBusConsumerProvider is Disposed");
        
        GC.SuppressFinalize(this);
        disposed = true;
    }
    
    ~ServiceBusConsumerProvider() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
