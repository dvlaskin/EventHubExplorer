using Domain.Models;

namespace Domain.Interfaces.Services;

public interface IMessageConsumerService : IAsyncDisposable
{
    IAsyncEnumerable<IncomingMessage> StartReceiveMessageAsync(CancellationToken cancellationToken = default);
    Task StopReceiveMessageAsync();
}