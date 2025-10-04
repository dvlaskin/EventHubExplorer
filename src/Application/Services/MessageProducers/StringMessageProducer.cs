using Application.Utils;
using Domain.Interfaces.Providers;
using Domain.Models;

namespace Application.Services.MessageProducers;

public class StringMessageProducer : BaseMessageProducer<string>
{
    private readonly MessageOptions? messageOptions;

    public StringMessageProducer(
        IMessageProducerProvider messageProducerProvider,
        MessageOptions? messageOptions = null
    ) : base(messageProducerProvider, messageOptions)
    {
        this.messageOptions = messageOptions;
    }

    protected override string ApplyEncodingOptions(string message)
    {
        if (messageOptions is null || messageOptions.UseGzipCompression is false)
            return message;
        
        return message.Compress().EncodeBase64();
    }

    protected override BinaryData EncodeToBinaryData(string message)
    {
        return BinaryData.FromString(message);
    }
}