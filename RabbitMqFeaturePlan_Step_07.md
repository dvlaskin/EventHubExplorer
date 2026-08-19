# Шаг 7: WebUI — страница `RabbitMq.razor` + `.razor.css`

_Файл: `RabbitMqFeaturePlan_Step_07.md`_

**Статус:** ⬜ TODO
**Что:** Создать страницу `/rabbitmq/{id:guid}` как точный клон `ServiceBus.razor` с заменой: ключ keyed-DI `MessageBusType.RabbitMq`, поиск конфига в `RabbitMqConfigs`, описание сущности для Queue/Exchange, иконка.
**Зачем:** FR-012 (маршрут), G1/G2 (отправка/получение через UI), история (FR-024). Страница — интерфейс пользователя для работы с RabbitMQ.
**Файлы:**
- `src/WebUI/Components/Pages/RabbitMq.razor` — СОЗДАТЬ
- `src/WebUI/Components/Pages/RabbitMq.razor.css` — СОЗДАТЬ

**Зависит от:** Шаг 6 (keyed-DI), Шаг 1 (Domain типы)
**Риски:**
- НЕ менять логику страницы — только подстановки. Полная копия `ServiceBus.razor` (549 строк страницы + @code) с минимальными правками.
- Маршрут `/rabbitmq/{id:guid}` не конфликтует с `/eventhub/`, `/storagequeue/`, `/servicebus/` (проверено — префиксы уникальны).
- `EntityType` — enum `RabbitMqEntityType` (Queue/Exchange), не путать с `ServiceBusEntityType`.
- Для Exchange в описании показывать `ExchangeType`, `RoutingKey`, `QueueName`.
- Иконка страницы: `bi-hdd-network-fill` (или иная bootstrap-icon; в NavMenu иконки шин — обычные font-глифы bootstrap-icons, собственные CSS-классы не нужны).

**Детали реализации:**

1. **Копировать** `src/WebUI/Components/Pages/ServiceBus.razor` → `RabbitMq.razor`.

2. **Правки в разметке:**
   - `@page "/rabbitmq/{id:guid}"`
   - `<PageTitle>RabbitMQ - @title</PageTitle>`
   - Заголовок: `<h3 class="page-title"><i class="bi bi-hdd-network-fill"></i> @title</h3>`
   - Описание сущности:
     ```razor
     var entityDescription = $"{config.EntityType}: {config.EntityName}";
     if (config.EntityType == RabbitMqEntityType.Exchange)
     {
         entityDescription += $" ({config.ExchangeType}, routing key: {config.RoutingKey}, queue: {config.QueueName})";
     }
     <div class="rabbitmq-entity text-muted" title="@entityDescription">@entityDescription</div>
     ```
   - `MessageLayoutPresetSelector` id: `rabbitmq-layout-preset` (уникальный InputId).
   - `SendLabel="Send Message"` — без изменений.

3. **Правки в `@code`:**
   - `[Inject(Key = MessageBusType.RabbitMq)] private IMessageProducerFactory? MessageProducerFactory` и аналогично `IMessageConsumerFactory`.
   - `config = Config?.CurrentValue.RabbitMqConfigs.FirstOrDefault(w => w.Id == Id);`
   - `title = config?.Title ?? "unknown rabbitmq";`
   - `SaveLayoutPreferenceAsync`: `appConfiguration.RabbitMqConfigs.FirstOrDefault(...)` + `config.MessagePageLayout = currentLayoutPreset`.
   - `using Domain.Enums` — уже есть (нужен `RabbitMqEntityType`/`RabbitMqExchangeType`).
   - Остальной @code (SendMessage, история, StartReceiveMessages, StopReceiveMessages, DisplayErrorMessage, DisposeAsync, `CircularBuffer<EventHubMessage> receivedMessages = new(50)`, `ResettableCts`) — **без изменений**.

4. **Доп. обработка FR-041 (обрыв соединения при приёме):** в `StartReceiveMessages` после `await foreach` (когда поток завершился не по кнопке Stop) показать toast:
   ```csharp
   await foreach (var message in messageConsumer.StartReceiveMessageAsync(receiverCts.Token))
   {
       if (!isProcessing) return;
       ...
   }

   if (!receiverCts.IsCancellationRequested)
   {
       _ = toast?.ShowToast("Connection lost. Receiving stopped.", ToastType.Error, TimeSpan.FromSeconds(10));
   }
   ```
   Это единственное смысловое отличие от ServiceBus-страницы — реализует FR-041 без изменений в Application.

5. **`RabbitMq.razor.css`** — копия `ServiceBus.razor.css` с заменой класса `.servicebus-entity` → `.rabbitmq-entity` (и заголовок `.page-title` — идентичен).

**Критерии завершения:**
- `dotnet build EventHubExplorer.sln` без ошибок.
- Переход на `/rabbitmq/{id}` (после добавления конфига в appConfig.json, Шаг 9) открывает страницу; отправка/получение работают.
- Группа сообщений в UI: `sn {SequenceNumber}, routing key {PartitionId}` отображается корректно.