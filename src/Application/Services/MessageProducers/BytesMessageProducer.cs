using Application.Utils;
using Domain.Interfaces.Providers;
using Domain.Models;

namespace Application.Services.MessageProducers;

public class BytesMessageProducer : BaseMessageProducer<byte[]>
{
    private readonly MessageOptions? messageOptions;

    public BytesMessageProducer(
        IMessageProducerProvider messageProducerProvider,
        MessageOptions? messageOptions = null
    ) : base(messageProducerProvider, messageOptions)
    {
        this.messageOptions = messageOptions;
    }

    protected override byte[] ApplyEncodingOptions(string message)
    {
        if (messageOptions is null || messageOptions.UseGzipCompression is false)
            throw new InvalidOperationException("Compression is disabled, incorrect MessageProducer is used");
        
        return message.Compress();
    }

    protected override BinaryData EncodeToBinaryData(byte[] message)
    {
        return BinaryData.FromBytes(message);
    }
}