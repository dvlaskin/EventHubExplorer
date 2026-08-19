# Шаг 6: Infrastructure — keyed-DI регистрация + сборка

_Файл: `RabbitMqFeaturePlan_Step_06.md`_

**Статус:** ⬜ TODO
**Что:** Зарегистрировать `RabbitMqProducerFactory`/`RabbitMqConsumerFactory` в `InfrastructureRegistration.cs` как keyed-singleton по ключу `MessageBusType.RabbitMq`. Собрать решение.
**Зачем:** FR-012 (страница `/rabbitmq/{id:guid}`) и все WebUI-потребители получат фабрики через `[Inject(Key = MessageBusType.RabbitMq)]`. Паритет с существующими тремя шинами.
**Файлы:**
- `src/Infrastructure/IoC/InfrastructureRegistration.cs` — ИЗМЕНИТЬ

**Зависит от:** Шаг 5
**Риски:**
- Ключ должен совпадать ровно с enum-значением `MessageBusType.RabbitMq` (прописано в Шаге 1) — иначе `[Inject(Key = ...)]` на странице не найдёт фабрику.
- Порядок регистраций в методе — по секциям; RabbitMQ добавить после Service Bus секции.

**Детали реализации:**

В `AddInfrastructureServices` после блока service bus добавить:
```csharp
        // rabbit mq
        services.AddKeyedSingleton<IMessageProducerFactory, RabbitMqProducerFactory>(MessageBusType.RabbitMq);
        services.AddKeyedSingleton<IMessageConsumerFactory, RabbitMqConsumerFactory>(MessageBusType.RabbitMq);
```

**Критерии завершения:**
- `dotnet build EventHubExplorer.sln` — без ошибок и без предупреждений, связанных с новым кодом.
- `dotnet run --project src/WebUI` — приложение стартует без исключений (keyed-DI регистрации валидны).
- Проверка: `IOptionsMonitor<AppConfiguration>` уже настроен в `Program.cs` (`AddJsonFile` + `Configure<AppConfiguration>`), менять его не нужно.