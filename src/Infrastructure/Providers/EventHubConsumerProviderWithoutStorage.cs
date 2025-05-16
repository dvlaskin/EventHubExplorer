using System.Text;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Domain.Configs;
using Domain.Interfaces.Providers;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

public sealed class EventHubConsumerProviderWithoutStorage : IMessageConsumerProvider
{
    private readonly ILogger<EventHubConsumerProviderWithoutStorage> logger;
    private readonly EventHubConfig config;
    
    private const string ConsumerGroup = EventHubConsumerClient.DefaultConsumerGroupName;
    private bool needToStop;

    public EventHubConsumerProviderWithoutStorage(ILogger<EventHubConsumerProviderWithoutStorage> logger, EventHubConfig config)
    {
        this.logger = logger;
        this.config = config;
    }
    
    public async Task StartReceiveMessageAsync(Func<EventHubMessage, Task> onMessageReceived, CancellationToken cancellationToken)
    {
        needToStop = false;
        var options = new EventHubConsumerClientOptions
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
        };
        
        await using var consumer = new EventHubConsumerClient(ConsumerGroup, config.ConnectionString, config.Name, options);
        var partitions = await consumer.GetPartitionIdsAsync(cancellationToken);
        logger.LogInformation("Start reading from partitions {Partitions}", string.Join(", ", partitions));
        await foreach (var partitionEvent in consumer.ReadEventsAsync(startReadingAtEarliestEvent: false, cancellationToken: cancellationToken))
        {
            if (needToStop)
                break;
            
            var msgData = new EventHubMessage
            {
                Message = Encoding.UTF8.GetString(partitionEvent.Data.Body.ToArray()),
                PartitionId = partitionEvent.Partition.PartitionId,
                SequenceNumber = partitionEvent.Data.SequenceNumber,
                EnqueuedTime = partitionEvent.Data.EnqueuedTime
            };
            
            await onMessageReceived(msgData);
        }
    }

    public Task StopReceiveMessageAsync()
    {
        needToStop = true;
        return Task.CompletedTask;
    }
}