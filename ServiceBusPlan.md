# План: Поддержка Azure Service Bus
_Создан: 2026-03-14_
_Статус: В РАБОТЕ (Шаг 3 из 6 выполнен)_

## Цель
Добавить поддержку Azure Service Bus (Queue и Topic) в приложение, обеспечивая возможность отправки и получения сообщений в реальном времени через веб-интерфейс, аналогично текущей реализации для Event Hub и Storage Queue.

## Критерии успеха
- [ ] Конфигурация Service Bus может управляться на странице Configuration.
- [ ] Сущности Service Bus (Queues/Topics) отображаются в боковом меню (NavMenu).
- [ ] Пользователи могут отправлять сообщения в Service Bus Queue или Topic.
- [ ] Пользователи могут получать сообщения из Service Bus Queue или Subscription (для Topics).
- [ ] Поддержка форматеров сообщений и сжатия (Gzip/Base64), как для других шин.

## Обзор архитектуры
- **Domain**: Добавление моделей конфигурации `ServiceBusConfig`, перечисления `ServiceBusEntityType` и обновление `AppConfiguration`.
- **Infrastructure**: Добавление NuGet пакета `Azure.Messaging.ServiceBus`, реализация провайдеров (`ServiceBusProducerProvider`, `ServiceBusConsumerProvider`) и фабрик. Регистрация в DI как Keyed Services.
- **WebUI**: Создание новой страницы `ServiceBus.razor` и обновление существующих компонентов (`NavMenu`, `Configuration`).

## Затрагиваемые файлы
| Файл | Тип изменения | Описание |
|------|---------------|----------|
| `src/Domain/Enums/MessageBusType.cs` | ИЗМЕНИТЬ | Добавить `ServiceBus` |
| `src/Domain/Enums/ServiceBusEntityType.cs` | СОЗДАТЬ | Тип сущности: Queue или Topic |
| `src/Domain/Configs/ServiceBusConfig.cs` | СОЗДАТЬ | Модель конфигурации для Service Bus |
| `src/Domain/Configs/AppConfiguration.cs` | ИЗМЕНИТЬ | Добавить список `ServiceBusConfigs` |
| `src/Infrastructure/Infrastructure.csproj` | ИЗМЕНИТЬ | Добавить `Azure.Messaging.ServiceBus` |
| `src/Infrastructure/Providers/ServiceBusProducerProvider.cs` | СОЗДАТЬ | Реализация отправки сообщений |
| `src/Infrastructure/Providers/ServiceBusConsumerProvider.cs` | СОЗДАТЬ | Реализация получения сообщений |
| `src/Infrastructure/Factories/ServiceBusProducerFactory.cs` | СОЗДАТЬ | Фабрика продюсеров |
| `src/Infrastructure/Factories/ServiceBusConsumerFactory.cs` | СОЗДАТЬ | Фабрика консьюмеров |
| `src/Infrastructure/IoC/InfrastructureRegistration.cs` | ИЗМЕНИТЬ | Регистрация новых сервисов |
| `src/WebUI/Components/Layout/NavMenu.razor` | ИЗМЕНИТЬ | Отображение Service Bus сущностей |
| `src/WebUI/Components/Pages/Configuration.razor` | ИЗМЕНИТЬ | Управление конфигами Service Bus |
| `src/WebUI/Components/Pages/ServiceBus.razor` | СОЗДАТЬ | Страница взаимодействия с Service Bus |

## Шаги

### Шаг 1: Обновление Domain моделей
**Статус:** ✅ DONE
**Что:** Создать `ServiceBusConfig.cs`, `ServiceBusEntityType.cs`, обновить `MessageBusType.cs` и `AppConfiguration.cs`.
**Зачем:** Обеспечить типизацию и хранение настроек для нового типа шины.
**Файлы:** `src/Domain/Enums/MessageBusType.cs`, `src/Domain/Enums/ServiceBusEntityType.cs`, `src/Domain/Configs/ServiceBusConfig.cs`, `src/Domain/Configs/AppConfiguration.cs`
**Зависит от:** нет
**Детали реализации:** `ServiceBusConfig` должен содержать `ConnectionString`, `EntityName`, `EntityType`, `SubscriptionName` (для Topics), а также настройки форматеров и сжатия.

**Заметки по реализации:**
- **Что сделано:** Добавлен новый тип шины `ServiceBus`, создана модель `ServiceBusConfig` с поддержкой очередей и топиков (с подписками), обновлен основной конфиг `AppConfiguration`.
- **Отклонения от плана:** Нет.
- **Ключевые места:** `ServiceBusConfig.cs`, `AppConfiguration.cs`.
- **Важно знать:** `SubscriptionName` может быть null для очередей, но требуется для топиков при потреблении.

---

### Шаг 2: Настройка инфраструктуры и NuGet
**Статус:** ✅ DONE
**Что:** Добавить пакет `Azure.Messaging.ServiceBus` в проект Infrastructure.
**Зачем:** Использование официального SDK для работы с Service Bus.
**Файлы:** `src/Infrastructure/Infrastructure.csproj`
**Зависит от:** нет

**Заметки по реализации:**
- **Что сделано:** Добавлен NuGet пакет `Azure.Messaging.ServiceBus` версии `7.20.1` в проект `Infrastructure`.
- **Отклонения от плана:** Нет.
- **Ключевые места:** `src/Infrastructure/Infrastructure.csproj`.
- **Важно знать:** Версия `7.20.1` является актуальной стабильной версией на момент реализации.

---

### Шаг 3: Реализация ServiceBus Провайдеров
**Статус:** ✅ DONE
**Что:** Создать `ServiceBusProducerProvider` и `ServiceBusConsumerProvider`.
**Зачем:** Инкапсуляция логики взаимодействия с Azure Service Bus SDK.
**Файлы:** `src/Infrastructure/Providers/ServiceBusProducerProvider.cs`, `src/Infrastructure/Providers/ServiceBusConsumerProvider.cs`
**Зависит от:** Шаг 1, Шаг 2
**Детали реализации:** Провайдеры должны реализовывать `IMessageProducerProvider` и `IMessageConsumerProvider` соответственно. Для ConsumerProvider нужно реализовать цикл получения сообщений (destructive read, как в Storage Queue).

**Заметки по реализации:**
- **Что сделано:** Реализованы `ServiceBusProducerProvider` и `ServiceBusConsumerProvider`.
- **Отклонения от плана:** Нет.
- **Ключевые места:** `src/Infrastructure/Providers/ServiceBusProducerProvider.cs`, `src/Infrastructure/Providers/ServiceBusConsumerProvider.cs`. Добавлена поддержка `ServiceBusConfig` в `CompressingEncoding.cs`.
- **Важно знать:** Использован `Lazy` для инициализации клиентов Service Bus. Реализован "destructive read" (получение с последующим `CompleteMessageAsync`).

---

### Шаг 4: Реализация Фабрик и регистрация DI
**Статус:** ⬜ TODO
**Что:** Создать `ServiceBusProducerFactory`, `ServiceBusConsumerFactory` и обновить `InfrastructureRegistration.cs`.
**Зачем:** Обеспечить создание провайдеров через фабрики и их доступность через Keyed DI.
**Файлы:** `src/Infrastructure/Factories/ServiceBusProducerFactory.cs`, `src/Infrastructure/Factories/ServiceBusConsumerFactory.cs`, `src/Infrastructure/IoC/InfrastructureRegistration.cs`
**Зависит от:** Шаг 3

---

### Шаг 5: Реализация UI страницы Service Bus
**Статус:** ⬜ TODO
**Что:** Создать `src/WebUI/Components/Pages/ServiceBus.razor`.
**Зачем:** Предоставить пользователю интерфейс для отправки и получения сообщений.
**Файлы:** `src/WebUI/Components/Pages/ServiceBus.razor`
**Зависит от:** Шаг 4
**Детали реализации:** Можно взять за основу `EventHub.razor`, адаптировав его под специфику Service Bus (например, отображение SubscriptionName для топиков).

---

### Шаг 6: Интеграция в NavMenu и Configuration
**Статус:** ⬜ TODO
**Что:** Обновить `NavMenu.razor` и `Configuration.razor`.
**Зачем:** Позволить пользователю настраивать Service Bus и переходить на страницу управления.
**Файлы:** `src/WebUI/Components/Layout/NavMenu.razor`, `src/WebUI/Components/Pages/Configuration.razor`
**Зависит от:** Шаг 5

## Открытые вопросы
- Использовать ли `EventHubMessage` как общую модель сообщения или переименовать её? (Пока используем как есть для минимизации изменений).
- Нужна ли поддержка сессий в Service Bus? (В текущей задаче не указано, пропускаем).

## Вне scope
- Поддержка Dead Letter Queue.
- Управление подписками (создание/удаление) через UI.
