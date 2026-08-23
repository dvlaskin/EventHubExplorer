using Domain.Models;

namespace Domain.Interfaces.Providers;

public interface IMessageConsumerProvider : IAsyncDisposable
{
    Task StartReceiveMessageAsync(Func<MessageRecord, Task> onMessageReceived, CancellationToken cancellationToken);
    Task StopReceiveMessageAsync();
}