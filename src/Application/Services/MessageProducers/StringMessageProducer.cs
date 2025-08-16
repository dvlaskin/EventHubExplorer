using Application.Utils;
using Domain.Interfaces.Providers;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Application.Services.MessageProducers;

public class StringMessageProducer : MessageProducerBase<string>
{
    private readonly ILogger<StringMessageProducer> logger;
    private readonly MessageOptions? messageOptions;

    public StringMessageProducer(
        ILogger<StringMessageProducer> logger,
        IMessageProducerProvider messageProducerProvider,
        MessageOptions? messageOptions = null
    ) : base(messageProducerProvider)
    {
        this.logger = logger;
        this.messageOptions = messageOptions;
    }

    protected override string ApplyOptions(string message)
    {
        if (messageOptions is null || messageOptions.UseGzipCompression is false)
            return message;
        
        return message.Compress().EncodeBase64();
    }

    protected override async Task SendSingleMessageAsync(string message, CancellationToken cancellationToken)
    {
        await MessageProducerProvider.SendMessageAsync(message, cancellationToken);
    }

    protected override async Task SendBatchMessagesAsync(
        string message, int numberOfMessages, CancellationToken cancellationToken
    )
    {
        var messages = MultipleMessages(message, numberOfMessages);
        await MessageProducerProvider.SendMessagesAsync(messages, cancellationToken);
    }

    protected override async Task SendMessagesWithDelayAsync(
        string message, int numberOfMessages, TimeSpan timeDelay, CancellationToken cancellationToken
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