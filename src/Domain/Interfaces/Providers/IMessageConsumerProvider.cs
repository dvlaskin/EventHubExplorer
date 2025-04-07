namespace Domain.Interfaces.Providers;

public interface IMessageConsumerProvider
{
    Task StartReceiveMessageAsync(Func<string, Task> onMessageReceived, CancellationToken cancellationToken);
    Task StopReceiveMessageAsync();
}