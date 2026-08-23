using Domain.Models;

namespace Domain.Interfaces.Services;

public interface IMessageConsumerService : IAsyncDisposable
{
    IAsyncEnumerable<MessageRecord> StartReceiveMessageAsync(CancellationToken cancellationToken = default);
    Task StopReceiveMessageAsync();
}