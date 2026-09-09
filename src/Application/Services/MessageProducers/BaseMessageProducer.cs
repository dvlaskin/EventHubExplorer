using Domain.Interfaces.Providers;
using Domain.Interfaces.Services;
using Domain.Models;

namespace Application.Services.MessageProducers;

public abstract class BaseMessageProducer<T> : IMessageProducerService
{
    private readonly IMessageProducerProvider messageProducerProvider;
    private readonly MessageOptions? messageOptions;
    private bool disposed;


    protected BaseMessageProducer(
        IMessageProducerProvider messageProducerProvider,
        MessageOptions? messageOptions = null
    )
    {
        this.messageProducerProvider = messageProducerProvider;
        this.messageOptions = messageOptions;
    }


    public async Task SendMessagesAsync(
        string? messageText,
        uint numberOfMessages = 1,
        TimeSpan? delayToSend = null,
        IReadOnlyDictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(messageText))
            return;

        var outgoingMessage = new OutgoingMessage(messageText, CreateMessageModifier(), properties);

        if (numberOfMessages <= 1)
        {
            await messageProducerProvider
                .SendMessageAsync(outgoingMessage, cancellationToken)
                .ConfigureAwait(false);
        }
        else if (delayToSend is null || delayToSend.Value <= TimeSpan.Zero)
        {
            await messageProducerProvider
                .SendMessagesAsync(outgoingMessage, numberOfMessages, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await messageProducerProvider.SendMessagesWithDelayAsync(
                outgoingMessage, numberOfMessages, delayToSend.Value, cancellationToken
            ).ConfigureAwait(false);
        }
    }


    private Func<string, BinaryData> CreateMessageModifier()
    {
        return messageInput =>
        {
            var formatterMessage = ApplyFormattingOptions(messageInput);
            var messageToSend = ApplyEncodingOptions(formatterMessage);
            return EncodeToBinaryData(messageToSend);
        };
    }

    protected virtual string ApplyFormattingOptions(string messageText)
    {
        return messageOptions?.TextProcessingPipeline?.Process(messageText) ?? messageText;
    }

    protected abstract T ApplyEncodingOptions(string message);
    protected abstract BinaryData EncodeToBinaryData(T message);


    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        await messageProducerProvider.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
        disposed = true;
    }

    ~BaseMessageProducer() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}