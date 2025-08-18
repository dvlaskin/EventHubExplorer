using Application.Utils;
using Domain.Interfaces.Providers;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Application.Services.MessageProducers;

public class BytesMessageProducer : MessageProducerBase<byte[]>
{
    private readonly ILogger<BytesMessageProducer> logger;
    private readonly MessageOptions? messageOptions;

    public BytesMessageProducer(
        ILogger<BytesMessageProducer> logger,
        IMessageProducerProvider messageProducerProvider,
        MessageOptions? messageOptions = null
    ) : base(messageProducerProvider, messageOptions)
    {
        this.logger = logger;
        this.messageOptions = messageOptions;
    }

    protected override byte[] ApplyEncodingOptions(string message)
    {
        if (messageOptions is null || messageOptions.UseGzipCompression is false)
            throw new InvalidOperationException("Compression is disabled, incorrect MessageProducer is used");
        
        return message.Compress();
    }

    protected override async Task SendSingleMessageAsync(byte[] message, CancellationToken cancellationToken)
    {
        await MessageProducerProvider.SendMessageAsync(message, cancellationToken);
    }

    protected override async Task SendBatchMessagesAsync(
        byte[] message, int numberOfMessages, CancellationToken cancellationToken
    )
    {
        var messages = MultipleMessages(message, numberOfMessages);
        await MessageProducerProvider.SendMessagesAsync(messages, cancellationToken);
    }

    protected override async Task SendMessagesWithDelayAsync(
        byte[] message, int numberOfMessages, TimeSpan timeDelay, CancellationToken cancellationToken
    )
    {
        try
        {
            var messages = MultipleMessages(message, numberOfMessages);
            await MessageProducerProvider.SendMessagesAsync(messages, timeDelay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Task SendMessagesWithDelayAsync was canceled");
        }
    }
}