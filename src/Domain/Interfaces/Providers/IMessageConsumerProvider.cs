using Domain.Models;

namespace Domain.Interfaces.Providers;

public interface IMessageConsumerProvider
{
    Task StartReceiveMessageAsync(Func<EventHubMessage, Task> onMessageReceived, CancellationToken cancellationToken);
    Task StopReceiveMessageAsync();
}