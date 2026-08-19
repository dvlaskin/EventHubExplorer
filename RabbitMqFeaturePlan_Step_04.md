# Шаг 4: Infrastructure — `RabbitMqConsumerProvider`

_Файл: `RabbitMqFeaturePlan_Step_04.md`_

**Статус:** ⬜ TODO
**Что:** Создать `RabbitMqConsumerProvider` — реализацию `IMessageConsumerProvider` на push-API (`IAsyncBasicConsumer`/`AsyncDefaultBasicConsumer`) с ручным подтверждением (`BasicAckAsync`), декларацией очереди/exchange и binding для Exchange.
**Зачем:** FR-030/031/032/033/034/042. Получение в реальном времени с декомпрессией (`CompressingEncoding.DecodeMessage`) и ручным ack (аналог peek-lock + Complete у ServiceBus).
**Файлы:**
- `src/Infrastructure/Providers/RabbitMqConsumerProvider.cs` — СОЗДАТЬ

**Зависит от:** Шаг 1 (Domain), Шаг 2 (пакет), Шаг 3 (паттерн подключения/declaration)
**Риски:**
- `EventHubMessage.SequenceNumber` (long) ← delivery tag (ulong): явное приведение `unchecked((long)deliveryTag)` с допущением, что tag не превышает long.MaxValue (раздел 14 спецификации).
- Тело `ReadOnlyMemory<byte>` в `HandleBasicDeliverAsync` **нужно копировать** до выхода из метода (предупреждение в документации API) — `CompressingEncoding.DecodeMessage` принимает `ReadOnlyMemory<byte>` и читает его синхронно до возврата, копия не требуется; но при передаче `body.ToArray()` в async-цепочку копию делаем явно.
- Обрыв соединения во время приёма: `ConnectionShutdownAsync` → лог ошибки «Connection lost. Receiving stopped.», consumer корректно завершается (FR-041). Автоматическое восстановление через `AutomaticRecoveryEnabled = true` (FR-042).
- Потокобезопасность: callback `onMessageReceived` вызывается из consumer-потока — он пишет в bounded `Channel` (`EventHubConsumerService`), это безопасно.
- Валидация для Exchange: пустые `RoutingKey`/`QueueName` → `InvalidOperationException` (раздел 11).
- Повторный `StartReceiveMessageAsync` без `Stop` — защита через `Interlocked.CompareExchange` на `isProcessing`.

**Детали реализации:**

Структура класса — клон `ServiceBusConsumerProvider` (поля, `Lazy<Task<...>>`, логирование, `isProcessing`, `DisposeAsync` без финализатора).

```csharp
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

    private Func<EventHubMessage, Task>? runMessageProcessing;
    private RabbitMqAsyncConsumer? consumer;
    private string? consumerTag;
    private volatile bool isProcessing;
    private volatile bool disposed;
```

**Подключение и channel** — аналогично продюсеру (Шаг 3): `ConnectionFactory { Uri = new Uri(config.ConnectionString), AutomaticRecoveryEnabled = true }` → `CreateConnectionAsync()` → `CreateChannelAsync()`.

**StartReceiveMessageAsync** — объявление сущности, подписка consumer, ожидание до остановки:
```csharp
    public async Task StartReceiveMessageAsync(Func<EventHubMessage, Task> onMessageReceived, CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref isProcessing, true, false))
            return; // уже работает

        runMessageProcessing = onMessageReceived;
        logger.LogInformation("Start receiving messages from RabbitMQ {EntityType} {EntityName}", config.EntityType, config.EntityName);

        var ch = await channel.Value;
        var consumeQueue = await PrepareEntityAsync(ch);   // declare + bind; возвращает имя очереди для consume

        consumer = new RabbitMqAsyncConsumer(ch, this, onMessageReceived);
        consumerTag = await ch.BasicConsumeAsync(consumeQueue, autoAck: false, consumer: consumer, cancellationToken);
        logger.LogInformation("Consumer registered with tag {ConsumerTag} on queue {Queue}", consumerTag, consumeQueue);

        // удерживаем задачу активной до явной остановки или обрыва соединения
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // остановка
        }
        finally
        {
            isProcessing = false;
        }
    }

    public async Task StopReceiveMessageAsync()
    {
        logger.LogInformation("Stop receiving messages from RabbitMQ {EntityName}", config.EntityName);
        try
        {
            if (consumerTag is not null && channel.IsValueCreated)
            {
                var ch = await channel.Value;
                await ch.BasicCancelAsync(consumerTag);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error while cancelling consumer {ConsumerTag}", consumerTag);
        }
        finally
        {
            consumerTag = null;
            isProcessing = false;
        }
    }
```

**Подготовка сущности** (FR-025/031). Возвращает имя очереди для consume:
```csharp
    private async Task<string> PrepareEntityAsync(IChannel ch)
    {
        if (config.EntityType == RabbitMqEntityType.Queue)
        {
            await ch.QueueDeclareAsync(config.EntityName, durable: true, exclusive: false, autoDelete: false);
            return config.EntityName;
        }

        // Exchange: требуется QueueName и RoutingKey для binding
        if (string.IsNullOrWhiteSpace(config.QueueName))
            throw new InvalidOperationException("Queue name is required for Exchange entity type");
        if (config.ExchangeType != RabbitMqExchangeType.Fanout && string.IsNullOrWhiteSpace(config.RoutingKey))
            throw new InvalidOperationException("Routing key is required for Exchange entity type");

        await ch.ExchangeDeclareAsync(config.EntityName, GetExchangeTypeString(config.ExchangeType), durable: true, autoDelete: false);
        await ch.QueueDeclareAsync(config.QueueName, durable: true, exclusive: false, autoDelete: false);
        await ch.QueueBindAsync(config.QueueName, config.EntityName, config.RoutingKey ?? string.Empty);
        return config.QueueName;
    }
```
`GetExchangeTypeString` — как в Шаге 3 (`direct`/`fanout`/`topic`).

**Consumer** (вложенный private sealed класс в `RabbitMQ.Client.Events`):
```csharp
    private sealed class RabbitMqAsyncConsumer : AsyncDefaultBasicConsumer
    {
        private readonly RabbitMqConsumerProvider owner;
        private readonly Func<EventHubMessage, Task> onMessageReceived;

        public RabbitMqAsyncConsumer(IChannel channel, RabbitMqConsumerProvider owner, Func<EventHubMessage, Task> onMessageReceived)
            : base(channel)
        {
            this.owner = owner;
            this.onMessageReceived = onMessageReceived;
        }

        public override async Task HandleBasicDeliverAsync(
            string consumerTag, ulong deliveryTag, bool redelivered, string exchange,
            string routingKey, IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default)
        {
            var msgData = new EventHubMessage
            {
                Message = CompressingEncoding.DecodeMessage(body, owner.config),
                EnqueuedTime = DateTimeOffset.UtcNow,
                SequenceNumber = unchecked((long)deliveryTag),
                PartitionId = routingKey
            };

            try
            {
                if (owner.runMessageProcessing is not null)
                    await owner.runMessageProcessing(msgData);

                await Channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                owner.logger.LogError(ex, "Error processing message from RabbitMQ {EntityName}, deliveryTag {DeliveryTag}", owner.config.EntityName, deliveryTag);
            }
        }
    }
```

**Обрыв соединения** (FR-041): подписаться на `connection.ConnectionShutdownAsync` (или событие `ConnectionShutdown` — проверить сигнатуру в 7.x). При shutdown, не инициированном нашим `Stop`/`Dispose`, залогировать и уведомить:
```csharp
    private void AttachShutdownHandlers()
    {
        // при создании connection: conn.ConnectionShutdownAsync += HandleConnectionShutdownAsync;
    }

    private async Task HandleConnectionShutdownAsync(object sender, ShutdownEventArgs e)
    {
        if (!disposed && isProcessing)
            logger.LogError("Connection lost. Receiving stopped. RabbitMQ {EntityName}", config.EntityName);
        isProcessing = false;
    }
```

**DisposeAsync** (паттерн ServiceBusConsumerProvider, без финализатора):
```csharp
    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;
        disposed = true;
        isProcessing = false;

        if (channel.IsValueCreated)
        {
            var ch = await channel.Value;
            await ch.CloseAsync();
            await ch.DisposeAsync();
        }

        if (connection.IsValueCreated)
        {
            var conn = await connection.Value;
            await conn.CloseAsync();
            await conn.DisposeAsync();
        }

        GC.SuppressFinalize(this);
        logger.LogInformation("RabbitMqConsumerProvider is Disposed");
    }
```

> Примечание: точные имена событий/методов (`ConnectionShutdownAsync`, `CloseAsync`, `DisposeAsync`, `BasicCancelAsync`) сверить по IntelliSense пакета RabbitMQ.Client 7.2.2 и скорректировать при расхождении. Если `IChannel` не имеет `DisposeAsync` — использовать `Dispose()`.

**Критерии завершения:**
- `dotnet build EventHubExplorer.sln` без ошибок.
- Queue mode: очередь декларируется (durable) и consume идёт с `autoAck: false`, после обработки — `BasicAckAsync`.
- Exchange mode: exchange + queue + binding декларируются, consume из `QueueName`.
- При обрыве соединения — лог ошибки, без падения circuit.