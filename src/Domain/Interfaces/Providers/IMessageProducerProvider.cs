using Domain.Models;

namespace Domain.Interfaces.Providers;

// TODO: Replace numberOfMessages and sendDelay to SendOptions object type 
public interface IMessageProducerProvider : IAsyncDisposable
{
    /// <summary>
    /// Send a single message to the message broker.
    /// </summary>
    /// <param name="message">Message body</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    Task SendMessageAsync(OutgoingMessage message, CancellationToken ct = default);

    /// <summary>
    /// Send multiple messages to the message broker.
    /// </summary>
    /// <param name="message">Message body</param>
    /// <param name="numberOfMessages">Number of messages to send</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    Task SendMessagesAsync(OutgoingMessage message, uint numberOfMessages = 1, CancellationToken ct = default);

    /// <summary>
    /// Send multiple messages to the message broker with a delay between each message.
    /// </summary>
    /// <param name="message">Message body</param>
    /// <param name="numberOfMessages">Number of messages to send</param>
    /// <param name="sendDelay">Delay between each message</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    Task SendMessagesWithDelayAsync(
        OutgoingMessage message, uint numberOfMessages = 1, TimeSpan sendDelay = default, CancellationToken ct = default
    );
}
