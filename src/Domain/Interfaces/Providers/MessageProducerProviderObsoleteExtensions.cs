using Domain.Models;

namespace Domain.Interfaces.Providers;

/// <summary>
/// Backward-compatibility shims for the pre-<see cref="OutgoingMessage"/> overloads.
/// Each shim packs its arguments into an <see cref="OutgoingMessage"/> with no properties
/// and delegates to the new contract method.
/// </summary>
public static class MessageProducerProviderObsoleteExtensions
{
    [Obsolete("Use the OutgoingMessage overload. Will be removed in a future release.")]
    public static Task SendMessageAsync(
        this IMessageProducerProvider provider,
        string message,
        Func<string, BinaryData>? messageModifier = null,
        CancellationToken cancellationToken = default
    ) => provider.SendMessageAsync(new OutgoingMessage(message, messageModifier), cancellationToken);

    [Obsolete("Use the OutgoingMessage overload. Will be removed in a future release.")]
    public static Task SendMessagesAsync(
        this IMessageProducerProvider provider,
        string message,
        Func<string, BinaryData>? messageModifier = null,
        uint numberOfMessages = 1,
        CancellationToken cancellationToken = default
    ) => provider.SendMessagesAsync(new OutgoingMessage(message, messageModifier), numberOfMessages, cancellationToken);

    [Obsolete("Use the OutgoingMessage overload. Will be removed in a future release.")]
    public static Task SendMessagesWithDelayAsync(
        this IMessageProducerProvider provider,
        string message,
        Func<string, BinaryData>? messageModifier = null,
        uint numberOfMessages = 1,
        TimeSpan sendDelay = default,
        CancellationToken cancellationToken = default
    ) => provider.SendMessagesWithDelayAsync(
        new OutgoingMessage(message, messageModifier), numberOfMessages, sendDelay, cancellationToken
    );
}
