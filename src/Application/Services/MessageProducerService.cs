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
    

    public async Task SendMessagesAsync(string? messageText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageText))
            return;
        
        await messageProducerProvider.SendMessagesAsync(messageText, cancellationToken);
    }

    public async Task SendMessagesAsync(string? messageText, int numberOfMessages, CancellationToken cancellationToken = default)
    {
        if (numberOfMessages <= 0 || string.IsNullOrWhiteSpace(messageText))
            return;

        var messages = Enumerable.Repeat(messageText, numberOfMessages).ToArray();
        await messageProducerProvider.SendMessagesAsync(messages, cancellationToken);
    }

    public async IAsyncEnumerable<int> SendMessagesWithDelayAsync(string? messageText, int numberOfMessages, TimeSpan timeDelay, CancellationToken cancellationToken = default)
    {
        if (numberOfMessages <= 0 || timeDelay <= TimeSpan.Zero || string.IsNullOrWhiteSpace(messageText))
            yield break;
        
        for (var i = 0; i < numberOfMessages; i++)
        {
            await SendMessagesAsync(messageText, cancellationToken);
            logger.LogInformation("Message number {MessageNumber} is sending", i + 1);
            await Task.Delay(timeDelay, cancellationToken);
            
            yield return i + 1;
        }
    }
    
    
    public async ValueTask DisposeAsync()
    {
        await messageProducerProvider.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}