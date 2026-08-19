# Шаг 8: WebUI — вкладка RabbitMQ в `Configuration.razor`

_Файл: `RabbitMqFeaturePlan_Step_08.md`_

**Статус:** ⬜ TODO
**Что:** Добавить четвёртую вкладку «RabbitMQ» на страницу `Configuration.razor` с CRUD: `AddNewRabbitMqConfig`/`RemoveRabbitMqConfig`, accordion-форма с условными полями Exchange (`ExchangeType`, `RoutingKey`, `QueueName`), валидацией по образцу ServiceBus (подсветка незаполненных обязательных полей), сбросом `UseBase64Coding` при выключенном gzip (FR-035).
**Зачем:** FR-001..FR-006 (конфигурирование), FR-035 (неактивная комбинация base64 без gzip), сохранение в `Data/appConfig.json` через `AppConfigurationProvider`.
**Файлы:**
- `src/WebUI/Components/Pages/Configuration.razor` — ИЗМЕНИТЬ
- `src/WebUI/Components/Pages/Configuration.razor.css` — ИЗМЕНИТЬ (при необходимости)

**Зависит от:** Шаг 1 (Domain типы)
**Риски:**
- Файл 549 строк — редактировать аккуратно, следуя структуре существующих вкладок.
- `activeTab` по умолчанию `"eventhubs"` — не менять.
- Sticky-actions: сейчас `else` ветка обрабатывает servicebus; после добавления — явная ветка для `rabbitmq`.
- `LoadConfiguration()` дозаполняет `MessageFormatters` из зарегистрированных `IMessageFormatter` — добавить итерацию по `RabbitMqConfigs`.
- Биндинг `@bind:get`/`@bind:set` на ключи словаря `MessageFormatters` — по образцу ServiceBus.
- Валидация `isEmpty` — `string.IsNullOrWhiteSpace(ConnectionString) || string.IsNullOrWhiteSpace(EntityName)`; для Exchange дополнительно подсветить пустые `QueueName` (нужен для приёма) — по образцу подсветки незаполненных полей.
- `RemoveRabbitMqConfig` должен чистить историю сообщений (`MessagesHistoryService.RemoveAllAsync(configId)`) — паритет с ServiceBus.

**Детали реализации:**

1. **Вкладка-переключатель** после service bus button:
```razor
<button class="config-tab @(activeTab == "rabbitmq" ? "config-tab--active" : "")" @onclick='() => SetActiveTab("rabbitmq")'>
    <i class="bi bi-hdd-network-fill"></i>
    RabbitMQ
    <span class="config-tab-badge">@(appConfiguration?.RabbitMqConfigs.Count ?? 0)</span>
</button>
```

2. **Кнопка Add** в sticky-actions: добавить ветку `else if (activeTab == "rabbitmq")` → `AddNewRabbitMqConfig`.

3. **Блок контента** `@if (activeTab == "rabbitmq")`:
   - Empty state: «No RabbitMQ configured» + «Click "Add RabbitMQ" to create your first connection.» (по образцу ServiceBus).
   - Accordion `id="rabbitMqAccordion"`, для каждого конфига: id полей с префиксом `rmq-` (напр. `rmq-title-{i}`, `rmq-connection-string-{i}`, `rmq-entity-name-{i}`, `rmq-entity-type-{i}`, `rmq-exchange-type-{i}`, `rmq-routing-key-{i}`, `rmq-queue-name-{i}`, `rmq-use-gzip-{i}`, `rmq-use-base64-{i}`, `rmq-layout-preset-{i}`).
   - Поля: Title, Connection String, Entity Name (Queue or Exchange), Entity Type (select `RabbitMqEntityType`), Blocks layout (select `MessagePageLayoutPreset`), Gzip/Base64 чекбоксы (Base64 `disabled="@(!rbConfig.UseGzipCompression)"`, `@bind:after` → `OnRabbitMqGzipCompressionChanged(rbConfig)`), форматтеры (словарь), Remove.
   - **Условные поля Exchange** (FR-003/FR-004) — показать только при `rbConfig.EntityType == RabbitMqEntityType.Exchange`:
     ```razor
     @if (rbConfig.EntityType == RabbitMqEntityType.Exchange)
     {
         <div class="mb-3">
             <label class="form-label" for="@rmq-exchange-type-{i}">Exchange Type</label>
             <select id="rmq-exchange-type-@i" class="form-select" @bind="rbConfig.ExchangeType">
                 <option value="@RabbitMqExchangeType.Direct">Direct</option>
                 <option value="@RabbitMqExchangeType.Fanout">Fanout</option>
                 <option value="@RabbitMqExchangeType.Topic">Topic</option>
             </select>
         </div>
         <div class="mb-3">... Routing Key (input @bind="rbConfig.RoutingKey") ...</div>
         <div class="mb-3">... Queue Name (for consuming) (input @bind="rbConfig.QueueName") ...</div>
     }
     ```

4. **@code:**
   ```csharp
   private void AddNewRabbitMqConfig()
   {
       appConfiguration?.RabbitMqConfigs.Add(new RabbitMqConfig
       {
           Title = "New RabbitMQ",
           ConnectionString = "amqp://guest:guest@localhost:5672",
           EntityName = "",
           EntityType = RabbitMqEntityType.Queue,
           ExchangeType = RabbitMqExchangeType.Direct,
           MessagePageLayout = MessagePageLayoutPreset.TopSendReceiveBottomPayload,
           MessageFormatters = MessageFormatters?.ToDictionary(k => k.Name, v => false) ?? new Dictionary<string, bool>()
       });
   }

   private void RemoveRabbitMqConfig(int index)
   {
       var configId = appConfiguration?.RabbitMqConfigs[index].Id;
       if (configId is not null)
           MessagesHistoryService.RemoveAllAsync(configId.Value);
       appConfiguration?.RabbitMqConfigs.RemoveAt(index);
   }

   private void OnRabbitMqGzipCompressionChanged(RabbitMqConfig rbConfig)
   {
       if (!rbConfig.UseGzipCompression)
           rbConfig.UseBase64Coding = false;
   }
   ```
   (`OnRabbitMqGzipCompressionChanged` — клон существующего `OnGzipCompressionChanged`.)

5. **LoadConfiguration** — в цикле дозаполнения MessageFormatters добавить обход `appConfiguration.RabbitMqConfigs`.

6. **CSS** (при необходимости): если вкладка/бейдж отличаются — добавить классы по образцу существующих (`.config-tab--active`, `.config-tab-badge` уже общие; вероятно, правки не нужны).

**Критерии завершения:**
- На странице Configuration появилась вкладка «RabbitMQ» с badge-количеством.
- CRUD работает: добавить конфиг, заполнить поля (в т.ч. условные Exchange), сохранить → данные пишутся в `Data/appConfig.json`.
- При `EntityType == Queue` поля Exchange скрыты; при `Exchange` — видны.
- Выключение gzip сбрасывает base64 (FR-035).
- Удаление конфига чистит историю сообщений.