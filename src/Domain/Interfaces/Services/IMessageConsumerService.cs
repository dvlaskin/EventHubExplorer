namespace Domain.Interfaces.Services;

public interface IMessageConsumerService : IAsyncDisposable
{
    IAsyncEnumerable<string> StartReceiveMessageAsync(CancellationToken cancellationToken = default);
    Task StopReceiveMessageAsync();
}