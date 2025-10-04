namespace Domain.Interfaces.Services;

public interface IMessageProducerService : IAsyncDisposable
{
    Task SendMessagesAsync(
        string? messageText, uint numberOfMessages = 1, TimeSpan? delayToSend = null, CancellationToken cancellationToken = default
    );
}