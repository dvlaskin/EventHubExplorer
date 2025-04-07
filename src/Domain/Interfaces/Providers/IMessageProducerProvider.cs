namespace Domain.Interfaces.Providers;

public interface IMessageProducerProvider : IAsyncDisposable
{
    Task SendMessagesAsync(string messageText, CancellationToken cancellationToken);
}