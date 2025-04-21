using Domain.Models;

namespace Domain.Interfaces.Services;

public interface IMessageConsumerService : IAsyncDisposable
{
    IAsyncEnumerable<EventHubMessage> StartReceiveMessageAsync(CancellationToken cancellationToken = default);
    Task StopReceiveMessageAsync();
}