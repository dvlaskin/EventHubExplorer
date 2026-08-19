# План: Поддержка RabbitMQ в EventHub Explorer

_Создан: 2026-08-19_
_Статус: В РАБОТЕ (Шаг 1 из 10 выполнен)_
_Источник: `Spec_RabbitMQ.md` (v1.0, Approved)_

## Цель

Добавить в EventHub Explorer четвёртую message bus — **RabbitMQ** (AMQP 0-9-1) — с полным функциональным паритетом с Service Bus: настройка конфигов (Queue/Exchange), отправка одиночных/пакетных сообщений с задержкой и форматированием (JSON/GUID/DateTime) и сжатием (gzip/base64), получение сообщений в реальном времени через push-API с ручным подтверждением, декомпрессией и JSON-форматированием. Используется существующая архитектура Clean Architecture и keyed-DI — **Application-слой не меняется (G4)**.

## Критерии успеха

- [ ] G1: Полный функциональный паритет с Service Bus / Storage Queue (отправка одиночных, батчей, с задержкой через UI; сообщения появляются в очереди RabbitMQ).
- [ ] G2: Получение сообщений в реальном времени с форматированием JSON и декомпрессией gzip/base64.
- [ ] G3: Поддержка сущностей Queue и Exchange (direct/fanout/topic) с routing key и привязкой очереди для получения.
- [ ] G4: Ноль изменений в `src/Application`.
- [ ] G5: Документация в `readme.md` (docker run, примеры AMQP URI).
- [ ] G6: Регрессия существующих шин (Event Hubs / Storage Queue / Service Bus) — приложение собирается и работает.

## Обзор архитектуры

Новая шина встраивается по уже существующему шаблону Service Bus без единого изменения в Application-слое:

```
Domain:  MessageBusType.RabbitMq (enum) + RabbitMqConfig : IFormattingConfig + RabbitMqEntityType + RabbitMqExchangeType + AppConfiguration.RabbitMqConfigs
Infrastructure: RabbitMqProducerProvider / RabbitMqConsumerProvider (RabbitMQ.Client 7.2.2)
                RabbitMqProducerFactory / RabbitMqConsumerFactory (клон ServiceBus-фабрик)
                keyed-DI: AddKeyedSingleton<...>(MessageBusType.RabbitMq)
WebUI:   Pages/RabbitMq.razor (клон ServiceBus.razor, route /rabbitmq/{id:guid})
         вкладка RabbitMQ в Configuration.razor, секция в NavMenu, карточка на Home, пример в appConfig.json
Docs:    раздел RabbitMQ в readme.md
```

Отправка переиспользует `StringMessageProducer`/`BytesMessageProducer` + `TextProcessingPipeline` + `CompressingEncoding`. Получение переиспользует `EventHubConsumerService` (Channel + `IAsyncEnumerable`) + форматтеры AfterReceive. Сопоставление моделей: **Queue ↔ Queue**, **Exchange ↔ Topic+Subscription** (для Exchange нужны `RoutingKey` и `QueueName` для binding).

Ключевые API RabbitMQ.Client 7.x (проверены по документации):
- `ConnectionFactory { Uri = new Uri(amqpUri), AutomaticRecoveryEnabled = true }` → `await factory.CreateConnectionAsync()`.
- `IConnection.CreateChannelAsync()` → `IChannel`.
- Declare: `channel.QueueDeclareAsync(queue, durable: true, ...)`, `channel.ExchangeDeclareAsync(exchange, type, durable: true, ...)`, `channel.QueueBindAsync(queue, exchange, routingKey)`.
- Publish: `channel.BasicPublishAsync(exchange, routingKey, mandatory: false, body)` (body = `ReadOnlyMemory<byte>`).
- Consume: `AsyncDefaultBasicConsumer` / `IAsyncBasicConsumer` + `channel.BasicConsumeAsync(queue, autoAck: false, consumer)` → consumerTag; ack: `channel.BasicAckAsync(deliveryTag, multiple: false)`.

## Затрагиваемые файлы

| Файл | Тип изменения | Описание |
|------|---------------|----------|
| `src/Domain/Enums/MessageBusType.cs` | ИЗМЕНИТЬ | Добавить `RabbitMq` |
| `src/Domain/Enums/RabbitMqEntityType.cs` | СОЗДАТЬ | enum Queue \| Exchange |
| `src/Domain/Enums/RabbitMqExchangeType.cs` | СОЗДАТЬ | enum Direct \| Fanout \| Topic |
| `src/Domain/Configs/RabbitMqConfig.cs` | СОЗДАТЬ | Конфиг подключения, `IFormattingConfig` |
| `src/Domain/Configs/AppConfiguration.cs` | ИЗМЕНИТЬ | `List<RabbitMqConfig> RabbitMqConfigs` |
| `src/Infrastructure/Infrastructure.csproj` | ИЗМЕНИТЬ | `RabbitMQ.Client` 7.2.2 |
| `src/Infrastructure/Providers/RabbitMqProducerProvider.cs` | СОЗДАТЬ | Отправка single/batch/delay + declare |
| `src/Infrastructure/Providers/RabbitMqConsumerProvider.cs` | СОЗДАТЬ | Push-приём, ручной ack, declare+bind |
| `src/Infrastructure/Factories/RabbitMqProducerFactory.cs` | СОЗДАТЬ | Клон ServiceBusProducerFactory |
| `src/Infrastructure/Factories/RabbitMqConsumerFactory.cs` | СОЗДАТЬ | Клон ServiceBusConsumerFactory |
| `src/Infrastructure/IoC/InfrastructureRegistration.cs` | ИЗМЕНИТЬ | 2 keyed-DI регистрации |
| `src/WebUI/Components/Pages/RabbitMq.razor` | СОЗДАТЬ | Страница `/rabbitmq/{id:guid}` |
| `src/WebUI/Components/Pages/RabbitMq.razor.css` | СОЗДАТЬ | Клон ServiceBus.razor.css |
| `src/WebUI/Components/Pages/Configuration.razor` | ИЗМЕНИТЬ | Вкладка RabbitMQ (CRUD, условные поля Exchange) |
| `src/WebUI/Components/Pages/Configuration.razor.css` | ИЗМЕНИТЬ | Стили вкладки RabbitMQ (если нужны) |
| `src/WebUI/Components/Layout/NavMenu.razor` | ИЗМЕНИТЬ | Секция RabbitMQ |
| `src/WebUI/Components/Layout/NavMenu.razor.cs` | ИЗМЕНИТЬ | Список RabbitMqConfigs + подписка + toggle |
| `src/WebUI/Components/Pages/Home.razor` | ИЗМЕНИТЬ | Бейдж + карточка RabbitMQ |
| `src/WebUI/Components/Pages/Home.razor.css` | ИЗМЕНИТЬ | Класс `service-card--rabbitmq` |
| `src/WebUI/Data/appConfig.json` | ИЗМЕНИТЬ | Пример `RabbitMqConfigs` |
| `readme.md` | ИЗМЕНИТЬ | Раздел про RabbitMQ |

## Шаги

| # | Шаг | Файл плана шага |
|---|-----|-----------------|
| 1 | Domain: enum'ы, `RabbitMqConfig`, `AppConfiguration.RabbitMqConfigs` | [`RabbitMqFeaturePlan_Step_01.md`](RabbitMqFeaturePlan_Step_01.md) |
| 2 | Infrastructure: пакет `RabbitMQ.Client` 7.2.2 | [`RabbitMqFeaturePlan_Step_02.md`](RabbitMqFeaturePlan_Step_02.md) |
| 3 | Infrastructure: `RabbitMqProducerProvider` | [`RabbitMqFeaturePlan_Step_03.md`](RabbitMqFeaturePlan_Step_03.md) |
| 4 | Infrastructure: `RabbitMqConsumerProvider` | [`RabbitMqFeaturePlan_Step_04.md`](RabbitMqFeaturePlan_Step_04.md) |
| 5 | Infrastructure: `RabbitMqProducerFactory` + `RabbitMqConsumerFactory` | [`RabbitMqFeaturePlan_Step_05.md`](RabbitMqFeaturePlan_Step_05.md) |
| 6 | Infrastructure: keyed-DI регистрация + сборка | [`RabbitMqFeaturePlan_Step_06.md`](RabbitMqFeaturePlan_Step_06.md) |
| 7 | WebUI: страница `RabbitMq.razor` + `.razor.css` | [`RabbitMqFeaturePlan_Step_07.md`](RabbitMqFeaturePlan_Step_07.md) |
| 8 | WebUI: вкладка RabbitMQ в `Configuration.razor` | [`RabbitMqFeaturePlan_Step_08.md`](RabbitMqFeaturePlan_Step_08.md) |
| 9 | WebUI: `NavMenu` + `Home.razor` + `appConfig.json` | [`RabbitMqFeaturePlan_Step_09.md`](RabbitMqFeaturePlan_Step_09.md) |
| 10 | Docs: раздел RabbitMQ в `readme.md` + финальная проверка | [`RabbitMqFeaturePlan_Step_10.md`](RabbitMqFeaturePlan_Step_10.md) |

## Открытые вопросы

- **OQ-1**: Prefetch (`BasicQosAsync`), таймауты подключения и TTL — вне MVP, используются дефолты клиента (паритет с другими шинами).
- **OQ-2**: Тестовый проект (xUnit + Testcontainers) — вне scope этой фичи (папка `tests/` пуста).
- **OQ-3**: Маскировать ли `ConnectionString` в UI — оставляем как есть (паритет, пароль в plaintext в `appConfig.json`).
- **OQ-4**: Рефакторинг четырёх страниц шин в одну генерируемую — сознательно отложен.

## Вне scope

- Изменение `compose.yaml`.
- Dead-letter queues, requeue, управление topology через Management HTTP API.
- RabbitMQ Streams, AMQP 1.0, другие протоколы.
- TLS-настройка в UI (только через `amqps://` в URI).
- Автоматизированные тесты.

## Порядок выполнения

Строго по номерам шагов: каждый следующий зависит от предыдущего. После шагов 1–6 — `dotnet build EventHubExplorer.sln` без ошибок. После шага 10 — полная сборка решения и ручная smoke-проверка с `rabbitmq:management` в Docker.
