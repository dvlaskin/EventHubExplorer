using Domain.Configs;
using Domain.Enums;
using Domain.Interfaces.Providers;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Infrastructure.Providers;

public sealed class RabbitMqProducerProvider : IMessageProducerProvider
{
    private readonly ILogger<RabbitMqProducerProvider> logger;
    private readonly RabbitMqConfig config;
    private readonly Lazy<Task<IConnection>> connection;
    private readonly Lazy<Task<IChannel>> channel;
    private readonly SemaphoreSlim declarationLock = new(1, 1);
    private volatile bool declared;
    private bool disposed;


    public RabbitMqProducerProvider(ILogger<RabbitMqProducerProvider> logger, RabbitMqConfig config)
    {
        this.logger = logger;
        this.config = config;
        connection = new Lazy<Task<IConnection>>(CreateConnectionAsync);
        channel = new Lazy<Task<IChannel>>(CreateChannelAsync);
    }


    public async Task SendMessageAsync(
        string message, Func<string, BinaryData>? messageModifier = null, CancellationToken cancellationToken = default
    )
    {
        ValidateForExchange();
        var rabbitChannel = await GetChannelAsync();
        var binaryData = CreateMessage(message, messageModifier);

        await PublishAsync(rabbitChannel, binaryData.ToMemory(), cancellationToken);
        logger.LogInformation("Single message sent to RabbitMQ {EntityType} {EntityName}", config.EntityType, config.EntityName);
    }

    public async Task SendMessagesAsync(
        string message,
        Func<string, BinaryData>? messageModifier = null,
        uint numberOfMessages = 1,
        CancellationToken cancellationToken = default
    )
    {
        ValidateForExchange();
        var rabbitChannel = await GetChannelAsync();

        for (var i = 0; i < numberOfMessages; i++)
        {
            var binaryData = CreateMessage(message, messageModifier);
            await PublishAsync(rabbitChannel, binaryData.ToMemory(), cancellationToken);
        }

        logger.LogInformation(
            "Sent {MsgCount} messages to RabbitMQ {EntityType} {EntityName}",
            numberOfMessages, config.EntityType, config.EntityName
        );
    }

    public async Task SendMessagesWithDelayAsync(
        string message,
        Func<string, BinaryData>? messageModifier = null,
        uint numberOfMessages = 1,
        TimeSpan sendDelay = default,
        CancellationToken cancellationToken = default
    )
    {
        ValidateForExchange();
        var rabbitChannel = await GetChannelAsync();

        for (var i = 0; i < numberOfMessages; i++)
        {
            var binaryData = CreateMessage(message, messageModifier);
            await PublishAsync(rabbitChannel, binaryData.ToMemory(), cancellationToken);
            logger.LogInformation("Message number {MessageNumber} from {TotalMessages} sent", i + 1, numberOfMessages);

            if (sendDelay != TimeSpan.Zero)
                await Task.Delay(sendDelay, cancellationToken);
        }

        logger.LogInformation(
            "Sent all {MsgCount} messages to RabbitMQ {EntityType} {EntityName}",
            numberOfMessages, config.EntityType, config.EntityName
        );
    }


    private async Task<IConnection> CreateConnectionAsync()
    {
        logger.LogInformation("Creating RabbitMQ connection for {EntityName}", config.EntityName);
        var factory = new ConnectionFactory
        {
            Uri = new Uri(config.ConnectionString),
            AutomaticRecoveryEnabled = true
        };

        return await factory.CreateConnectionAsync();
    }

    private async Task<IChannel> CreateChannelAsync()
    {
        var rabbitConnection = await connection.Value;
        return await rabbitConnection.CreateChannelAsync();
    }

    private async Task<IChannel> GetChannelAsync()
    {
        var rabbitChannel = await channel.Value;
        await EnsureEntityDeclaredAsync(rabbitChannel);
        return rabbitChannel;
    }

    private async Task EnsureEntityDeclaredAsync(IChannel rabbitChannel)
    {
        if (declared)
            return;

        await declarationLock.WaitAsync();
        try
        {
            if (declared)
                return;

            if (config.EntityType == RabbitMqEntityType.Queue)
            {
                await rabbitChannel.QueueDeclareAsync(config.EntityName, durable: true, exclusive: false, autoDelete: false);
                logger.LogInformation("Declared queue {QueueName} (durable)", config.EntityName);
            }
            else
            {
                var exchangeType = GetExchangeTypeString(config.ExchangeType);
                await rabbitChannel.ExchangeDeclareAsync(config.EntityName, exchangeType, durable: true, autoDelete: false);
                logger.LogInformation("Declared {Type} exchange {ExchangeName} (durable)", exchangeType, config.EntityName);
            }

            declared = true;
        }
        finally
        {
            declarationLock.Release();
        }
    }

    private async Task PublishAsync(IChannel rabbitChannel, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        var exchange = config.EntityType == RabbitMqEntityType.Queue ? string.Empty : config.EntityName;
        var routingKey = config.EntityType == RabbitMqEntityType.Queue
            ? config.EntityName
            : config.RoutingKey ?? string.Empty;

        await rabbitChannel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            body: body,
            cancellationToken: cancellationToken
        );
    }

    private void ValidateForExchange()
    {
        if (
            config.EntityType == RabbitMqEntityType.Exchange
            && config.ExchangeType != RabbitMqExchangeType.Fanout
            && string.IsNullOrWhiteSpace(config.RoutingKey)
        )
        {
            throw new InvalidOperationException("Routing key is required for Exchange entity type");
        }
    }

    private static BinaryData CreateMessage(string message, Func<string, BinaryData>? messageModifier)
    {
        return messageModifier is null ? BinaryData.FromString(message) : messageModifier(message);
    }

    private static string GetExchangeTypeString(RabbitMqExchangeType type) => type switch
    {
        RabbitMqExchangeType.Direct => "direct",
        RabbitMqExchangeType.Fanout => "fanout",
        RabbitMqExchangeType.Topic => "topic",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        if (channel.IsValueCreated)
            await (await channel.Value).DisposeAsync();

        if (connection.IsValueCreated)
            await (await connection.Value).DisposeAsync();

        declarationLock.Dispose();
        logger.LogInformation("RabbitMqProducerProvider is Disposed");
        GC.SuppressFinalize(this);
        disposed = true;
    }

    ~RabbitMqProducerProvider() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
