using Domain.Models;

namespace Domain.Interfaces.Providers;

public interface IMessageConsumerProvider : IAsyncDisposable
{
    Task StartReceiveMessageAsync(Func<IncomingMessage, Task> onMessageReceived, CancellationToken cancellationToken);
    Task StopReceiveMessageAsync();
}