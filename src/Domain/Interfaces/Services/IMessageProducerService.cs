namespace Domain.Interfaces.Services;

public interface IMessageProducerService : IAsyncDisposable
{
    Task SendMessagesAsync(string? messageText, CancellationToken cancellationToken = default);
    Task SendMessagesAsync(string? messageText, int numberOfMessages, CancellationToken cancellationToken = default);
    IAsyncEnumerable<int> SendMessagesWithDelayAsync(
        string? messageText, 
        int numberOfMessages, 
        TimeSpan timeDelay, 
        CancellationToken cancellationToken = default
    );
}