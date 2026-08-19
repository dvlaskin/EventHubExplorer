# Шаг 3: Infrastructure — `RabbitMqProducerProvider`

_Файл: `RabbitMqFeaturePlan_Step_03.md`_

**Статус:** ⬜ TODO
**Что:** Создать `RabbitMqProducerProvider` — реализацию `IMessageProducerProvider` для отправки одиночных/пакетных сообщений и батчей с задержкой, с декларацией очереди/exchange перед публикацией.
**Зачем:** FR-020/021/022/025. Провайдер — единственное место, где код касается RabbitMQ.Client при отправке; Application-слой остаётся нетронутым (G4).
**Файлы:**
- `src/Infrastructure/Providers/RabbitMqProducerProvider.cs` — СОЗДАТЬ

**Зависит от:** Шаг 1 (Domain), Шаг 2 (пакет)
**Риски:**
- `Lazy<Task<T>>` вместо `Lazy<T>`: `CreateConnectionAsync`/`CreateChannelAsync` асинхронны — оборачивать в `Lazy<Task<T>>` и `await channel.Value`.
- `mandatory: false` — не публиковать возврат невостребованных сообщений (подходит для dev-инструмента; иначе `BasicReturn` требует обработчика).
- Декларация (declare) — идемпотентна, но её надо выполнить **один раз** перед первой публикацией (флаг `declared`), не на каждое сообщение.
- `deliveryTag`/ack здесь не участвует (это consumer-сторона).
- `IChannel` не является `IAsyncDisposable` в 7.x — закрытие через `channel.CloseAsync()`/`channel.Dispose()` и `connection.CloseAsync()`/`connection.Dispose()`.
- При `EntityType == Exchange` и пустом `RoutingKey` (кроме Fanout) — бросать понятное исключение (раздел 11 спецификации).

**Детали реализации:**

Шаблон — точный клон `ServiceBusProducerProvider` (структура класса, логирование, `Lazy`, `DisposeAsync`, финализатор).

```csharp
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
    private volatile bool declared;
    private bool disposed;

    public RabbitMqProducerProvider(ILogger<RabbitMqProducerProvider> logger, RabbitMqConfig config)
    {
        this.logger = logger;
        this.config = config;
        connection = new Lazy<Task<IConnection>>(CreateConnectionAsync);
        channel = new Lazy<Task<IChannel>>(async () =>
        {
            var conn = await connection.Value;
            return await conn.CreateChannelAsync();
        });
    }
```

**Создание подключения** (`AutomaticRecoveryEnabled = true` — дефолт клиента; URI из `config.ConnectionString`):
```csharp
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
```

**Декларация перед первой отправкой** (FR-025):
```csharp
    private async Task EnsureEntityDeclaredAsync(IChannel ch)
    {
        if (declared)
            return;

        if (config.EntityType == RabbitMqEntityType.Queue)
        {
            // default exchange: очередь с именем EntityName
            await ch.QueueDeclareAsync(config.EntityName, durable: true, exclusive: false, autoDelete: false);
            logger.LogInformation("Declared queue {QueueName} (durable)", config.EntityName);
        }
        else
        {
            // exchange: декларируем exchange, публикуем с routing key (пустой для fanout)
            var exchangeType = GetExchangeTypeString(config.ExchangeType);
            await ch.ExchangeDeclareAsync(config.EntityName, exchangeType, durable: true, autoDelete: false);
            logger.LogInformation("Declared {Type} exchange {ExchangeName} (durable)", exchangeType, config.EntityName);
        }

        declared = true;
    }

    private static string GetExchangeTypeString(RabbitMqExchangeType type) => type switch
    {
        RabbitMqExchangeType.Direct => "direct",
        RabbitMqExchangeType.Fanout => "fanout",
        RabbitMqExchangeType.Topic => "topic",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };
```

**Валидация для Exchange** (раздел 11: «Routing key / Queue name is required for Exchange entity type»):
```csharp
    private void ValidateForExchange()
    {
        if (config.EntityType != RabbitMqEntityType.Exchange)
            return;

        if (config.ExchangeType != RabbitMqExchangeType.Fanout && string.IsNullOrWhiteSpace(config.RoutingKey))
            throw new InvalidOperationException("Routing key is required for Exchange entity type");
    }
```

**Методы отправки** — клонировать логику `ServiceBusProducerProvider.SendMessageAsync`/`SendMessagesAsync`/`SendMessagesWithDelayAsync`, заменив вызовы:

- `var binaryData = messageModifier is null ? BinaryData.FromString(message) : messageModifier(message);`
- `var body = binaryData.ToMemory();` (ReadOnlyMemory<byte>)
- `var ch = await channel.Value;` → `await EnsureEntityDeclaredAsync(ch);`
- Queue: `await ch.BasicPublishAsync(exchange: "", routingKey: config.EntityName, mandatory: false, body: body, cancellationToken: cancellationToken);`
- Exchange: `await ch.BasicPublishAsync(exchange: config.EntityName, routingKey: config.RoutingKey ?? string.Empty, mandatory: false, body: body, cancellationToken: cancellationToken);`

`SendMessagesAsync` — простой цикл (без Azure batch), т.к. RabbitMQ не требует батчинга (паритет с `StorageQueueProducerProvider`):
```csharp
    public async Task SendMessagesAsync(string message, Func<string, BinaryData>? messageModifier = null, uint numberOfMessages = 1, CancellationToken cancellationToken = default)
    {
        ValidateForExchange();
        var ch = await channel.Value;
        await EnsureEntityDeclaredAsync(ch);

        for (var i = 0; i < numberOfMessages; i++)
        {
            var binaryData = messageModifier is null ? BinaryData.FromString(message) : messageModifier(message);
            await PublishAsync(ch, binaryData.ToMemory(), cancellationToken);
        }
        logger.LogInformation("Sent {MsgCount} messages to RabbitMQ {EntityType} {EntityName}", numberOfMessages, config.EntityType, config.EntityName);
    }
```

`SendMessagesWithDelayAsync` — цикл с `if (sendDelay != TimeSpan.Zero) await Task.Delay(sendDelay, cancellationToken);` (как в ServiceBus).

`SendMessageAsync` — одиночная публикация с тем же Prepare + publish.

**DisposeAsync** (клонировать паттерн ServiceBus):
```csharp
    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

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

        logger.LogInformation("RabbitMqProducerProvider is Disposed");
        GC.SuppressFinalize(this);
        disposed = true;
    }

    ~RabbitMqProducerProvider() => DisposeAsync().AsTask().GetAwaiter().GetResult();
```

> Примечание: в RabbitMQ.Client 7.x `IChannel`/`IConnection` реализуют `IAsyncDisposable` и имеют `CloseAsync()`. На этапе реализации проверить точные сигнатуры через IntelliSense; при отсутствии `DisposeAsync()` использовать `Dispose()`.

**Критерии завершения:**
- Класс компилируется; `dotnet build EventHubExplorer.sln` без ошибок.
- Отправка в очередь публикует в default exchange с routing key = `EntityName`; в exchange — с routing key `RoutingKey`.
- Очередь/exchange декларируется один раз, `durable: true`.