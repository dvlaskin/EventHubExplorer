# План: OutgoingMessage — Properties сообщений для всех шин
_Создан: 2026-09-04_
_Статус: В РАБОТЕ (Шаг 5 из 7 выполнен)_
_Источник: `Spec_OutgoingMessage.md` v1.1 (Approved, OQ-1..OQ-3 resolved)_

## Цель

Дать инженерам возможность отправлять key-value свойства вместе с телом сообщения через единый шинно-независимый контракт, который переиспользуют 4 провайдера (EventHub, ServiceBus, RabbitMQ, StorageQueue), одновременно закрыв долг «>3 параметров в `IMessageProducerProvider`» через введение immutable envelope `OutgoingMessage`.

## Критерии успеха

- [ ] Сообщение, отправленное с N свойствами через EventHub, читается консумером с теми же N парами (`EventData.Properties`).
- [ ] `IMessageProducerProvider` не ссылается на типы `Azure.*` / `RabbitMQ.*`; все 4 провайдера компилируются против одного интерфейса; каждый публичный метод имеет ≤3 параметров.
- [ ] Пустые/null свойства отправляются бит-в-бит как сегодня (регрессий тела нет).
- [ ] StorageQueue с непустыми свойствами пишет warning (ключи/количество, без значений) и отправляет только тело, без исключений.
- [ ] На всех 4 страницах отправки один переиспользуемый `MessagePropertiesEditor`: по умолчанию свернут и пуст; add/update/remove без потери текста; на StorageQueue виден hint про игнорирование.
- [ ] `dotnet build EventHubExplorer.sln` зеленный; старые вызовы через `[Obsolete]`-шиммы компилируются с warning, а не с ошибкой.

## Обзор архитектуры

Цепочка `Blazor page → IMessageProducerService → BaseMessageProducer → IMessageProducerProvider → шина` сохраняется. Меняется только payload между звеньями:

1. **Domain:** два новых файла — `MessagePropertiesLimits` (константы 30/256/256, единственный источник лимитов) и `OutgoingMessage` (sealed record: `Message` + `MessageModifier` + `Properties`, валидация в конструкторе, defensive copy словаря).
2. **Domain-контракт (без DIM):** `IMessageProducerProvider` содержит ТОЛЬКО 3 новых метода на `OutgoingMessage` (≤3 параметров каждый, без дефолтных реализаций). Старые `(string message, Func<string, BinaryData>?)` перегрузки НЕ остаются в интерфейсе — они переезжают в отдельный `static class MessageProducerProviderObsoleteExtensions` с `[Obsolete]` extension-методами, упаковывающими аргументы в `new OutgoingMessage(message, messageModifier)` (`Properties` по умолчанию null) и делегирующими новым методам интерфейса. Провайдеры реализуют только новый контракт.
3. **Infrastructure:** каждый провайдер добавляет private helper `ApplyProperties`/`BuildNativeMessage` и вызывает его во всех 3 методах внутри существующего цикла/batching (`TryAdd` логика не меняется). RabbitMQ маппит в `BasicProperties.Headers`; StorageQueue только логирует warning.
4. **Application:** `IMessageProducerService.SendMessagesAsync` + `BaseMessageProducer.SendMessagesAsync` принимают опциональный `IReadOnlyDictionary<string, object>? properties`, собирают `new OutgoingMessage(messageText, CreateMessageModifier(), properties)`, диспетч single/batch/delayed как сегодня.
5. **WebUI:** новый shared-компонент `MessagePropertiesEditor.razor` (`IDictionary<string,string>` binding, `IsExpanded=false` по умолчанию, inline-валидация по `MessagePropertiesLimits`, лимит строк 30). 4 страницы (`EventHub/ServiceBus/RabbitMq/StorageQueue.razor`) хранят `Dictionary<string,string>`, конвертируют в `Dictionary<string,object>` при вызове сервиса. StorageQueue показывает статический hint.

SDK-типы не протекают в Domain/Application: валидация — только по allowlist (`string, byte[], int, long, double, bool, Guid, DateTimeOffset`), ошибки маппинга оборачиваются в `InvalidOperationException` с контекстом, сырые SDK-исключения наружу не пробрасываются.

## Затрагиваемые файлы

| Файл | Тип изменения | Описание |
|------|---------------|----------|
| `src/Domain/Models/MessagePropertiesLimits.cs` | СОЗДАТЬ | `static class` с `MaxPairs=30`, `MaxKeyLength=256`, `MaxValueLength=256` |
| `src/Domain/Models/OutgoingMessage.cs` | СОЗДАТЬ | `sealed record OutgoingMessage(Message, MessageModifier?, Properties?)` + валидация + defensive copy |
| `src/Domain/Interfaces/Providers/IMessageProducerProvider.cs` | ИЗМЕНИТЬ | Заменить 3 старые сигнатуры на 3 новых метода на `OutgoingMessage`; БЕЗ дефолтных реализаций |
| `src/Domain/Interfaces/Providers/MessageProducerProviderObsoleteExtensions.cs` | СОЗДАТЬ | `static class` с 3 `[Obsolete]` extension-методами старых перегрузок, делегирующими новым методам |
| `src/Infrastructure/Providers/EventHubProducerProvider.cs` | ИЗМЕНИТЬ | Helper `ApplyProperties(EventData, Properties)` × 3 метода; лог Information с количеством |
| `src/Infrastructure/Providers/ServiceBusProducerProvider.cs` | ИЗМЕНИТЬ | Helper для `ApplicationProperties` × 3 метода |
| `src/Infrastructure/Providers/RabbitMqProducerProvider.cs` | ИЗМЕНИТЬ | `BasicProperties.Headers` × 3 метода; передача props в `PublishAsync` |
| `src/Infrastructure/Providers/StorageQueueProducerProvider.cs` | ИЗМЕНИТЬ | Warning + игнор × 3 метода |
| `src/Domain/Interfaces/Services/IMessageProducerService.cs` | ИЗМЕНИТЬ | Параметр `IReadOnlyDictionary<string,object>? properties = null` |
| `src/Application/Services/MessageProducers/BaseMessageProducer.cs` | ИЗМЕНИТЬ | Сборка `OutgoingMessage` из текста + `CreateMessageModifier()` + properties; диспетч как сегодня |
| `src/WebUI/Components/Shared/MessagePropertiesEditor.razor` | СОЗДАТЬ | Сворачиваемая key-value таблица: show/hide, add/update/remove, inline-валидация 30/256, duplicate-check |
| `src/WebUI/Components/Pages/EventHub.razor` | ИЗМЕНИТЬ | Словарь свойств + редактор + проброс в сервис |
| `src/WebUI/Components/Pages/ServiceBus.razor` | ИЗМЕНИТЬ | То же |
| `src/WebUI/Components/Pages/RabbitMq.razor` | ИЗМЕНИТЬ | То же |
| `src/WebUI/Components/Pages/StorageQueue.razor` | ИЗМЕНИТЬ | То же + статический hint «свойства игнорируются» |

## Шаги

### Шаг 1: Создать Domain-типы `MessagePropertiesLimits` + `OutgoingMessage`
**Статус:** ✅ DONE
**Что:** Создать `src/Domain/Models/MessagePropertiesLimits.cs` (константы) и `src/Domain/Models/OutgoingMessage.cs` (sealed record с валидацией на границе).
**Зачем:** Это фундамент, от которого зависят интерфейс, провайдеры, Application и UI-валидация; лимиты в одном месте (FR-011).
**Файлы:** `src/Domain/Models/MessagePropertiesLimits.cs`, `src/Domain/Models/OutgoingMessage.cs`
**Зависит от:** нет
**Риски:** Спорные типы (float, DateTime) — запретить изначально, разрешить только allowlist из FR-008 (`string, byte[], int, long, double, bool, Guid, DateTimeOffset`); переполнение лимитов — `ArgumentException` до обращения к шине.
**Детали реализации:**
- `MessagePropertiesLimits`: `public static class` с `public const int MaxPairs = 30; MaxKeyLength = 256; MaxValueLength = 256;` Неймспейс `Domain.Models`, file-scoped namespace как в `MessageRecord.cs`.
- `OutgoingMessage`: `public sealed record OutgoingMessage(string Message, Func<string, BinaryData>? MessageModifier = null, IReadOnlyDictionary<string, object>? Properties = null)`. Явный конструктор с валидацией (primary ctor + тело):
  - `Message is null` → `ArgumentNullException(nameof(Message))` (пустая строка допустима — фильтрация `IsNullOrWhiteSpace` остается в `BaseMessageProducer`).
  - `Properties` != null: `Count > MaxPairs` → `ArgumentException("Too many properties (max 30).")`; для каждой пары: ключ null/empty/whitespace → `ArgumentException("Invalid message property: key must be a non-empty string.")`; `key.Length > MaxKeyLength` → `ArgumentException`; значение null → `ArgumentException` (null-значения запрещены, т.к. `EventData.Properties` их не принимает); проверка типа по allowlist, иначе `ArgumentException("Property '{key}' has unsupported type '{type}'.")`; если значение `string` и `Length > MaxValueLength` → `ArgumentException`.
  - Defensive copy: сохранить `new Dictionary<string, object>(Properties)` (или `ReadOnlyDictionary`) чтобы провайдеры не могли мутировать входной словарь и ссылка не хранилась.
  - XML-doc комментарии для record и параметров (стиль проекта — см. AGENTS.md).
- Верификация: `dotnet build src/Domain/Domain.csproj` успешен.

**Заметки по реализации:**
- **Что сделано:** Созданы `MessagePropertiesLimits` (30/256/256) и `OutgoingMessage` (sealed record, валидация allowlist + лимитов в конструкторе, defensive copy через `ReadOnlyDictionary`, XML-doc). `dotnet build src/Domain/Domain.csproj` — 0 warnings, 0 errors.
- **Отклонения от плана:** Defensive copy через `ReadOnlyDictionary(new Dictionary(...))` вместо голого `Dictionary` — строже (защита от каста к `Dictionary`); валидация вынесена в `ValidateProperties`/`ValidateSingleProperty` для лимита ~20 строк/метод по ai-code-standards.
- **Ключевые места:** `MessagePropertiesLimits` в `src/Domain/Models/MessagePropertiesLimits.cs`; `OutgoingMessage.ctor` в `src/Domain/Models/OutgoingMessage.cs`
- **Важно знать:** `float`/`DateTime` запрещены на границе (только allowlist); пустой `Message` допустим, фильтрация остается в `BaseMessageProducer`. Имена полей приведены к существующей терминологии провайдеров: `Message` / `MessageModifier` (переименование пользователя от 2026-09-05, порядок ctor: `Message, MessageModifier, Properties`).

---

### Шаг 2: Заменить контракт `IMessageProducerProvider` + `[Obsolete]` extension-шиммы (без DIM)
**Статус:** ✅ DONE
**Что:** Заменить 3 старые сигнатуры интерфейса на 3 новых метода на `OutgoingMessage` (без дефолтных реализаций); создать `MessageProducerProviderObsoleteExtensions` — `static class` с 3 `[Obsolete]` extension-методами старых перегрузок, делегирующими новым.
**Зачем:** Единый шинно-независимый контракт (G2), закрытие долга «4 параметра», обратная совместимость для внешних потребителей (FR-012, риск из §14 спеки) — без default interface methods, по требованию заказчика.
**Файлы:** `src/Domain/Interfaces/Providers/IMessageProducerProvider.cs`, `src/Domain/Interfaces/Providers/MessageProducerProviderObsoleteExtensions.cs`
**Зависит от:** Шаг 1
**Риски:** Extension-методы не полиморфны — external-код, держащий ссылку строго на `IMessageProducerProvider`, по-прежнему компилируется (вызов резолвится в extension), но mock-и старых сигнатур придется обновить; провайдеры реализуют только 3 новых метода, дублирования делегирования в 4 классах нет.
**Детали реализации:**
```csharp
// IMessageProducerProvider.cs — только новый контракт, без тел методов:
Task SendMessageAsync(OutgoingMessage message, CancellationToken cancellationToken = default);
Task SendMessagesAsync(OutgoingMessage message, uint numberOfMessages = 1, CancellationToken cancellationToken = default);
Task SendMessagesWithDelayAsync(OutgoingMessage message, uint numberOfMessages = 1, TimeSpan sendDelay = default, CancellationToken cancellationToken = default);
```
```csharp
// MessageProducerProviderObsoleteExtensions.cs
public static class MessageProducerProviderObsoleteExtensions
{
    [Obsolete("Use the OutgoingMessage overload. Will be removed in a future release.")]
    public static Task SendMessageAsync(this IMessageProducerProvider provider, string message, Func<string, BinaryData>? messageModifier = null, CancellationToken cancellationToken = default)
        => provider.SendMessageAsync(new OutgoingMessage(message, messageModifier), cancellationToken);

    [Obsolete("Use the OutgoingMessage overload. Will be removed in a future release.")]
    public static Task SendMessagesAsync(this IMessageProducerProvider provider, string message, Func<string, BinaryData>? messageModifier = null, uint numberOfMessages = 1, CancellationToken cancellationToken = default)
        => provider.SendMessagesAsync(new OutgoingMessage(message, messageModifier), numberOfMessages, cancellationToken);

    [Obsolete("Use the OutgoingMessage overload. Will be removed in a future release.")]
    public static Task SendMessagesWithDelayAsync(this IMessageProducerProvider provider, string message, Func<string, BinaryData>? messageModifier = null, uint numberOfMessages = 1, TimeSpan sendDelay = default, CancellationToken cancellationToken = default)
        => provider.SendMessagesWithDelayAsync(new OutgoingMessage(message, messageModifier), numberOfMessages, sendDelay, cancellationToken);
}
```
- `using Domain.Models;` в обоих файлах; неймспейс интерфейса `Domain.Interfaces.Providers` без изменений.
- Каждый новый метод интерфейса — строго ≤3 параметров (стандарт кода из G3). Никаких тел методов в интерфейсе.
- Верификация: `dotnet build src/Domain/Domain.csproj`; существующие 4 провайдера при этом НЕ компилируются (ожидаемо — чинятся в шагах 3–4).

**Заметки по реализации:**
- **Что сделано:** Интерфейс заменен на 3 метода на `OutgoingMessage` без тел (без DIM); создан `MessageProducerProviderObsoleteExtensions` с 3 `[Obsolete]` extension-шиммами (`new OutgoingMessage(message, messageModifier)`, `Properties` по умолчанию null). `dotnet build src/Domain/Domain.csproj` — 0 warnings, 0 errors.
- **Отклонения от плана:** Нет. Уточнение по лимиту параметров: `SendMessagesWithDelayAsync` имеет 4 параметра с `CancellationToken` (3 бизнес-параметра + CT) — CT не считается, долг «4 параметра» закрыт (было 4 бизнес + CT).
- **Ключевые места:** `IMessageProducerProvider` и `MessageProducerProviderObsoleteExtensions` в `src/Domain/Interfaces/Providers/`
- **Важно знать:** Полный `dotnet build EventHubExplorer.sln` ожидаемо красный: 12× CS0535 в 4 провайдерах (чинятся в шагах 3–4); `BaseMessageProducer` уже резолвится через шиммы — 3× CS0618 warning, ошибок нет. Это подтверждает работу шиммов.

---

### Шаг 3: Маппинг свойств в `EventHubProducerProvider` + `ServiceBusProducerProvider`
**Статус:** ✅ DONE
**Что:** Реализовать 3 новых метода в обоих провайдерах: encode тела через `MessageModifier ?? BinaryData.FromString(Message)`, скопировать `Properties` в нативное поле, сохранить batch/delay логику.
**Зачем:** G1 + FR-003/FR-004 — главная ценность фичи для EventHub-инженеров; ServiceBus — ближайший аналог по SDK-семантике, удобно делать парой.
**Файлы:** `src/Infrastructure/Providers/EventHubProducerProvider.cs`, `src/Infrastructure/Providers/ServiceBusProducerProvider.cs`
**Зависит от:** Шаг 2
**Риски:** `EventData.Properties.TryAdd` / `ServiceBusMessage.ApplicationProperties.TryAdd` могут вернуть false из-за размера batch — НЕ менять логику разбиения, переиспользовать существующий `TryAdd`-фолбэк как есть; значения не из allowlist уже отсечены в `OutgoingMessage`, повторная проверка не нужна.
**Детали реализации:**
- EventHub, private helper:
```csharp
private static EventData CreateEventData(OutgoingMessage message)
{
    var data = message.MessageModifier is null ? new EventData(message.Message) : new EventData(message.MessageModifier(message.Message));
    if (message.Properties is not null)
        foreach (var (k, v) in message.Properties)
            try { data.Properties.TryAdd(k, v); }
            catch (Exception ex) { throw new InvalidOperationException($"Failed to map property '{k}'.", ex); }
    return data;
}
```
  Вызвать в `SendMessageAsync` (1 событие), в цикле `SendMessagesAsync` (batch) и `SendMessagesWithDelayAsync` (по одному). Логи: `LogInformation("... sent with {PropertyCount} properties", count)` — только количество, без значений (NFR Observability).
- ServiceBus, аналогичный helper `CreateServiceBusMessage(OutgoingMessage)` с `new ServiceBusMessage(binaryData)` + цикл по `ApplicationProperties.TryAdd(k, v)` + та же обертка ошибок. Batch-логика `CreateMessageBatchAsync`/`TryAddMessage`/пересоздание батча — без изменений.
- Не мутировать `message.Properties`; не логировать значения выше Debug.
- Верификация: `dotnet build src/Infrastructure/Infrastructure.csproj` (RabbitMQ/StorageQueue еще красные — нормально до шага 4).

**Заметки по реализации:**
- **Что сделано:** Оба провайдера переведены на 3 новых метода на `OutgoingMessage`; добавлены private static helpers `CreateEventData` (EventHub, `EventData.Properties`) и `CreateServiceBusMessage` (ServiceBus, `ApplicationProperties`) с копированием `Properties` через `TryAdd` в try/catch → `InvalidOperationException("Failed to map property '{key}'.")`; batch/delay/`TryAdd`-фолбэк без изменений; логи `Information` только с `{PropertyCount}` без значений.
- **Отклонения от плана:** Нет. Параметр `CancellationToken` переименован в `ct` по сигнатуре интерфейса из шага 2.
- **Ключевые места:** `EventHubProducerProvider.CreateEventData()` в `src/Infrastructure/Providers/EventHubProducerProvider.cs`; `ServiceBusProducerProvider.CreateServiceBusMessage()` в `src/Infrastructure/Providers/ServiceBusProducerProvider.cs`
- **Важно знать:** `dotnet build src/Infrastructure/Infrastructure.csproj` частично красный как ожидалось: 6× CS0535 только в RabbitMQ/StorageQueue (было 12×), EventHub/ServiceBus компилируются; повторная проверка типов в провайдерах не нужна — allowlist уже enforced в `OutgoingMessage`.

---

### Шаг 4: Маппинг свойств в `RabbitMqProducerProvider` + игнор в `StorageQueueProducerProvider`
**Статус:** ✅ DONE
**Что:** RabbitMQ — проброс `Properties` в `BasicProperties.Headers`; StorageQueue — warning-лог и отправка только тела во всех 3 методах.
**Зачем:** FR-005/FR-006 + G4: закрыть контракт для всех 4 шин, чтобы интерфейс компилировался целиком.
**Файлы:** `src/Infrastructure/Providers/RabbitMqProducerProvider.cs`, `src/Infrastructure/Providers/StorageQueueProducerProvider.cs`
**Зависит от:** Шаг 2 (параллелен шагу 3 по коду, но выполняется после него чтобы build чинить по частям)
**Риски:** Сигнатура `BasicPublishAsync` с headers зависит от версии `RabbitMQ.Client` — перед кодированием проверить в Solution фактическую версию и поле (`IBasicProperties.Headers` vs `BasicProperties` в v7); UI передает только string, но провайдер обязан принять весь allowlist — сложное отклонять уже на границе (в `OutgoingMessage`), здесь только копия.
**Детали реализации:**
- RabbitMQ: расширить `PublishAsync(IChannel, ReadOnlyMemory<byte>, IReadOnlyDictionary<string,object>?, CancellationToken)` — внутри создать `BasicProperties` (конкретный класс по версии SDK), скопировать `Headers = new Dictionary<string, object?>(properties)` (копия на публикацию, не хранить ссылку), передать в `BasicPublishAsync(..., basicProperties: props, ...)`. `CreateMessage(OutgoingMessage)` возвращает `BinaryData` через `MessageModifier ?? FromString`. Все 3 метода прокидывают `message.Properties`. Логи — количество свойств, без значений.
- StorageQueue: в начале каждого из 3 методов:
```csharp
if (message.Properties is { Count: > 0 })
    logger.LogWarning("Storage Queue {QueueName} does not support properties — ignoring {PropertyCount} properties ({Keys}).", config.QueueName, message.Properties.Count, string.Join(",", message.Properties.Keys));
```
  Далее существующий код отправки тела без изменений (encode через `MessageModifier ?? FromString`). Без исключений.
- Верификация: `dotnet build EventHubExplorer.sln` успешен полностью (все 4 провайдера против нового интерфейса, старые вызовы из Application пока идут через `[Obsolete]`-шиммы с warnings — допустимо до шага 5).

**Заметки по реализации:**
- **Что сделано:** `RabbitMqProducerProvider` переведен на 3 новых метода на `OutgoingMessage`; `PublishAsync` расширен до `(IChannel, ReadOnlyMemory<byte>, IReadOnlyDictionary<string,object>?, CancellationToken)` — внутри `new BasicProperties()` + копия `Headers` на каждую публикацию, передача в `BasicPublishAsync(..., basicProperties: props, ...)`; `CreateMessage(OutgoingMessage)` возвращает `BinaryData` через `MessageModifier ?? FromString`; логи только с `{PropertyCount}`. `StorageQueueProducerProvider` переведен на 3 новых метода: warning с ключами без значений в начале каждого метода, далее отправка только тела без изменений, без исключений.
- **Отклонения от плана:** Warning-дубликация StorageQueue вынесена в private helper `LogPropertiesIgnored(OutgoingMessage)` вместо 3 inline-копий (DRY по ai-code-standards, вызывается в начале каждого метода как в плане); копия headers через `ToDictionary(kv => kv.Key, kv => (object?)kv.Value)` вместо `new Dictionary<string, object?>(properties)` — убран warning CS8620 nullability (`Headers` в v7 — `IDictionary<string, object?>`); параметр `CancellationToken` именован `ct` по сигнатуре интерфейса из шага 2.
- **Ключевые места:** `RabbitMqProducerProvider.PublishAsync()` / `CreateMessage()` в `src/Infrastructure/Providers/RabbitMqProducerProvider.cs`; `StorageQueueProducerProvider.LogPropertiesIgnored()` в `src/Infrastructure/Providers/StorageQueueProducerProvider.cs`
- **Важно знать:** Проверена фактическая версия `RabbitMQ.Client 7.2.2`: `CreateBasicProperties` удален, `BasicProperties` создается напрямую, сигнатура `BasicPublishAsync<TProperties>(exchange, routingKey, mandatory, basicProperties, body, ...)` подтверждена по документации; `dotnet build EventHubExplorer.sln` — 0 errors, только 3 ожидаемых CS0618 из `BaseMessageProducer` (шиммы, чинятся в шаге 5).

---

### Шаг 5: Application-слой — `IMessageProducerService` + `BaseMessageProducer`
**Статус:** ✅ DONE
**Что:** Добавить опциональный `IReadOnlyDictionary<string, object>? properties = null` в сервис и собирать `OutgoingMessage` с существующим `CreateMessageModifier()` как `MessageModifier`.
**Зачем:** Пробросить свойства от UI до провайдера без изменения диспетча single/batch/delayed (FR-007); `MessageModifier` переиспользует gzip/base64/pipeline как сегодня.
**Файлы:** `src/Domain/Interfaces/Services/IMessageProducerService.cs`, `src/Application/Services/MessageProducers/BaseMessageProducer.cs`
**Зависит от:** Шаги 3–4 (провайдеры готовы принимать envelope)
**Риски:** `messageText` null — как сегодня ранний return (валидация `Message not null` в record не конфликтует, т.к. сервис не создает envelope для пустого ввода); `StringMessageProducer`/`BytesMessageProducer` наследники не меняются (только базовый класс).
**Детали реализации:**
```csharp
// IMessageProducerService
Task SendMessagesAsync(string? messageText, uint numberOfMessages = 1, TimeSpan? delayToSend = null, IReadOnlyDictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
```
```csharp
// BaseMessageProducer.SendMessagesAsync
if (string.IsNullOrWhiteSpace(messageText)) return;
var envelope = new OutgoingMessage(messageText, CreateMessageModifier(), properties);
if (numberOfMessages <= 1) await messageProducerProvider.SendMessageAsync(envelope, cancellationToken)...
else if (delayToSend is null || ...) await ...SendMessagesAsync(envelope, numberOfMessages, cancellationToken)...
else await ...SendMessagesWithDelayAsync(envelope, numberOfMessages, delayToSend.Value, cancellationToken)...
```
- `using Domain.Models;` уже есть в `BaseMessageProducer.cs`; добавить в интерфейс.
- Ошибки `MessageModifier` пробрасывать без обертки (ответственность Application, §11 спеки).
- Верификация: `dotnet build EventHubExplorer.sln` без новых warnings кроме ожидаемых `[Obsolete]` (если страницы еще на старом сервисе — ок); ручной прогон отправки без свойств работает как раньше.

**Заметки по реализации:**
- **Что сделано:** В `IMessageProducerService.SendMessagesAsync` добавлен опциональный `IReadOnlyDictionary<string, object>? properties = null` перед `cancellationToken`; `BaseMessageProducer.SendMessagesAsync` собирает `new OutgoingMessage(messageText, CreateMessageModifier(), properties)` после раннего return на пустой ввод и диспетчит single/batch/delayed через новые методы провайдера без изменений логики; наследники `StringMessageProducer`/`BytesMessageProducer` не тронуты; ошибки `MessageModifier` идут без обертки.
- **Отклонения от плана:** Нет в коде. Но верификация шага неточна (зафиксировано по решению заказчика «строго по плану» от 2026-09-05): вставка `properties` перед CT ломает 4 позиционных вызова страниц — `dotnet build EventHubExplorer.sln` сейчас красный с 4× CS1503 в `EventHub/ServiceBus/RabbitMq/StorageQueue.razor` (4-й аргумент `CancellationToken` попадает в `properties`). Это ожидаемое переходное состояние, чинится в шаге 7. Побочный плюс: 3× CS0618 из `BaseMessageProducer` исчезли (шиммы больше не используются); Domain/Application/Infrastructure компилируются с 0 warnings.
- **Ключевые места:** `IMessageProducerService.SendMessagesAsync()` в `src/Domain/Interfaces/Services/IMessageProducerService.cs`; `BaseMessageProducer.SendMessagesAsync()` в `src/Application/Services/MessageProducers/BaseMessageProducer.cs`
- **Важно знать:** Сигнатура сервиса теперь 5 параметров (4 бизнес + CT) — исключение из правила «≤3 параметра», разрешено спекой; провайдерный долг «≤3» при этом закрыт. Ручной прогон без свойств до шага 7 невозможен из-за CS1503 — гнать после шага 7.

---

### Шаг 6: Shared-компонент `MessagePropertiesEditor`
**Статус:** ⬜ TODO
**Что:** Создать переиспользуемый Blazor-компонент: свернутая по умолчанию key-value таблица с add/update/remove и inline-валидацией по `MessagePropertiesLimits`.
**Зачем:** FR-010 + G5: компактный ввод свойств, один компонент на все страницы, лимиты читаются только из Domain (FR-011).
**Файлы:** `src/WebUI/Components/Shared/MessagePropertiesEditor.razor` (+ опционально `.razor.css` в стиле соседних компонентов)
**Зависит от:** Шаг 1 (лимиты)
**Риски:** Потеря текста сообщения при обновлении словаря — компонент биндится только к своему словарю, `MessageToSend` не трогает; дубли ключей — inline-ошибка «Duplicate key», строка не добавляется; значения в UI — только string (типизация не требуется, FR-008 string-ветка покрывает).
**Детали реализации:**
- Параметры: `[Parameter] public IDictionary<string,string> Value { get; set; } = new Dictionary...; [Parameter] public EventCallback<IDictionary<string,string>> ValueChanged; [Parameter] public bool IsExpanded { get; set; } = false;` Заголовок-кнопка `Properties (@Value.Count)` toggle show/hide с текстовой меткой (a11y: `aria-expanded`, keyboard-навигация, кнопки delete с текстом/aria-label).
- Строки: `key`/`value` inputs + кнопки add/remove; при add/update/remove вызывать `ValueChanged`. Inline-валидация: ключ пустой/whitespace, дубликат, `key.Length > MessagePropertiesLimits.MaxKeyLength`, `value.Length > MessagePropertiesLimits.MaxValueLength`, `Value.Count >= MaxPairs` → понятное сообщение, строка отклоняется («Too many properties (max 30).» и т.д. по §11).
- Стиль — Bootstrap-классы как в `MessageSendPanel.razor`; пустое начальное состояние; таблица растет по мере добавления.
- Верификация: `dotnet build src/WebUI/WebUI.csproj`; открыть любую страницу — редактор свернут и пуст; развернуть — add/edit/remove работают без перезагрузки и без потери текста.

---

### Шаг 7: Подключить редактор на 4 страницах + hint StorageQueue
**Статус:** ⬜ TODO
**Что:** На `EventHub/ServiceBus/RabbitMq/StorageQueue.razor`: хранить `Dictionary<string,string>`, рендерить `MessagePropertiesEditor`, конвертировать в `Dictionary<string,object>` и передавать в `IMessageProducerService.SendMessagesAsync`; на StorageQueue добавить статический hint.
**Зачем:** End-to-end замыкание (Phase 3 milestone спеки): свойства доходят от UI до шины; FR-013 — явное предупреждение про StorageQueue.
**Файлы:** `src/WebUI/Components/Pages/EventHub.razor`, `src/WebUI/Components/Pages/ServiceBus.razor`, `src/WebUI/Components/Pages/RabbitMq.razor`, `src/WebUI/Components/Pages/StorageQueue.razor`
**Зависит от:** Шаги 5–6
**Риски:** 4 почти идентичные правки — делать строго по одному шаблону (поле `private Dictionary<string, string> messageProperties = new();`, конвертация `messageProperties.ToDictionary(kv => kv.Key, kv => (object)kv.Value)` только в момент Send); не сломать существующие `@bind` истории/задержки; hint — статическая подсказка, не toast.
**Детали реализации (шаблон на страницу, на примере `EventHub.razor`):**
- В `@code`: `private Dictionary<string, string> messageProperties = new();`
- В `SendArea` после `MessageSendPanel`: `<MessagePropertiesEditor @bind-Value="messageProperties" />` (на StorageQueue ниже + `<div class="form-text">Storage Queue does not support properties — they will be ignored.</div>`).
- В `SendMessage()`: построить `IReadOnlyDictionary<string,object>? props = messageProperties.Count == 0 ? null : messageProperties.ToDictionary(kv => kv.Key, kv => (object)kv.Value);` и вызвать `SendMessagesAsync(messageToSend, numberMessagesToSend, delayTimeSpan, props, delayedSenderCts.Token)`.
- Проверить остальные 3 страницы имеют тот же `SendMessage`-паттерн (структура идентична `EventHub.razor:158-206`) — применить аналогично.
- Верификация: `dotnet build EventHubExplorer.sln` зеленный; ручной прогон: EventHub со свойствами → консумер видит пары; пустые свойства → как раньше; StorageQueue со свойствами → warning в логах + успешный toast + hint виден.

## Открытые вопросы

- [x] OQ-1 (формат ввода): решен — `MessagePropertiesEditor`, свернут по умолчанию (FR-010).
- [x] OQ-2 (лимиты): решены — 30/256/256 в `MessagePropertiesLimits` (FR-011).
- [x] OQ-3 (старые перегрузки): решены — `[Obsolete]`-шиммы с делегированием (FR-012).
- Шиммы без DIM — решено: интерфейс без дефолтных реализаций; `[Obsolete]`-шиммы живут в `MessageProducerProviderObsoleteExtensions` (требование заказчика).

## Вне scope

- Тесты любого вида — исключены по требованию заказчика (переопределяет дефолт skill «тестировать логику»).
- Новые метаданные (`PartitionKey`, `SessionId`, `RoutingKey`, `ContentType`) — `OutgoingMessage` спроектирован под их добавление полями без смены интерфейса, но сами поля не вводятся.
- Consumer-сторона: чтение/отображение свойств в UI получения — не входит.
- Удаление `[Obsolete]`-шиммов — отдельным решением вне scope.
- Смена версий Azure SDK / RabbitMQ.Client, изменения `compose.yaml` / инфраструктуры — нет.
