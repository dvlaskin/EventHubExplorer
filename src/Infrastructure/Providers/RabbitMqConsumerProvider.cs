using Application.Utils;
using Domain.Configs;
using Domain.Enums;
using Domain.Interfaces.Providers;
using Domain.Models;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Infrastructure.Providers;

public sealed class RabbitMqConsumerProvider : IMessageConsumerProvider
{
    private readonly ILogger<RabbitMqConsumerProvider> logger;
    private readonly RabbitMqConfig config;
    private readonly Lazy<Task<IConnection>> connection;
    private readonly Lazy<Task<IChannel>> channel;
    private readonly Lock lifecycleLock = new();

    private Func<IncomingMessage, Task>? runMessageProcessing;
    private RabbitMqAsyncConsumer? consumer;
    private string? consumerTag;
    private TaskCompletionSource<bool> stopSignal = CreateStopSignal();
    private volatile bool isProcessing;
    private volatile bool disposed;


    public RabbitMqConsumerProvider(ILogger<RabbitMqConsumerProvider> logger, RabbitMqConfig config)
    {
        this.logger = logger;
        this.config = config;
        connection = new Lazy<Task<IConnection>>(CreateConnectionAsync);
        channel = new Lazy<Task<IChannel>>(CreateChannelAsync);
    }


    public async Task StartReceiveMessageAsync(
        Func<IncomingMessage, Task> onMessageReceived, CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(onMessageReceived);

        if (Interlocked.CompareExchange(ref isProcessing, true, false))
            return;

        runMessageProcessing = onMessageReceived;
        stopSignal = CreateStopSignal();

        try
        {
            var rabbitChannel = await channel.Value;
            var queueName = await PrepareEntityAsync(rabbitChannel);
            consumer = new RabbitMqAsyncConsumer(rabbitChannel, this);
            consumerTag = await rabbitChannel.BasicConsumeAsync(
                queueName, autoAck: false, consumer, cancellationToken
            );

            logger.LogInformation(
                "Start receiving messages from RabbitMQ {EntityType} {EntityName}",
                config.EntityType, config.EntityName
            );

            await Task.WhenAny(
                Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken),
                stopSignal.Task
            );
        }
        finally
        {
            isProcessing = false;
        }
    }

    public async Task StopReceiveMessageAsync()
    {
        logger.LogInformation("Stop receiving messages from RabbitMQ {EntityName}", config.EntityName);
        stopSignal.TrySetResult(true);

        try
        {
            var activeConsumerTag = consumerTag;
            if (activeConsumerTag is not null && channel.IsValueCreated)
                await (await channel.Value).BasicCancelAsync(activeConsumerTag);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Error while cancelling RabbitMQ consumer {ConsumerTag}", consumerTag);
        }
        finally
        {
            consumerTag = null;
            isProcessing = false;
        }
    }

    private async Task<IConnection> CreateConnectionAsync()
    {
        logger.LogInformation("Creating RabbitMQ connection for {EntityName}", config.EntityName);
        var factory = new ConnectionFactory
        {
            Uri = new Uri(config.ConnectionString),
            AutomaticRecoveryEnabled = true
        };
        var rabbitConnection = await factory.CreateConnectionAsync();
        rabbitConnection.ConnectionShutdownAsync += HandleConnectionShutdownAsync;
        return rabbitConnection;
    }

    private async Task<IChannel> CreateChannelAsync()
    {
        var rabbitConnection = await connection.Value;
        return await rabbitConnection.CreateChannelAsync();
    }

    private async Task<string> PrepareEntityAsync(IChannel rabbitChannel)
    {
        if (config.EntityType == RabbitMqEntityType.Queue)
        {
            await rabbitChannel.QueueDeclareAsync(
                queue: config.EntityName, durable: true, exclusive: false, autoDelete: false
            );
            return config.EntityName;
        }

        if (string.IsNullOrWhiteSpace(config.QueueName))
            throw new InvalidOperationException("Queue name is required for RabbitMQ Exchange entity type");
        if (config.ExchangeType != RabbitMqExchangeType.Fanout && string.IsNullOrWhiteSpace(config.RoutingKey))
            throw new InvalidOperationException("Routing key is required for RabbitMQ Exchange entity type");

        await rabbitChannel.ExchangeDeclareAsync(
            exchange: config.EntityName, type: GetExchangeTypeString(config.ExchangeType), durable: true, autoDelete: false
        );

        await rabbitChannel.QueueDeclareAsync(
            queue: config.QueueName, durable: true, exclusive: false, autoDelete: false
        );

        await rabbitChannel.QueueBindAsync(
            queue: config.QueueName, exchange: config.EntityName, routingKey: config.RoutingKey ?? string.Empty
        );

        return config.QueueName;
    }

    private Task HandleConnectionShutdownAsync(object sender, ShutdownEventArgs args)
    {
        if (!disposed && isProcessing)
        {
            logger.LogError(
                "Connection lost. Receiving stopped. RabbitMQ {EntityName}", config.EntityName
            );
        }

        isProcessing = false;
        stopSignal.TrySetResult(true);
        return Task.CompletedTask;
    }

    private static TaskCompletionSource<bool> CreateStopSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static string GetExchangeTypeString(RabbitMqExchangeType type) => type switch
    {
        RabbitMqExchangeType.Direct => "direct",
        RabbitMqExchangeType.Fanout => "fanout",
        RabbitMqExchangeType.Topic => "topic",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public async ValueTask DisposeAsync()
    {
        lock (lifecycleLock)
        {
            if (disposed)
                return;
            disposed = true;
        }

        isProcessing = false;
        stopSignal.TrySetResult(true);

        if (channel.IsValueCreated)
            await (await channel.Value).DisposeAsync();
        if (connection.IsValueCreated)
        {
            var activeConnection = await connection.Value;
            activeConnection.ConnectionShutdownAsync -= HandleConnectionShutdownAsync;
            await activeConnection.DisposeAsync();
        }

        logger.LogInformation("RabbitMqConsumerProvider is Disposed");
        GC.SuppressFinalize(this);
    }

    private sealed class RabbitMqAsyncConsumer : AsyncDefaultBasicConsumer
    {
        private readonly RabbitMqConsumerProvider owner;

        public RabbitMqAsyncConsumer(IChannel channel, RabbitMqConsumerProvider owner) : base(channel)
        {
            this.owner = owner;
        }

        public override async Task HandleBasicDeliverAsync(
            string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IReadOnlyBasicProperties properties,
            ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                var message = new IncomingMessage
                {
                    Message = CompressingEncoding.DecodeMessage(body, owner.config),
                    EnqueuedTime = DateTimeOffset.UtcNow,
                    SequenceNumber = unchecked((long)deliveryTag),
                    PartitionId = routingKey,
                    Properties = properties.Headers?.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value is byte[] bytes 
                            ? System.Text.Encoding.UTF8.GetString(bytes) 
                            : kvp.Value ?? ""
                    ).AsReadOnly()
                };

                if (owner.runMessageProcessing is not null)
                    await owner.runMessageProcessing(message);

                await Channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                owner.logger.LogError(
                    ex,
                    "Error processing message from RabbitMQ {EntityName}, deliveryTag {DeliveryTag}",
                    owner.config.EntityName,
                    deliveryTag);

                try
                {
                    await Channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken);
                }
                catch (Exception nackEx) when (nackEx is not OperationCanceledException)
                {
                    owner.logger.LogWarning(
                        nackEx,
                        "Failed to reject message from RabbitMQ {EntityName}, deliveryTag {DeliveryTag}",
                        owner.config.EntityName,
                        deliveryTag
                    );
                }
            }
        }
    }
}
