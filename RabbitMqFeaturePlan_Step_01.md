# Шаг 1: Domain — enum'ы, `RabbitMqConfig`, `AppConfiguration.RabbitMqConfigs`

_Файл: `RabbitMqFeaturePlan_Step_01.md`_

**Статус:** ✅ DONE
**Что:** Добавить в Domain-слой новые сущности для RabbitMQ: значение `RabbitMq` в `MessageBusType`, enum'ы `RabbitMqEntityType` и `RabbitMqExchangeType`, класс `RabbitMqConfig : IFormattingConfig` и список `RabbitMqConfigs` в `AppConfiguration`.
**Зачем:** Спецификация (раздел 8.2, FR-002/FR-003). Domain-слой — единственное место, где должны появиться типы; Application/Infrastructure/WebUI будут на них ссылаться.
**Файлы:**
- `src/Domain/Enums/MessageBusType.cs` — ИЗМЕНИТЬ
- `src/Domain/Enums/RabbitMqEntityType.cs` — СОЗДАТЬ
- `src/Domain/Enums/RabbitMqExchangeType.cs` — СОЗДАТЬ
- `src/Domain/Configs/RabbitMqConfig.cs` — СОЗДАТЬ
- `src/Domain/Configs/AppConfiguration.cs` — ИЗМЕНИТЬ

**Зависит от:** нет
**Риски:**
- Не забыть `required` на обязательных свойствах (паритет с `ServiceBusConfig`) — иначе JSON-десериализация `appConfig.json` молча создаст пустые конфиги.
- Имя enum-значения в `MessageBusType` — строго `RabbitMq` (не `RabbitMQ`), так как оно станет ключом keyed-DI и будет использоваться во всех слоях.
- `EntityType` по умолчанию `Queue` (значение 0) — порядок значений в enum не менять после публикации.

**Детали реализации:**

Стиль — файловый namespace, file-scoped, 4 пробела, `ImplicitUsings`/`Nullable` включены (как в соседних файлах).

1. `MessageBusType.cs` — добавить `RabbitMq` последним значением:
```csharp
public enum MessageBusType
{
    EventHub,
    StorageQueue,
    ServiceBus,
    RabbitMq
}
```

2. `RabbitMqEntityType.cs` (новый):
```csharp
namespace Domain.Enums;

public enum RabbitMqEntityType
{
    Queue,
    Exchange
}
```

3. `RabbitMqExchangeType.cs` (новый):
```csharp
namespace Domain.Enums;

public enum RabbitMqExchangeType
{
    Direct,
    Fanout,
    Topic
}
```

4. `RabbitMqConfig.cs` (новый) — полный клон структуры `ServiceBusConfig` плюс поля RabbitMQ. Свойства `IFormattingConfig` обязательны:
```csharp
using Domain.Enums;
using Domain.Interfaces;

namespace Domain.Configs;

public class RabbitMqConfig : IFormattingConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Title { get; set; }
    public required string ConnectionString { get; set; }
    public required string EntityName { get; set; }
    public RabbitMqEntityType EntityType { get; set; }
    public RabbitMqExchangeType ExchangeType { get; set; } = RabbitMqExchangeType.Direct;
    public string? RoutingKey { get; set; }
    public string? QueueName { get; set; }

    public bool UseGzipCompression { get; set; }
    public bool UseBase64Coding { get; set; }
    public Dictionary<string, bool> MessageFormatters { get; set; } = new();
    public MessagePageLayoutPreset MessagePageLayout { get; set; } = MessagePageLayoutPreset.TopSendReceiveBottomPayload;
}
```
Комментарий-документация: `ExchangeType`/`RoutingKey`/`QueueName` используются только при `EntityType == Exchange` (RoutingKey — для публикации и binding, QueueName — имя очереди для привязки при получении).

5. `AppConfiguration.cs` — добавить список:
```csharp
public List<RabbitMqConfig> RabbitMqConfigs { get; set; } = [];
```

**Критерии завершения:**
- `dotnet build EventHubExplorer.sln` собирается без ошибок.
- Новые типы доступны в namespace `Domain.Enums` / `Domain.Configs`.

**Заметки по реализации:**
- **Что сделано:** Добавлены `MessageBusType.RabbitMq`, enum'ы `RabbitMqEntityType` и `RabbitMqExchangeType`, конфигурация `RabbitMqConfig` с обязательными полями подключения и форматирования, а также `AppConfiguration.RabbitMqConfigs`.
- **Отклонения от плана:** Нет.
- **Ключевые места:** `RabbitMqConfig` в `src/Domain/Configs/RabbitMqConfig.cs`; `AppConfiguration.RabbitMqConfigs` в `src/Domain/Configs/AppConfiguration.cs`.
- **Важно знать:** `RabbitMqEntityType.Queue` остаётся значением по умолчанию, а `RabbitMqExchangeType.Direct` назначается явно.
