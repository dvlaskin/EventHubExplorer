# Шаг 5: Infrastructure — `RabbitMqProducerFactory` + `RabbitMqConsumerFactory`

_Файл: `RabbitMqFeaturePlan_Step_05.md`_

**Статус:** ✅ DONE
**Что:** Создать `RabbitMqProducerFactory` (реализация `IMessageProducerFactory`) и `RabbitMqConsumerFactory` (реализация `IMessageConsumerFactory`) — точные клоны ServiceBus-фабрик с подстановкой `RabbitMqConfig` и RabbitMQ-провайдеров.
**Зачем:** FR-023 (форматтеры/gzip через `BaseMessageProducer`), сборка продюсера/консьюмера с pipeline. Фабрики — мост между keyed-DI и конкретными провайдерами.
**Файлы:**
- `src/Infrastructure/Factories/RabbitMqProducerFactory.cs` — СОЗДАТЬ
- `src/Infrastructure/Factories/RabbitMqConsumerFactory.cs` — СОЗДАТЬ

**Зависит от:** Шаг 1, Шаг 3, Шаг 4
**Риски:**
- Не изобретать новый код — копировать `ServiceBusProducerFactory`/`ServiceBusConsumerFactory` построчно, заменив `ServiceBusConfig` → `RabbitMqConfig`, `ServiceBusProducerProvider` → `RabbitMqProducerProvider` и `config.CurrentValue.ServiceBusConfigs` → `config.CurrentValue.RabbitMqConfigs`.
- `GetTextProcessingPipeline`/`GetActiveMessageFormatters` дублируются в фабриках — это легаси-паттерн кодовой базы, копируем как есть (не рефакторим).
- Выбор продюсера: `BytesMessageProducer` при `UseGzipCompression: true && UseBase64Coding: false`, иначе `StringMessageProducer` — строго как в ServiceBus-фабрике.
- `ActivatorUtilities.CreateInstance<...>(serviceProvider, config)` — конфиг передаётся параметром конструктора провайдера.

**Детали реализации:**

**`RabbitMqProducerFactory.cs`** (клон `ServiceBusProducerFactory.cs`):
- Конструктор: `ILogger<RabbitMqProducerFactory>`, `IOptionsMonitor<AppConfiguration>`, `IServiceProvider`.
- `CreateProducer(Guid configId)`:
  ```csharp
  var rmqConfig = config.CurrentValue.RabbitMqConfigs.First(x => x.Id == configId);
  var rmqProducerProvider = ActivatorUtilities.CreateInstance<RabbitMqProducerProvider>(serviceProvider, rmqConfig);
  var textProcessingPipeline = GetTextProcessingPipeline(rmqConfig);

  var msgOptions = new MessageOptions
  {
      UseGzipCompression = rmqConfig.UseGzipCompression,
      UseBase64Coding = rmqConfig.UseBase64Coding,
      TextProcessingPipeline = textProcessingPipeline
  };

  if (msgOptions is { UseGzipCompression: true, UseBase64Coding: false })
      return ActivatorUtilities.CreateInstance<BytesMessageProducer>(serviceProvider, rmqProducerProvider, msgOptions);

  return ActivatorUtilities.CreateInstance<StringMessageProducer>(serviceProvider, rmqProducerProvider, msgOptions);
  ```
- `GetTextProcessingPipeline` / `GetActiveMessageFormatters` — идентичны ServiceBus (фильтр `MessageFormatterType.BeforeSend`).

**`RabbitMqConsumerFactory.cs`** (клон `ServiceBusConsumerFactory.cs`):
- `CreateConsumer(Guid configId)`:
  ```csharp
  var rmqConfig = config.CurrentValue.RabbitMqConfigs.First(x => x.Id == configId);
  var rmqConsumerProvider = ActivatorUtilities.CreateInstance<RabbitMqConsumerProvider>(serviceProvider, rmqConfig);
  var textProcessingPipeline = GetTextProcessingPipeline(rmqConfig);
  return ActivatorUtilities.CreateInstance<EventHubConsumerService>(serviceProvider, rmqConsumerProvider, textProcessingPipeline);
  ```
- `GetActiveMessageFormatters` — фильтр `MessageFormatterType.AfterReceive` (как ServiceBus).

**Критерии завершения:**
- `dotnet build EventHubExplorer.sln` без ошибок.
- Фабрики используют `RabbitMqConfig`/`RabbitMq*Provider`, никаких ссылок на `ServiceBus*` внутри RabbitMq-фабрик.
- Application-слой не изменён.

**Заметки по реализации:**
- **Что сделано:** Созданы `RabbitMqProducerFactory` и `RabbitMqConsumerFactory` — точные клоны ServiceBus-фабрик с подстановкой `RabbitMqConfig`, `RabbitMqProducerProvider`/`RabbitMqConsumerProvider` и `RabbitMqConfigs` из `AppConfiguration`. Producer выбирает `BytesMessageProducer` при gzip && !base64, иначе `StringMessageProducer`; Consumer собирает `EventHubConsumerService` с `ITextProcessingPipeline`.
- **Отклонения от плана:** Нет — реализация построчно следует ServiceBus-фабрикам, как и задумано в шаге.
- **Ключевые места:** `RabbitMqProducerFactory.CreateProducer()` в `src/Infrastructure/Factories/RabbitMqProducerFactory.cs` ~31; `RabbitMqConsumerFactory.CreateConsumer()` в `src/Infrastructure/Factories/RabbitMqConsumerFactory.cs` ~30.
- **Важно знать:** В продюсере `GetActiveMessageFormatters` фильтрует `MessageFormatterType.BeforeSend` (`.ToArray()` + `.Contains`), в консьюмере — `AfterReceive` (`.ToHashSet()` + `.Contains`) — это легаси-паттерн кодовой базы, скопирован как есть, не рефакторился. Обе фабрики регистрируются ключом `MessageBusType.RabbitMq` на шаге 6.