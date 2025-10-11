using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Domain.Interfaces.Providers;
using Domain.Interfaces.Services;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class EventHubConsumerService : IMessageConsumerService
{
    private readonly ILogger<EventHubConsumerService> logger;
    private readonly IMessageConsumerProvider messageConsumerProvider;
    private readonly ITextProcessingPipeline textProcessingPipeline;
    
    private readonly Channel<EventHubMessage> channel = Channel.CreateUnbounded<EventHubMessage>();
    private readonly object startLock = new();
    private bool isProcessing;
    

    public EventHubConsumerService(
        ILogger<EventHubConsumerService> logger,
        IMessageConsumerProvider messageConsumerProvider,
        ITextProcessingPipeline textProcessingPipeline
    )
    {
        this.logger = logger;
        this.messageConsumerProvider = messageConsumerProvider;
        this.textProcessingPipeline = textProcessingPipeline;
    }
    
    
    public async IAsyncEnumerable<EventHubMessage> StartReceiveMessageAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await ReadMessageAsync(cancellationToken);

        await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken))
        {
            logger.LogInformation("Received message {MsgData}", message.Message);
            message.Message = textProcessingPipeline.Process(message.Message);
            
            yield return message;
        }
    }

    public async Task StopReceiveMessageAsync()
    {
        lock (startLock)
        {
            isProcessing = false;
        }
        await messageConsumerProvider.StopReceiveMessageAsync();
    }
        
    
    private async Task ReadMessageAsync(CancellationToken cancellationToken)
    {
        lock (startLock)
        {
            if (isProcessing)
                return;
            
            isProcessing = true;
        }

        var taskResult = messageConsumerProvider.StartReceiveMessageAsync(async message =>
        {
            await channel.Writer.WriteAsync(message, cancellationToken);
        }, cancellationToken);
        
        if (taskResult.Exception is not null)
            throw taskResult.Exception;

        await Task.CompletedTask;
    }
    
    
    public async ValueTask DisposeAsync()
    {
        channel.Writer.TryComplete();
        await StopReceiveMessageAsync();
        GC.SuppressFinalize(this);
        logger.LogInformation("EventHubConsumerService is Disposed");
    }
}