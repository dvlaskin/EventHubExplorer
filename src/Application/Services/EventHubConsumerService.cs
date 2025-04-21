using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Domain.Interfaces.Providers;
using Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class EventHubConsumerService : IMessageConsumerService
{
    private readonly ILogger<EventHubConsumerService> logger;
    private readonly IMessageConsumerProvider messageConsumerProvider;
    private readonly Channel<string> channel = Channel.CreateUnbounded<string>();
    private readonly object startLock = new();
    private bool isProcessing;
    

    public EventHubConsumerService(
        ILogger<EventHubConsumerService> logger,
        IMessageConsumerProvider messageConsumerProvider
    )
    {
        this.logger = logger;
        this.messageConsumerProvider = messageConsumerProvider;
    }
    
    
    public async IAsyncEnumerable<string> StartReceiveMessageAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await ReadMessageAsync(cancellationToken);

        await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken))
        {
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

        _ = messageConsumerProvider.StartReceiveMessageAsync(async message =>
        {
            await channel.Writer.WriteAsync(message, cancellationToken);
        }, cancellationToken);

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