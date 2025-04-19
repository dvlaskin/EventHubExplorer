using System.Text;
using Azure.Messaging.EventHubs.Consumer;
using Domain.Entities;
using Domain.Interfaces.Providers;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

public class EventHubConsumerProviderWithoutStorage : IMessageConsumerProvider
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
    
    public async Task StartReceiveMessageAsync(Func<string, Task> onMessageReceived, CancellationToken cancellationToken)
    {
        needToStop = false;
        await using var consumer = new EventHubConsumerClient(ConsumerGroup, config.ConnectionString, config.Name);
        await foreach (var partitionEvent in consumer.ReadEventsAsync(startReadingAtEarliestEvent: false, cancellationToken: cancellationToken))
        {
            if (needToStop)
                break;
            
            logger.LogInformation(
                "Received message from Partition {Partition}, SequenceNumber {SequenceNumber}, EnqueuedTime {EnqueuedTime}\r\n{Message}", 
                partitionEvent.Partition.PartitionId, 
                partitionEvent.Data.SequenceNumber,
                partitionEvent.Data.EnqueuedTime,
                Encoding.UTF8.GetString(partitionEvent.Data.Body.ToArray())
            );
            
            await onMessageReceived(Encoding.UTF8.GetString(partitionEvent.Data.Body.ToArray()));
        }
    }

    public Task StopReceiveMessageAsync()
    {
        needToStop = true;
        return Task.CompletedTask;
    }
}