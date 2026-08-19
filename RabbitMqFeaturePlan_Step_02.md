# Шаг 2: Infrastructure — пакет `RabbitMQ.Client` 7.2.2

_Файл: `RabbitMqFeaturePlan_Step_02.md`_

**Статус:** ⬜ TODO
**Что:** Добавить NuGet-пакет `RabbitMQ.Client` версии 7.2.2 в `src/Infrastructure/Infrastructure.csproj`.
**Зачем:** Клиент AMQP 0-9-1 для RabbitMQ. Версия 7.x актуальна (последняя 7.2.2, подтверждена по NuGet/GitHub) и совместима с `net10.0`. Спецификация раздел 2.4 п.2 и 17.
**Файлы:**
- `src/Infrastructure/Infrastructure.csproj` — ИЗМЕНИТЬ

**Зависит от:** нет
**Риски:**
- Использовать только стабильные async-API 7.x (`BasicPublishAsync`, `IAsyncBasicConsumer`, `BasicAckAsync`, `CreateConnectionAsync`, `CreateChannelAsync`, `QueueDeclareAsync`, `ExchangeDeclareAsync`, `QueueBindAsync`). Избегать deprecated overload'ов без `mandatory`/`cancellationToken`.
- Не добавлять пакет в другие csproj — WebUI получает его транзитивно через ProjectReference на Infrastructure.
- Целевая версия фиксируется явно (7.2.2), не флоатинг `7.*` — повторяет стиль остальных пакетов в файле.

**Детали реализации:**

В `<ItemGroup>` с остальными PackageReference добавить:
```xml
<PackageReference Include="RabbitMQ.Client" Version="7.2.2" />
```

После правки проверить, что package restore проходит и решение собирается.

**Критерии завершения:**
- `dotnet restore` и `dotnet build EventHubExplorer.sln` без ошибок.
- `dotnet list src/Infrastructure/Infrastructure.csproj package` показывает `RabbitMQ.Client 7.2.2`.