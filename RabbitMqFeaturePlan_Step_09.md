# Шаг 9: WebUI — `NavMenu` + `Home.razor` + `appConfig.json`

_Файл: `RabbitMqFeaturePlan_Step_09.md`_

**Статус:** ✅ DONE
**Что:** Добавить секцию «RabbitMQ» в навигацию (`NavMenu.razor` + `.razor.cs`), карточку и бейдж на главную страницу (`Home.razor` + `.razor.css`), пример конфига в `Data/appConfig.json`.
**Зачем:** FR-010 (секция в NavMenu), FR-011 (карточка на Home), FR-006 (сохранение в appConfig.json). Навигация — единственная точка входа на страницы шин.
**Файлы:**
- `src/WebUI/Components/Layout/NavMenu.razor` — ИЗМЕНИТЬ
- `src/WebUI/Components/Layout/NavMenu.razor.cs` — ИЗМЕНИТЬ
- `src/WebUI/Components/Pages/Home.razor` — ИЗМЕНИТЬ
- `src/WebUI/Components/Pages/Home.razor.css` — ИЗМЕНИТЬ
- `src/WebUI/Data/appConfig.json` — ИЗМЕНИТЬ

**Зависит от:** Шаг 7 (страница), Шаг 8 (вкладка Configuration)
**Риски:**
- NavMenu подписан на `IOptionsMonitor<AppConfiguration>.OnChange` — при сохранении конфига меню обновляется автоматически; список `RabbitMqConfigs` должен обновляться в обработчике.
- `IsRouteActive("rabbitmq/")` — префикс уникален, не пересекается с другими маршрутами.
- Иконки в NavMenu — font-глифы bootstrap-icons (`bi-hdd-network-fill`), собственные CSS-классы не нужны (проверено: `.bi-event-hubs-nav-menu` — исключение, для остальных шин используются стандартные глифы).
- В `appConfig.json` добавить только пример; не ломать существующие конфиги. JSON-поля должны соответствовать `RabbitMqConfig` (десериализация через `AddJsonFile`).

**Детали реализации:**

**1. `NavMenu.razor.cs`:**
- Поле `private List<RabbitMqConfig>? RabbitMqConfigs { get; set; }` + `using Domain.Configs;` (уже есть).
- `private bool isRabbitMqExpanded;`
- `private bool IsRabbitMqRouteActive => IsRouteActive("rabbitmq/");`
- В `OnInitialized`: `RabbitMqConfigs = Config?.CurrentValue.RabbitMqConfigs ?? [];`
- В `OnChange` callback: `RabbitMqConfigs = x.RabbitMqConfigs;`
- `ToggleRabbitMq()`, в `EnsureSectionExpandedForCurrentRoute()`: `if (IsRabbitMqRouteActive) isRabbitMqExpanded = true;`

**2. `NavMenu.razor`** — секция после Service Bus (копия разметки service bus с подстановками):
```razor
<div class="nav-item px-3 mt-3">
    <button type="button"
            class="nav-link nav-section-toggle @(IsRabbitMqRouteActive ? "nav-section-toggle--active" : string.Empty)"
            @onclick="ToggleRabbitMq"
            aria-expanded="@isRabbitMqExpanded"
            aria-controls="rabbitmq-nav-group">
        <span class="nav-section-header">RabbitMQ</span>
        <i class="bi @(isRabbitMqExpanded ? "bi-chevron-up" : "bi-chevron-down") nav-section-chevron" aria-hidden="true"></i>
    </button>
</div>

<div id="rabbitmq-nav-group" hidden="@(!isRabbitMqExpanded)">
    @foreach (var rbConfig in RabbitMqConfigs ?? Enumerable.Empty<RabbitMqConfig>())
    {
        <div class="nav-item px-3">
            <NavLink class="nav-link nav-link--compact" href="@($"rabbitmq/{rbConfig.Id}")">
                <span class="icon">
                    <i class="bi bi-hdd-network-fill" aria-hidden="true"></i>
                </span>
                <span class="text" title="@(rbConfig.EntityType == RabbitMqEntityType.Exchange ? $"{rbConfig.EntityName} ({rbConfig.ExchangeType})" : rbConfig.EntityName)">
                    @rbConfig.Title
                </span>
            </NavLink>
        </div>
    }
</div>
```

**3. `Home.razor`:**
- В `hero-badges` добавить: `<li class="hero-badge"><i class="bi bi-hdd-network-fill" aria-hidden="true"></i> RabbitMQ</li>`
- В `services-grid` добавить карточку:
  ```razor
  <article class="service-card service-card--rabbitmq">
      <i class="bi bi-hdd-network-fill service-icon"></i>
      <h5>RabbitMQ</h5>
      <p>Popular open-source message broker (AMQP 0-9-1). Queues and exchanges with routing for pub/sub patterns.</p>
  </article>
  ```

**4. `Home.razor.css`** — добавить (паритет с существующими):
```css
.service-card--rabbitmq:hover { border-color: var(--color-danger); }
.service-card--rabbitmq .service-icon { color: var(--color-danger); }
```
> Цвет — на усмотрение; если `--color-danger` неуместен, использовать другой токен из `wwwroot/app.css`.

**5. `Data/appConfig.json`** — добавить секцию `RabbitMqConfigs` (пример Queue и Exchange):
```json
"RabbitMqConfigs": [
  {
    "Id": "f3c1a2b4-0000-4000-8000-000000000001",
    "Title": "Local RabbitMQ Queue",
    "ConnectionString": "amqp://guest:guest@localhost:5672",
    "EntityName": "orders",
    "EntityType": 0,
    "ExchangeType": 0,
    "RoutingKey": null,
    "QueueName": null,
    "UseGzipCompression": true,
    "UseBase64Coding": true,
    "MessageFormatters": { "Json formatter": true, "Guid replacer": true, "Date replacer": false, "Datetime replacer": false },
    "MessagePageLayout": 0
  },
  {
    "Id": "f3c1a2b4-0000-4000-8000-000000000002",
    "Title": "Local RabbitMQ Exchange (Topic)",
    "ConnectionString": "amqp://guest:guest@localhost:5672",
    "EntityName": "events",
    "EntityType": 1,
    "ExchangeType": 2,
    "RoutingKey": "orders.created",
    "QueueName": "order-created-queue",
    "UseGzipCompression": false,
    "UseBase64Coding": false,
    "MessageFormatters": { "Json formatter": true, "Guid replacer": true, "Date replacer": false, "Datetime replacer": false },
    "MessagePageLayout": 1
  }
]
```
`EntityType`: 0=Queue, 1=Exchange. `ExchangeType`: 0=Direct, 1=Fanout, 2=Topic.

**Критерии завершения:**
- В NavMenu появляется секция «RabbitMQ» со ссылками на конфиги; раскрывается при переходе на `/rabbitmq/...`.
- На Home — бейдж и карточка RabbitMQ.
- После `dotnet run --project src/WebUI` ссылки в NavMenu ведут на работающие страницы `/rabbitmq/{id}`.

**Заметки по реализации:**
- **Что сделано:** В `NavMenu.razor.cs` добавлены `RabbitMqConfigs`, `isRabbitMqExpanded`, `IsRabbitMqRouteActive` (`IsRouteActive("rabbitmq/")`), инициализация в `OnInitialized` и обновление в `OnChange`-колбэке, `ToggleRabbitMq()`, ветка раскрытия в `EnsureSectionExpandedForCurrentRoute()`. В `NavMenu.razor` добавлена секция «RabbitMQ» после Service Bus: toggle-кнопка с `aria-controls="rabbitmq-nav-group"`, `NavLink` на `/rabbitmq/{Id}` с глифом `bi-hdd-network-fill` и title `EntityName (ExchangeType)` для Exchange (по образцу ServiceBus). В `Home.razor` добавлен бейдж `bi-hdd-network-fill` в `hero-badges` и карточка `service-card--rabbitmq` в `services-grid`. В `Home.razor.css` добавлены `.service-card--rabbitmq:hover` и `.service-card--rabbitmq .service-icon` на токене `--color-danger` (различает RabbitMQ от eventhub/queue/servicebus, занявших secondary/success/primary). В `Data/appConfig.json` добавлена секция `RabbitMqConfigs` с двумя примерами: Queue (`EntityType: 0`) и Exchange Topic (`EntityType: 1, ExchangeType: 2, RoutingKey: orders.created, QueueName: order-created-queue`), поля соответствуют `RabbitMqConfig`. `dotnet build EventHubExplorer.sln` — 0 warning / 0 error; JSON провалидирован.
- **Отклонения от плана:** Нет. Цвет `--color-danger` использован как предложено в плане.
- **Ключевые места:** секция RabbitMQ ~строка 106 и `RabbitMqConfigs`/`ToggleRabbitMq`/`IsRabbitMqRouteActive` в `src/WebUI/Components/Layout/NavMenu.razor` / `NavMenu.razor.cs`; бейдж ~строка 18 и карточка ~строка 42 в `src/WebUI/Components/Pages/Home.razor`; `RabbitMqConfigs` в `src/WebUI/Data/appConfig.json`.
- **Важно знать:** Префикс маршрута `"rabbitmq/"` уникален (не пересекается с `eventhub/`, `storagequeue/`, `servicebus/`, `configuration`). Секция меню обновляется автоматически через `IOptionsMonitor.OnChange` при сохранении конфига на странице Configuration (Шаг 8). Страница `/rabbitmq/{id:guid}` создана на Шаге 7.