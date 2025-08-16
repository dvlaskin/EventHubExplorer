using Domain.Interfaces.Providers;
using Domain.Interfaces.Services;

namespace Application.Services.MessageProducers;

public abstract class MessageProducerBase<T> : IMessageProducerService
{
    protected readonly IMessageProducerProvider MessageProducerProvider;

    protected MessageProducerBase(IMessageProducerProvider messageProducerProvider)
    {
        this.MessageProducerProvider = messageProducerProvider;
    }
    

    public async Task SendMessagesAsync(
        string? messageText, int numberOfMessages = 1, TimeSpan? delayToSend = null, CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(messageText))
            return;
        
        var messageToSend = ApplyOptions(messageText);
        
        if (numberOfMessages <= 1)
            await SendSingleMessageAsync(messageToSend, cancellationToken);
        else if (delayToSend is null || delayToSend.Value <= TimeSpan.Zero)
            await SendBatchMessagesAsync(messageToSend, numberOfMessages, cancellationToken);
        else
            await SendMessagesWithDelayAsync(
                messageToSend, numberOfMessages, delayToSend.Value, cancellationToken
            );
    }
    
    
    protected abstract T ApplyOptions(string message);
    protected abstract Task SendSingleMessageAsync(T message, CancellationToken cancellationToken);
    protected abstract Task SendBatchMessagesAsync(T message, int numberOfMessages, CancellationToken cancellationToken);
    protected abstract Task SendMessagesWithDelayAsync(T message, int numberOfMessages, TimeSpan timeDelay, CancellationToken cancellationToken);
    
    
    protected static IReadOnlyList<T> MultipleMessages(T message, int numberOfMessages)
    {
        return Enumerable.Repeat(message, numberOfMessages).ToArray();
    }
    
        
    public async ValueTask DisposeAsync()
    {
        await MessageProducerProvider.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}