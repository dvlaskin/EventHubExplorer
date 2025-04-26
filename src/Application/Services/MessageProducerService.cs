using Domain.Interfaces.Providers;
using Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class MessageProducerService : IMessageProducerService
{
    private readonly ILogger<MessageProducerService> logger;
    private readonly IMessageProducerProvider messageProducerProvider;


    public MessageProducerService(
        ILogger<MessageProducerService> logger,
        IMessageProducerProvider messageProducerProvider
    )
    {
        this.logger = logger;
        this.messageProducerProvider = messageProducerProvider;
    }
    
    
    public async Task SendMessagesAsync(
        string? messageText, 
        int numberOfMessages = 1, 
        TimeSpan? delayToSend = null,
        CancellationToken cancellationToken = default
    )
    {
        if (numberOfMessages <= 1)
            await SendSingleMessageAsync(messageText, cancellationToken);
        else if (delayToSend is null || delayToSend.Value == TimeSpan.Zero)
            await SendBatchMessagesAsync(messageText, numberOfMessages, cancellationToken);
        else
            await SendMessagesWithDelayAsync(
                messageText, numberOfMessages, delayToSend.Value, cancellationToken
            );
    }
    

    private async Task SendSingleMessageAsync(string? messageText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageText))
            return;
        
        await messageProducerProvider.SendMessagesAsync(messageText, cancellationToken);
    }

    private async Task SendBatchMessagesAsync(string? messageText, int numberOfMessages, CancellationToken cancellationToken = default)
    {
        if (numberOfMessages <= 0 || string.IsNullOrWhiteSpace(messageText))
            return;

        var messages = Enumerable.Repeat(messageText, numberOfMessages).ToArray();
        await messageProducerProvider.SendMessagesAsync(messages, cancellationToken);
    }

    private async Task SendMessagesWithDelayAsync(string? messageText, int numberOfMessages, TimeSpan timeDelay, CancellationToken cancellationToken = default)
    {
        if (numberOfMessages <= 0 || timeDelay <= TimeSpan.Zero || string.IsNullOrWhiteSpace(messageText))
            return;

        try
        {
            for (var i = 0; i < numberOfMessages; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                
                await SendSingleMessageAsync(messageText, cancellationToken);
                logger.LogInformation("Message number {MessageNumber} for {TotalMessages} is sent", i + 1, numberOfMessages);
                await Task.Delay(timeDelay, cancellationToken);
            }
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("Task SendMessagesWithDelayAsync was canceled");
        }
    }
    
    
    public async ValueTask DisposeAsync()
    {
        await messageProducerProvider.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}