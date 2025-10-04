namespace Domain.Interfaces.Providers;

public interface IMessageProducerProvider : IAsyncDisposable
{
    Task SendMessageAsync(
        string message, Func<string, BinaryData>? messageModifier = null, CancellationToken cancellationToken = default
    );

    Task SendMessagesAsync(
        string message,
        Func<string, BinaryData>? messageModifier = null,
        uint numberOfMessages = 1,
        CancellationToken cancellationToken = default
    );

    Task SendMessagesWithDelayAsync(
        string message,
        Func<string, BinaryData>? messageModifier = null,
        uint numberOfMessages = 1,
        TimeSpan sendDelay = default,
        CancellationToken cancellationToken = default
    );
}