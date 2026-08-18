# Feature Specification: Поддержка RabbitMQ в EventHub Explorer

---

## Document Metadata

| Field | Value                                                                                                                                                    |
|---|----------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Status** | Approved                                                                                                                                                 |
| **Version** | 1.0                                                                                                                                                      |
| **Created** | 2026-08-18                                                                                                                                               |
| **Last Updated** | 2026-08-18                                                                                                                                               |
| **Related Docs** | `readme.md`, `src/Domain/Enums/MessageBusType.cs`, `src/Infrastructure/IoC/InfrastructureRegistration.cs`, `src/WebUI/Components/Pages/ServiceBus.razor` |

---

## 1. Executive Summary

EventHub Explorer — это developer-tool с графическим интерфейсом для работы с message bus.
Сейчас приложение поддерживает три шины: Azure Event Hubs, Azure Storage Queues и 
Azure Service Bus. Цель фичи — добавить четвёртую шину: **RabbitMQ** (AMQP), полностью по аналогии с существующей поддержкой Service Bus. Пользователь сможет настраивать подключения к RabbitMQ (очередь или exchange с routing key), отправлять одиночные/пакетные сообщения, с задержкой, с форматированием (JSON/GUID/DateTime) и сжатием (gzip/base64), а также получать сообщения в реальном времени с форматированием JSON и декомпрессией. Архитектура Clean Architecture и keyed-DI паттерн позволяют добавить RabbitMQ без изменений в Application-слое: все существующие абстракции (`IMessageProducerFactory`, `IMessageConsumerFactory`, `IMessageProducerProvider`, `IMessageConsumerProvider`) переиспользуются как есть.

---

## 2. Background & Context

### 2.1 Problem Statement

Разработчики и тестировщики, использующие EventHub Explorer, работают с несколькими message bus. RabbitMQ — самый популярный self-hosted брокер сообщений (AMQP 0-9-1) в мире, который широко применяется в локальной разработке и production. Сейчас приложение не умеет работать с RabbitMQ, поэтому пользователям приходится использовать отдельные инструменты (RabbitMQ Management UI, rabbitmqadmin, сторонние IDE типа RabbitGUI), что нарушает единый workflow: отправка с форматированием/сжатием, просмотр полученных сообщений и история — доступны только внутри EventHub Explorer для трёх Azure-шин.

### 2.2 Business Motivation

- Замкнуть единый developer-workflow для всех популярных шин внутри одного инструмента.
- RabbitMQ — де-факто стандарт для локальной разработки и самохостинга; поддержка расширяет аудиторию инструмента.
- Архитектура приложения изначально спроектирована под pluggable-шины — добавление RabbitMQ дёшево и не трогает существующие сценарии.

### 2.3 Current State

Приложение построено по Clean Architecture (4 проекта: `Domain`, `Application`, `Infrastructure`, `WebUI`). Все три существующие шины подключены через единый каркас:

- **Domain**: общие интерфейсы (`IMessageProducerFactory`, `IMessageConsumerFactory`, `IMessageProducerProvider`, `IMessageConsumerProvider`, `IMessageProducerService`, `IMessageConsumerService`, `IFormattingConfig`), общая модель сообщения `EventHubMessage`, дискриминатор `MessageBusType` (enum), конфиги шин (`EventHubConfig`, `StorageQueueConfig`, `ServiceBusConfig`), корневой `AppConfiguration`.
- **Application**: полностью bus-agnostic — `EventHubConsumerService` (Channel + `IAsyncEnumerable`), `BaseMessageProducer<T>`/`StringMessageProducer`/`BytesMessageProducer`, `CompressingEncoding` (gzip/base64), форматтеры (`JsonFormatter`, `GuidReplacer`, `DateReplacer`, `DateTimeReplacer`), `TextProcessingPipeline`, история сообщений. **Ничего не меняется при добавлении новой шины.**
- **Infrastructure**: на каждую шину по 2 provider (`*ProducerProvider`, `*ConsumerProvider`) + 2 factory (`*ProducerFactory`, `*ConsumerFactory`), зарегистрированные как keyed singleton по `MessageBusType`.
- **WebUI**: на каждую шину своя страница (`/eventhub/{id}`, `/storagequeue/{id}`, `/servicebus/{id}`) с keyed-инъекцией фабрик, вкладка в `Configuration.razor`, секция в `NavMenu.razor`, карточка на `Home.razor`. Переиспользуемые компоненты: `MessageSendPanel`, `MessageReceivePanel`, `MessageBodyPanel`, `MessagePageLayout`, `MessageLayoutPresetSelector`, `ToastNotification`.

### 2.4 Proposed Solution Overview

Добавить RabbitMQ как четвёртую шину:

1. **Domain**: enum `MessageBusType.RabbitMq`, конфиг `RabbitMqConfig : IFormattingConfig`, enum `RabbitMqEntityType` (Queue/Exchange), enum `RabbitMqExchangeType` (Direct/Fanout/Topic), поле `RabbitMqConfigs` в `AppConfiguration`.
2. **Infrastructure**: NuGet `RabbitMQ.Client` (7.x), provider `RabbitMqProducerProvider`/`RabbitMqConsumerProvider`, factory `RabbitMqProducerFactory`/`RabbitMqConsumerFactory`, две keyed-DI регистрации.
3. **WebUI**: страница `/rabbitmq/{id:guid}` (клон `ServiceBus.razor`), вкладка в `Configuration.razor`, секция в `NavMenu.razor`, карточка на `Home.razor`, запись в `Data/appConfig.json`.
4. **Docs**: раздел про RabbitMQ в `readme.md` (docker run `rabbitmq:management`, примеры AMQP URI). `compose.yaml` **не меняется** — только документация.

Модель сущностей RabbitMQ отображается на модель ServiceBus: **Queue ↔ Queue**, **Exchange ↔ Topic** (для Exchange нужен routing key и имя очереди для привязки/потребления — аналог Subscription).

---

## 3. Goals & Non-Goals

### 3.1 Goals

| # | Goal | Success Metric |
|---|---|---|
| G1 | Полный функциональный паритет с Service Bus/Storage Queue: отправка одиночных, батчей, с задержкой | Пользователь может отправить N сообщений (в т.ч. с задержкой) через UI, сообщения появляются в очереди RabbitMQ |
| G2 | Получение сообщений в реальном времени с форматированием и декомпрессией | Сообщения отображаются в UI в реальном времени; JSON форматируется, gzip/base64 декодируется |
| G3 | Поддержка Queue и Exchange (direct/fanout/topic) сущностей с routing key | Пользователь может настроить оба типа сущностей и работать с ними |
| G4 | Переиспользование существующей архитектуры без изменений в Application | Ноль изменений в `src/Application` |
| G5 | Документация по локальному запуску RabbitMQ | Раздел в `readme.md` с командами docker и примерами AMQP URI |
| G6 | Все существующие шины продолжают работать | Регрессионная проверка Event Hubs / Storage Queue / Service Bus |

### 3.2 Non-Goals

- **Изменение `compose.yaml`** — локальный RabbitMQ запускается отдельной командой `docker run`, документируется в readme.
- **Автоматизированное тестирование** — в репозитории пустая папка `tests/`; добавление тест-проекта (в т.ч. Testcontainers) не входит в scope этой фичи. [OPEN QUESTION OQ-2]
- **Dead-letter queues, requeue, управление topology через Management API** — приложение декларирует (declare) очередь/exchange через клиент при подключении, без HTTP Management API.
- **RabbitMQ Streams, AMQP 1.0, другие протоколы** — только AMQP 0-9-1 через `RabbitMQ.Client`.
- **TLS/certificates настройка в UI** — используется AMQP URI; TLS-параметры задаются в самом URI (`amqps://`).

---

## 4. User Personas & Stories

### 4.1 Personas

| Persona | Description | Frequency of use |
|---|---|---|
| Backend-разработчик | Работает с RabbitMQ локально (Docker) или в dev-окружении, тестирует очереди/exchanges, отлаживает контракты сообщений | Daily |
| QA / тестировщик | Верифицирует сообщения, отправляемые/принимаемые приложением, проверяет формат, целостность, порядок | Weekly |

### 4.2 User Stories

#### Отправка сообщений в очередь

```
As a backend-разработчик,
I want to настроить подключение к RabbitMQ (очередь) и отправлять сообщения,
So that я могу тестировать свой consumer без сторонних утилит.

Acceptance Criteria:
  - GIVEN сконфигурирована очередь в RabbitMQ, WHEN я нажимаю «Send», THEN сообщение появляется в очереди (видно в Management UI)
  - GIVEN введено количество N, WHEN я отправляю батч, THEN в очередь попадает N сообщений
  - GIVEN задана задержка, WHEN я отправляю батч с задержкой, THEN сообщения отправляются с интервалом
  - GIVEN включены gzip/base64 и форматтеры, THEN сообщение приходит в преобразованном виде
```

#### Получение сообщений из очереди

```
As a QA-тестировщик,
I want to получать сообщения из очереди RabbitMQ в реальном времени,
So that я могу проверить содержимое и формат сообщений от producer.

Acceptance Criteria:
  - GIVEN очередь содержит сообщения, WHEN я нажимаю «Start receiving», THEN сообщения появляются в UI
  - GIVEN сообщение — JSON строка, THEN оно форматируется (pretty-print)
  - GIVEN включена декомпрессия, THEN gzip/base64 сообщение декодируется
  - GIVEN получение активно, WHEN я нажимаю «Stop», THEN поток останавливается
```

#### Работа с Exchange + routing key

```
As a backend-разработчик,
I want to отправлять сообщения в exchange с routing key и получать их через привязанную очередь,
So that я могу тестировать паттерны publish/subscribe (topic/fanout/direct).

Acceptance Criteria:
  - GIVEN настроен exchange типа topic и routing key, WHEN я отправляю сообщение, THEN оно публикуется в exchange с указанным routing key
  - GIVEN настроено имя очереди для привязки, WHEN я начинаю получать, THEN очередь декларируется, привязывается к exchange и сообщения приходят
```

---

## 5. Functional Requirements

### 5.1 Конфигурация (администрирование)

- `FR-001 [MUST]` Система SHALL предоставить вкладку «RabbitMQ» на странице `Configuration.razor` с CRUD для конфигов RabbitMQ (добавить/удалить, редактирование полей), по аналогии с вкладкой Service Bus.
- `FR-002 [MUST]` Конфиг RabbitMQ SHALL содержать поля: `Title`, `ConnectionString` (AMQP URI вида `amqp://user:pass@host:port/vhost`), `EntityName`, `EntityType` (Queue/Exchange).
- `FR-003 [MUST]` При `EntityType == Exchange` система SHALL показывать дополнительные поля: `ExchangeType` (Direct/Fanout/Topic), `RoutingKey`, `QueueName` (имя очереди для привязки при получении).
- `FR-004 [MUST]` При `EntityType == Queue` поля `RoutingKey`/`QueueName`/`ExchangeType` SHALL быть скрыты (аналог скрытия `SubscriptionName` для Queue в ServiceBus).
- `FR-005 [MUST]` Конфиг SHALL поддерживать стандартные для всех шин настройки: `UseGzipCompression`, `UseBase64Coding`, `MessageFormatters` (чекбоксы форматтеров), `MessagePageLayout`.
- `FR-006 [MUST]` Конфиги SHALL сохраняться в `Data/appConfig.json` через существующий `AppConfigurationProvider` (добавить список `RabbitMqConfigs` в `AppConfiguration`).
- `FR-007 [MUST]` Система SHALL валидировать конфиг при работе (пустой `ConnectionString`/`EntityName` подсвечиваются как незаполненные), по аналогии с существующими шинами.

### 5.2 Навигация и карточки

- `FR-010 [MUST]` Система SHALL показывать секцию «RabbitMQ» в `NavMenu.razor` со ссылками на каждый сконфигурированный RabbitMQ-конфиг (по заголовку), по аналогии с секцией Service Bus.
- `FR-011 [SHOULD]` Система SHALL показывать карточку RabbitMQ на главной странице `Home.razor`.
- `FR-012 [MUST]` Страница RabbitMQ SHALL быть доступна по маршруту `/rabbitmq/{id:guid}` и не конфликтовать с существующими маршрутами.

### 5.3 Отправка сообщений

- `FR-020 [MUST]` Система SHALL отправлять **одиночное** сообщение в очередь (через default exchange, routing key = имя очереди) или в exchange с указанным routing key.
- `FR-021 [MUST]` Система SHALL отправлять **батч** из N сообщений.
- `FR-022 [MUST]` Система SHALL отправлять батч **с задержкой** между сообщениями (единицы: sec/ms), по аналогии с другими шинами.
- `FR-023 [MUST]` Система SHALL применять к сообщению перед отправкой включённые `BeforeSend`-форматтеры (Guid replacer, Date replacer, Datetime replacer) и опции кодирования (gzip/base64) через существующий `BaseMessageProducer<T>`.
- `FR-024 [MUST]` Система SHALL вести историю отправленных сообщений на конфиг (дропдаун в `MessageSendPanel`) через существующий `FileBasedMessageHistory`.
- `FR-025 [MUST]` Система SHALL декларировать очередь/exchange (при необходимости — `durable`) перед первой отправкой, чтобы гарантировать существование сущности.

### 5.4 Получение сообщений

- `FR-030 [MUST]` Система SHALL получать сообщения из очереди в реальном времени через push-API (`IAsyncBasicConsumer`) с **ручным подтверждением** (`BasicAckAsync` после обработки), по аналогии с peek-lock + Complete у ServiceBus.
- `FR-031 [MUST]` При `EntityType == Exchange` система SHALL декларировать очередь `QueueName`, привязать её к exchange `EntityName` с `RoutingKey` и получать из неё сообщения (аналог Topic + Subscription).
- `FR-032 [MUST]` Каждое полученное сообщение SHALL отображаться в `CircularBuffer<EventHubMessage>` (ёмкость 50) с полями: `Message` (декодированный текст), `EnqueuedTime`, `SequenceNumber` (= delivery tag), `PartitionId` (= routing key сообщения).
- `FR-033 [MUST]` Система SHALL применять к полученным сообщениям `AfterReceive`-форматтер (Json formatter) и декодирование gzip/base64 через `CompressingEncoding.DecodeMessage`.
- `FR-034 [MUST]` Система SHALL поддерживать остановку получения (Stop) с корректным закрытием consumer/channel.
- `FR-035 [MUST]` Комбинация `UseBase64Coding: true` без `UseGzipCompression` SHALL быть неактивной: вкладка Configuration сбрасывает `UseBase64Coding` при выключенном gzip (аналог существующих шин), а `CompressingEncoding.DecodeMessage` при `UseGzipCompression: false` возвращает тело как UTF-8 без декодирования base64. Изменения в Application не требуются (G4).

### 5.5 Обработка ошибок

- `FR-040 [MUST]` Система SHALL показывать понятную ошибку в toast (`ToastNotification`) при: недоступности брокера, неверных учётных данных, несуществующем vhost, ошибке AMQP при отправке/получении.
- `FR-041 [MUST]` При обрыве соединения в процессе получения система SHALL логировать ошибку и показывать уведомление, не падая при этом (Blazor circuit не должен завершаться).
- `FR-042 [SHOULD]` Система SHALL переиспользовать автоматическое восстановление соединения RabbitMQ.Client (automatic connection recovery) в потребителе.

---

## 6. Non-Functional Requirements

### 6.1 Performance

| Metric | Target | Measurement Method |
|---|---|---|
| Задержка появления полученного сообщения в UI | ≤ 1 сек (в пределах сети до брокера) | Субъективная/хронометраж при ручной проверке |
| Пропускная способность отправки | ≥ 1000 сообщений/мин через UI | Ручной замер при батче |

> `[ASSUMPTION]` EventHub Explorer — интерактивный developer-tool, не высоконагруженный сервис. Численные цели не критичны; парадигма — интерактивный комфорт.

### 6.2 Scalability

- Требований к горизонтальному масштабированию нет — инструмент одноэкземплярный (Blazor Server), как и для остальных шин.
- Количество конфигов RabbitMQ в `appConfig.json` не ограничено.

### 6.3 Availability & Reliability

| Metric | Target |
|---|---|
| Поведение при недоступном брокере | Точечная ошибка в toast, приложение продолжает работать |
| Поведение при обрыве соединения | Автоматическое восстановление (recovery) там, где поддерживает клиент; корректное уведомление |
| Утечка ресурсов | Корректный `DisposeAsync` соединения, channel и consumer (паттерн Lazy + Dispose из существующих provider) |

### 6.4 Security

- **Authentication**: RabbitMQ credentials передаются в AMQP URI (`amqp://user:pass@host`), поддерживается `amqps://` для TLS.
- **Authorization**: на стороне брокера (пользователь/vhost). Приложение не добавляет своих ролей.
- **Data in transit**: TLS поддерживается через `amqps://` URI. `[ASSUMPTION]` Локальный docker-образ запускается по `amqp://`.
- **Секреты**: пароли хранятся в `Data/appConfig.json` в открытом виде — **то же поведение, что и у существующих шин** (connection strings Azure содержат ключи). `[OPEN QUESTION OQ-3]` Оставляем как есть для паритета.
- **Input validation**: минимальная (непустые обязательные поля), валидация URI — через исключение `RabbitMQ.Client` при создании подключения. Паритет с существующими шинами.

### 6.5 Observability

- **Logging**: `ILogger` в provider/factory (лог создания producer/consumer, ошибки подключения, ошибки приёма), по образцу существующих шин.
- **Metrics/Tracing**: не добавляются (паритет с существующими шинами).

---

## 7. Technical Architecture

### 7.1 System Context

```mermaid
graph TD
    U([Разработчик]) -->|Blazor Server / SignalR| WEB[WebUI / RabbitMq.razor]
    WEB -->|keyed DI: MessageBusType.RabbitMq| PF[RabbitMqProducerFactory]
    WEB -->|keyed DI: MessageBusType.RabbitMq| CF[RabbitMqConsumerFactory]
    PF -->|ActivatorUtilities| PP[RabbitMqProducerProvider]
    CF -->|ActivatorUtilities| CP[RabbitMqConsumerProvider]
    PP -->|BasicPublishAsync| RB[(RabbitMQ Broker / AMQP 0-9-1)]
    CP -->|BasicConsumeAsync / BasicAckAsync| RB
    RB --> MGMT[Management UI :15672]
    WEB --> APPCFG[(Data/appConfig.json)]
    PP --> PL[TextProcessingPipeline + BaseMessageProducer]
    CP --> CS[EventHubConsumerService]
    PL --> FMT[Форматтеры / gzip-base64 / история]
```

### 7.2 Component Breakdown

| Component | Responsibility | Technology |
|---|---|---|
| `RabbitMqConfig` | Конфигурация подключения (Domain) | C# класс, `IFormattingConfig` |
| `RabbitMqProducerProvider` | Отправка одиночных/батч/с задержкой, declare queue/exchange | RabbitMQ.Client `IChannel.BasicPublishAsync` |
| `RabbitMqConsumerProvider` | Получение через push-API, ручной ack, declare+bind для Exchange | RabbitMQ.Client `IAsyncBasicConsumer` |
| `RabbitMqProducerFactory` | Сборка продюсера + pipeline + выбор String/Bytes producer | `IMessageProducerFactory` |
| `RabbitMqConsumerFactory` | Сборка консьюмера + pipeline | `IMessageConsumerFactory` |
| `RabbitMq.razor` | Страница UI `/rabbitmq/{id:guid}` | Blazor Server |
| `Configuration.razor` | Вкладка конфигурации RabbitMQ | Blazor Server |
| `NavMenu.razor` / `Home.razor` | Навигация и карточка | Blazor Server |

### 7.3 Key User Flow — Отправка одиночного сообщения в очередь

```mermaid
sequenceDiagram
    actor User
    participant Page as RabbitMq.razor
    participant PF as RabbitMqProducerFactory
    participant BP as StringMessageProducer
    participant PP as RabbitMqProducerProvider
    participant RB as RabbitMQ

    User->>Page: Вводит текст, жмёт «Send»
    Page->>PF: CreateProducer(configId)
    PF->>PF: Читает RabbitMqConfig из IOptionsMonitor, строит TextProcessingPipeline
    PF-->>Page: IMessageProducerService
    Page->>BP: SendMessagesAsync(message, 1, null, ct)
    BP->>BP: CreateMessageModifier() → форматирование → gzip/base64 → BinaryData
    BP->>PP: SendMessageAsync(message, modifier, ct)
    PP->>PP: Lazy<IChannel>: declare queue (durable)
    PP->>RB: BasicPublishAsync(exchange="", routingKey=EntityName, body)
    RB-->>PP: OK
    PP-->>BP: completed
    BP-->>Page: completed
    Page-->>User: Toast «Сообщение отправлено» + история обновлена
```

### 7.4 Key User Flow — Получение сообщений (Exchange + binding)

```mermaid
sequenceDiagram
    actor User
    participant Page as RabbitMq.razor
    participant CF as RabbitMqConsumerFactory
    participant CS as EventHubConsumerService
    participant CP as RabbitMqConsumerProvider
    participant RB as RabbitMQ

    User->>Page: Жмёт «Start receiving»
    Page->>CF: CreateConsumer(configId)
    CF-->>Page: IMessageConsumerService (EventHubConsumerService)
    Page->>CS: StartReceiveMessageAsync(ct)
    CS->>CP: StartReceiveMessageAsync(onMessageReceived, ct)
    CP->>RB: ExchangeDeclareAsync(EntityName, ExchangeType)
    CP->>RB: QueueDeclareAsync(QueueName)
    CP->>RB: QueueBindAsync(QueueName, EntityName, RoutingKey)
    CP->>RB: BasicConsumeAsync(QueueName, autoAck=false)
    RB-->>CP: BasicDeliver (deliveryTag, routingKey, body)
    CP->>CP: DecodeMessage → EventHubMessage { Message, SequenceNumber=deliveryTag, PartitionId=routingKey }
    CP->>CS: onMessageReceived(message)
    CP->>RB: BasicAckAsync(deliveryTag)
    CS-->>Page: await foreach → UI обновление + форматирование JSON
```

### 7.5 Error Flow — Брокер недоступен

```mermaid
sequenceDiagram
    actor User
    participant Page as RabbitMq.razor
    participant BP as StringMessageProducer
    participant PP as RabbitMqProducerProvider

    User->>Page: Жмёт «Send»
    Page->>BP: SendMessagesAsync(...)
    BP->>PP: SendMessageAsync(...)
    PP->>PP: Попытка подключения (Lazy<IConnection>)
    PP-->>BP: Исключение BrokerUnreachableException / AuthenticationFailureException
    BP-->>Page: Исключение пробрасывается
    Page-->>User: Toast (DisplayErrorMessage) с текстом ошибки
```

### 7.6 Deployment Architecture

- RabbitMQ запускается пользователем отдельно (Docker-образ `rabbitmq:management`), см. readme. Приложение подключается по AMQP URI.
- Приложение по-прежнему собирается в Docker (существующий `Dockerfile`), изменения в инфраструктуре деплоя не требуются.

---

## 8. Data Model

### 8.1 Entity-Relationship Diagram

```mermaid
erDiagram
    AppConfiguration {
        list EventHubsConfigs
        list StorageQueuesConfigs
        list ServiceBusConfigs
        list RabbitMqConfigs
    }

    RabbitMqConfig {
        guid Id PK
        string Title
        string ConnectionString
        string EntityName
        enum EntityType
        enum ExchangeType
        string RoutingKey
        string QueueName
        bool UseGzipCompression
        bool UseBase64Coding
        dict MessageFormatters
        enum MessagePageLayout
    }

    AppConfiguration ||--o{ RabbitMqConfig : "содержит"

    RabbitMqConfig ||..o| Queue : "EntityType=Queue"
    RabbitMqConfig ||..o| Exchange : "EntityType=Exchange"
    Exchange ||--o{ Queue : "bind (RoutingKey + QueueName)"
```

### 8.2 Key Entities

#### `RabbitMqConfig` (новый, `src/Domain/Configs/RabbitMqConfig.cs`)

| Field | Type | Constraints | Description |
|---|---|---|---|
| `Id` | Guid | PK, default `Guid.NewGuid()` | Идентификатор конфига |
| `Title` | string | required | Отображаемое имя в NavMenu |
| `ConnectionString` | string | required | AMQP URI, напр. `amqp://guest:guest@localhost:5672` |
| `EntityName` | string | required | Имя очереди (Queue) или exchange (Exchange) |
| `EntityType` | `RabbitMqEntityType` | default Queue | Queue \| Exchange |
| `ExchangeType` | `RabbitMqExchangeType` | default Direct, используется при `EntityType==Exchange` | Direct \| Fanout \| Topic |
| `RoutingKey` | string? | nullable, используется при `EntityType==Exchange` | Routing key для публикации и привязки очереди |
| `QueueName` | string? | nullable, используется при `EntityType==Exchange` | Имя очереди для привязки и получения |
| `UseGzipCompression` | bool | — | `IFormattingConfig` |
| `UseBase64Coding` | bool | — | `IFormattingConfig` |
| `MessageFormatters` | Dictionary<string,bool> | default new() | `IFormattingConfig` |
| `MessagePageLayout` | `MessagePageLayoutPreset` | default TopSendReceiveBottomPayload | Раскладка страницы |

#### `MessageBusType` (изменение, `src/Domain/Enums/MessageBusType.cs`)

```csharp
public enum MessageBusType
{
    EventHub,
    StorageQueue,
    ServiceBus,
    RabbitMq   // NEW
}
```

#### `RabbitMqEntityType` (новый enum, `src/Domain/Enums/RabbitMqEntityType.cs`)

```csharp
public enum RabbitMqEntityType
{
    Queue,
    Exchange
}
```

#### `RabbitMqExchangeType` (новый enum, `src/Domain/Enums/RabbitMqExchangeType.cs`)

```csharp
public enum RabbitMqExchangeType
{
    Direct,
    Fanout,
    Topic
}
```

#### `AppConfiguration` (изменение, `src/Domain/Configs/AppConfiguration.cs`)

```csharp
public class AppConfiguration
{
    public List<EventHubConfig> EventHubsConfigs { get; set; } = [];
    public List<StorageQueueConfig> StorageQueuesConfigs { get; set; } = [];
    public List<ServiceBusConfig> ServiceBusConfigs { get; set; } = [];
    public List<RabbitMqConfig> RabbitMqConfigs { get; set; } = [];   // NEW
}
```

### 8.3 Data Lifecycle

| Data Type | Retention Policy | Deletion Strategy |
|---|---|---|
| `RabbitMqConfig` (в `appConfig.json`) | Пока пользователь не удалит | Удаление через UI (вкладка Configuration) |
| История сообщений (`messagesHistory.json`) | Пока пользователь не удалит | Удаление через UI (кнопка очистки истории на странице) |

---

## 9. API Design

### 9.1 Внутренние интерфейсы (не внешние HTTP API)

Приложение не имеет внешних API. RabbitMQ интегрируется через существующие абстракции — внешний интерфейс — это AMQP-протокол:

| Интерфейс | Метод | Использование RabbitMQ |
|---|---|---|
| `IMessageProducerProvider.SendMessageAsync(string, Func<string,BinaryData>?, ct)` | Отправить одно сообщение | `IChannel.BasicPublishAsync` |
| `IMessageProducerProvider.SendMessagesAsync(...)` | Отправить батч | Цикл `BasicPublishAsync` |
| `IMessageProducerProvider.SendMessagesWithDelayAsync(...)` | Батч с задержкой | Цикл + `Task.Delay(sendDelay)` |
| `IMessageConsumerProvider.StartReceiveMessageAsync(Func<EventHubMessage,Task>, ct)` | Начать получение | `IAsyncBasicConsumer` + `BasicAckAsync` |
| `IMessageConsumerProvider.StopReceiveMessageAsync()` | Остановить получение | Закрытие consumer/channel |

### 9.2 Карта AMQP-операций

| Сценарий | RabbitMQ операция (RabbitMQ.Client 7.x) |
|---|---|
| Send в Queue | `IChannel.QueueDeclareAsync(EntityName, durable: true)` → `BasicPublishAsync(exchange: "", routingKey: EntityName, body)` |
| Send в Exchange | `IChannel.ExchangeDeclareAsync(EntityName, type: ExchangeType)` → `BasicPublishAsync(exchange: EntityName, routingKey: RoutingKey, body)` |
| Receive из Queue | `BasicConsumeAsync(queue: EntityName, autoAck: false, consumer)` → `BasicAckAsync(deliveryTag, false)` |
| Receive из Exchange | `ExchangeDeclareAsync` → `QueueDeclareAsync(QueueName)` → `QueueBindAsync(QueueName, EntityName, RoutingKey)` → consume |

---

## 10. Security & Compliance

### 10.1 Authentication & Authorization

- Аутентификация на брокере через AMQP URI (`user:pass` в URI). Поддержка `amqps://` для TLS.
- Авторизация и виртуальные хосты — ответственность брокера.

### 10.2 Sensitive Data Handling

| Data Element | Classification | Storage | Transit | Access Control |
|---|---|---|---|---|
| `ConnectionString` (пароль RabbitMQ) | Secret | Plaintext в `Data/appConfig.json` (паритет с Azure шинами) | `amqp://` — TLS нет, `amqps://` — TLS | Локальный файл, права ОС |

### 10.3 Threat Model

| Threat | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Пароль RabbitMQ в открытом виде в файле конфига | Medium | Medium | Паритет с существующими шинами (Azure keys тоже в файле); рекомендуется `amqps://` и ограничение доступа к файлу |
| Подключение к недоверенному брокеру (`amqp://` без TLS) | Medium | Medium | Документировать в readme; поддержка `amqps://` |
| Чувствительные данные в сообщениях | Low | Medium | Инструмент локальный; история сообщений в `messagesHistory.json` — как у остальных шин |

### 10.4 Compliance Requirements

- Применимых регуляторных требований нет (локальный developer-инструмент). Паритет с существующими шинами.

---

## 11. Error Handling & Edge Cases

| Scenario | Expected Behavior | User-Facing Message |
|---|---|---|
| Брокер недоступен (`BrokerUnreachableException`) | Исключение при создании подключения → toast | Текст ошибки из исключения (DisplayErrorMessage) |
| Неверные учётные данные / нет доступа к vhost (`AuthenticationFailureException`) | Исключение при подключении → toast | Текст ошибки из исключения |
| `EntityType == Exchange`, но `RoutingKey`/`QueueName` пустые | Публикация с пустым routing key / невозможность bind → понятная ошибка | «Routing key / Queue name is required for Exchange entity type» (аналог ServiceBus: «Subscription name is required for Topic») |
| Отправка в несуществующую очередь (Queue mode) | Очередь декларируется автоматически (durable) перед отправкой | — (прозрачно) |
| Получение из несуществующей очереди (Queue mode) | Очередь декларируется автоматически при старте consumer | — (прозрачно) |
| Обрыв соединения во время приёма | Лог ошибки + toast, consumer корректно завершает работу, circuit не падает | «Connection lost. Receiving stopped.» |
| Сообщение слишком большое | Стандартное ограничение RabbitMQ (default 128MB); исключение при публикации → toast | Текст ошибки из исключения |
| Повторный старт получения при активном consumer | Защита от двойного запуска (`Interlocked.CompareExchange` в `EventHubConsumerService`) | — |
| `Task.Delay` задержки отменено | Отмена через `CancellationToken` (`delayedSenderCts`) | — |
| Невалидный AMQP URI | Исключение при разборе URI в `ConnectionFactory` → toast | Текст ошибки из исключения |

---

## 12. Testing Strategy

### 12.1 Unit Tests

- В scope фичи **не включено** автоматизированное тестирование — папка `tests/` в репозитории пуста, тестовых проектов нет. `[OPEN QUESTION OQ-2]`
- Покрытие обеспечивается ручной проверкой по сценариям из раздела 4.2 и acceptance criteria ниже.

### 12.2 Integration / E2E

- Ручное E2E: запуск `rabbitmq:management` в Docker, добавление конфига в UI, отправка/получение, проверка через Management UI (http://localhost:15672).

### 12.3 Acceptance Criteria

| Requirement | Acceptance Test | Pass Condition |
|---|---|---|
| FR-020/FR-021/FR-022 | Отправка single/batch/delay в очередь, проверка в Management UI | Сообщения видны в очереди, количество и интервалы корректны |
| FR-030/FR-032/FR-033 | Получение из очереди с JSON-форматированием и декомпрессией | Сообщения появляются в UI, формат корректный |
| FR-025/FR-031 | Работа с Exchange (topic) + routing key + привязка очереди | Сообщения доставляются в привязанную очередь, routing key отображается |
| FR-001–FR-006 | CRUD вкладки Configuration, сохранение в appConfig.json | Конфиг сохраняется и переживает рестарт приложения |
| FR-040–FR-042 | Ошибки подключения | Toast с понятным текстом, приложение не падает |
| G6 | Регрессия Event Hubs / Storage Queue / Service Bus | Все три существующие шины продолжают работать |

---

## 13. Implementation Plan

### Phase 1 — MVP: полный паритет с Service Bus (est. 1–2 спринта)

**Milestone**: RabbitMQ полностью функционален: настройка, отправка, получение, форматирование, документация.

| Task | Owner | Effort | Dependencies |
|---|---|---|---|
| Domain: `MessageBusType.RabbitMq`, `RabbitMqConfig`, `RabbitMqEntityType`, `RabbitMqExchangeType`, `AppConfiguration.RabbitMqConfigs` | Backend | S (1d) | — |
| Infrastructure: NuGet `RabbitMQ.Client` 7.x в `Infrastructure.csproj` | Backend | XS | — |
| Infrastructure: `RabbitMqProducerProvider` (declare + publish, single/batch/delay) | Backend | M (3d) | Domain готов |
| Infrastructure: `RabbitMqConsumerProvider` (push-API consumer, manual ack, exchange bind) | Backend | M (3d) | Domain готов |
| Infrastructure: `RabbitMqProducerFactory` / `RabbitMqConsumerFactory` (клон ServiceBus factories) | Backend | M (3d) | Providers готовы |
| Infrastructure: keyed-DI регистрация в `InfrastructureRegistration.cs` | Backend | XS | Factories готовы |
| WebUI: страница `RabbitMq.razor` + `.razor.css` (клон `ServiceBus.razor`) | Frontend | M (3d) | Factories готовы |
| WebUI: вкладка RabbitMQ в `Configuration.razor` (CRUD, условные поля Exchange) | Frontend | L (5d) | Domain готов |
| WebUI: секция в `NavMenu.razor` + `.cs`, карточка на `Home.razor` | Frontend | S (1d) | Страница готова |
| WebUI: запись примера конфига в `Data/appConfig.json` | Frontend | XS | — |
| Docs: раздел RabbitMQ в `readme.md` (docker run, AMQP URI, примеры) | Docs | S (1d) | — |
| Ручное E2E-тестирование и регрессия существующих шин | QA | S (1–2d) | Всё выше |

### Effort Legend

| Label | Range |
|---|---|
| XS | < 1 day |
| S | 1–2 days |
| M | 3–4 days |
| L | 5–8 days |
| XL | > 8 days (consider breaking down) |

---

## 14. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation | Owner |
|---|---|---|---|---|
| API-несовместимость RabbitMQ.Client 7.x (breaking changes относительно 6.x) | Medium | Medium | Использовать только стабильные async-API (BasicPublishAsync, IAsyncBasicConsumer, BasicAckAsync); проверить v7 migration guide | Backend |
| `EventHubMessage.SequenceNumber` (long) vs delivery tag (ulong) | Low | Medium | Явное приведение с документированием допущения | Backend |
| «Declare при подключении» может конфликтовать с существующей сущностью (e.g. не-durable очередь) | Medium | Medium | Декларировать durable:true; документировать, что очередь/exchange должны существовать или быть созданы автоматически | Backend |
| Отправка/приём с включённым `UseBase64Coding` без gzip — конфликт интерпретации | Low | Low | Поведение зафиксировано в FR-035: комбинация неактивна (UI сбрасывает флаг), `CompressingEncoding` возвращает UTF-8 без декодирования | Backend |
| Разрастание дублирующегося кода (4-я копия страницы/конфига) | High | Medium | Сознательное следование существующему паттерну; рефакторинг общих страниц — отдельная фича [OPEN QUESTION OQ-4] | Architecture |
| Blazor circuit падает из-за необработанного исключения в фоновой задаче consumer | Medium | High | Обернуть цикл приёма в try/catch, логировать, не пробрасывать | Backend |

---

## 15. Open Questions

| # | Question | Owner | Due Date | Status |
|---|---|---|---|---|
| OQ-1 | Поддерживать ли настройку prefetch (`BasicQosAsync`), таймаутов подключения и TTL сообщений в MVP? По умолчанию — нет (паритет с другими шинами, используются дефолты клиента) | Backend | На старте реализации | Open |
| OQ-2 | Добавлять ли тестовый проект (xUnit + Testcontainers RabbitMQ) отдельной фичей после MVP? Пользователь выбрал «без тестов» в scope. | Team | После MVP | Open |
| OQ-3 | Стоит ли маскировать `ConnectionString` (пароль) в UI, оставив хранение в appConfig.json как есть? Влияет на UX вкладки Configuration. | Backend | На старте реализации | Open |
| OQ-4 | Вынести ли три (затем четыре) страницы шин в общую генерируемую страницу, чтобы убрать дублирование? Сознательно отложено — рискует регрессией. | Architecture | После фичи | Open |

---

## 16. Glossary

| Term | Definition |
|---|---|
| **RabbitMQ** | Open-source message broker, реализует AMQP 0-9-1 |
| **AMQP 0-9-1** | Протокол обмена сообщениями, используемый RabbitMQ.Client |
| **Queue** | Упорядоченная FIFO-структура для сообщений |
| **Exchange** | Маршрутизатор; получает сообщения от producer и направляет в очереди по binding/routing key |
| **Binding** | Правило, связывающее exchange и очередь через routing key |
| **Routing key** | Ключ маршрутизации сообщения |
| **Exchange types** | Direct (точное совпадение ключа), Fanout (всем привязанным очередям), Topic (по паттерну ключа) |
| **Delivery tag** | Порядковый номер доставленного сообщения (для ack) |
| **BasicAck** | Подтверждение обработки сообщения (ручной ack) |
| **vhost** | Виртуальный хост в RabbitMQ — изолированное пространство имён |
| **Default exchange** | Встроенный exchange `""`, направляющий в очередь по имени |
| **MessageBusType** | Enum-дискриминатор шины в keyed-DI (EventHub/StorageQueue/ServiceBus/RabbitMq) |

---

## 17. References

- RabbitMQ .NET Client docs: https://www.rabbitmq.com/client-libraries/dotnet (7.x)
- RabbitMQ .NET API Guide: https://www.rabbitmq.com/client-libraries/dotnet-api-guide
- RabbitMQ URI Specification: https://www.rabbitmq.com/docs/uri-spec
- RabbitMQ Docker image: https://hub.docker.com/_/rabbitmq
- RabbitMQ.Client NuGet: https://www.nuget.org/packages/RabbitMQ.Client (7.2.x)
- Существующие реализации-аналоги в кодовой базе:
  - `src/Infrastructure/Providers/ServiceBusProducerProvider.cs`
  - `src/Infrastructure/Providers/ServiceBusConsumerProvider.cs`
  - `src/Infrastructure/Factories/ServiceBusProducerFactory.cs`
  - `src/Infrastructure/Factories/ServiceBusConsumerFactory.cs`
  - `src/WebUI/Components/Pages/ServiceBus.razor`
  - `src/WebUI/Components/Pages/Configuration.razor`
  - `src/WebUI/Components/Layout/NavMenu.razor`

---

<!-- SELF-REVIEW LOG
Reviewed on: 2026-08-18
Checks performed:
  - Logical consistency: PASS — цели/не-цели не противоречат; Queue↔Exchange маппинг согласован с ServiceBus Queue/Topic+Subscription; фазы в логическом порядке.
  - Completeness: PASS — каждая user story имеет acceptance criteria; FR трассируются к целям; все интеграции (брокер, DI, UI, файлы) отражены в архитектуре; ошибки покрыты для каждого happy-path.
  - Accuracy / hallucination check: PASS — версии пакетов проверены по NuGet (RabbitMQ.Client 7.2.2, net8.0 совместим с net10.0); API (BasicPublishAsync, IAsyncBasicConsumer, BasicAckAsync, QueueDeclareAsync, ExchangeDeclareAsync, QueueBindAsync) сверены с официальной документацией RabbitMQ.Client 7.x; числовые цели помечены [ASSUMPTION].
  - Diagram correctness: PASS — mermaid-диаграммы синтаксически корректны; указаны данные на стрелках.
  - Actionability: PASS — задачи разбиты по фазам с effort-оценками; требования атомарны и тестируемы.
Changes made during review:
  - ExchangeType добавлен в модель (Direct/Fanout/Topic) — требуется для корректного Exchange-сценария.
  - RoutingKey/QueueName сделаны nullable и обязательными только при EntityType==Exchange.
  - Уточнено mapping EventHubMessage: SequenceNumber=delivery tag, PartitionId=routing key.
  - Исправлен FR-035: комбинация UseBase64Coding без gzip — неактивна (UI сбрасывает флаг, CompressingEncoding возвращает UTF-8 без декодирования), устранено противоречие с фактическим поведением кода.
  - Исправлено название NFR-метрики производительности (target ≥ 1000 сообщений/мин).
-->
