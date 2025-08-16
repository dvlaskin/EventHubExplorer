namespace Domain.Interfaces.Providers;

public interface IMessageProducerProvider : IAsyncDisposable
{
    Task SendMessageAsync(string message, CancellationToken cancellationToken);
    Task SendMessageAsync(byte[] message, CancellationToken cancellationToken);
    
    Task SendMessagesAsync(IReadOnlyList<string> messages, CancellationToken cancellationToken);
    Task SendMessagesAsync(IReadOnlyList<byte[]> messages, CancellationToken cancellationToken);
    
    Task SendMessagesAsync(IReadOnlyList<string> messages, TimeSpan timeDelay, CancellationToken cancellationToken);
    Task SendMessagesAsync(IReadOnlyList<byte[]> messages, TimeSpan timeDelay, CancellationToken cancellationToken);
}