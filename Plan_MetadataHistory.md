# План: История метаданных сообщений (Properties) с привязкой к записи истории
_Создан: 2026-09-06_
_Статус: Готов к реализации_
_SELF-REVIEW: 2026-09-06 — проверены консистентность (обзор/таблица/шаги), полнота покрытия FR-001…FR-034 и G1…G4, точность имён/путей по данным 3 суб-агентов, направление зависимостей, атомарность шагов; исправлены: противоречие атрибут-vs-опции, дефолт Version 0-vs-2 (критично для FR-032), DIP для .bak-метода, graceful-путь битого JSON на всех мутациях, границы Шаг 2/3. Критических ошибок не осталось._
_Спецификация: `Spec_MetadataHistory.md` v0.1_

## Цель

Персистить последний отправленный снапшот `Properties` (`Dictionary<string,string>`) вместе с телом сообщения в `Data/messagesHistory.json` как единую запись `MessageHistoryRecord` (1:1), однократно мигрировать legacy-формат (`List<string>`) без потери тел и порядка, и перевести 4 bus-страницы на identity-по-`Id` с записью в файл только в момент `Send` (черновик — 0 writes, повторный `Send` без диффа — 0 writes).

## Критерии успеха

- [ ] После рестарта выбор historic-записи подставляет тело и properties, совпадающие с моментом последнего `Send` (G1).
- [ ] Правки черновика до `Send` дают 0 writes (mtime/size файла неизменны); `Send` без диффа properties даёт 0 writes для properties-блока (G2, проверка моком `IFileStorageProvider` / mtime).
- [ ] Старый JSON открывается, все непустые тела на месте в том же порядке с пустыми properties; второй запуск файл не переписывает (стабильные `Id`, `Version: 2`) (G3).
- [ ] Сохранены гарантии: дедуп по точному совпадению тела, лимит 10 записей на конфиг, eviction oldest-first (G4, ручные тесты на 4 страницах).
- [ ] `dotnet build EventHubExplorer.sln` без ошибок и предупреждений как новых; ни один `catch {}`; публичные API с XML-доками.

## Обзор архитектуры

Меняется только вертикаль истории, транспортные сервисы (`IMessageProducerService`, `BaseMessageProducer`, `OutgoingMessage`) не трогаются — конвертация `string→object` остаётся в страницах в момент `Send`, как сегодня.

1. **Domain:** `MessagesHistory.Messages` меняет тип значения с `List<string>` на `List<MessageHistoryRecord>` + добавляется `int Version` (дефолт `0` = legacy, запись всегда с `2`). Новая сущность `MessageHistoryRecord { Guid Id; string Body; Dictionary<string,string> Properties; DateTimeOffset CreatedAt }`. Старый generic-интерфейс `IMessageHistory<Guid,List<string>>` удаляется в том же PR (OQ-1 закрыт), вводится новый не-generic контракт с identity-по-`Id` (см. Шаг 3). Лимиты `MessagePropertiesLimits` без изменений.
2. **Infrastructure:** новый `JsonConverter<List<MessageHistoryRecord>>` (`MessageHistoryRecordListConverter`) читает оба формата элемента (JSON-строка = legacy → `{ NewGuid(), Body, {}, now }`, JSON-объект = новый), пишет только объекты. Подключается через `JsonSerializerOptions` только в пайплайне `MessageHistoryProvider` (override опций чтения/записи; при необходимости — минимальный `protected virtual` хук в `BaseFileStorageProvider`, `AppConfigurationProvider` не затронут). Атрибут `[JsonConverter]` на Domain-модели НЕ используется (нарушил бы направление зависимостей Domain→Infrastructure). `Deserialize` в `BaseFileStorageProvider.GetDataAsync` сегодня вызывается без опций — поэтому проброс опций через override/хук обязателен (детали в Шаге 2).
3. **Application (`FileBasedMessageHistory` — полный рерайт под новый контракт):** CRUD по `messageId`, копии наружу (не внутренние ссылки), валидация properties по `MessagePropertiesLimits` на границе, точное поэлементное order-independent сравнение для no-diff, дедуп по телу + eviction до 10 внутри сервиса (переезд логики из 4 страниц — см. риски), однократный миграционный persist (`Version < 2` → `Version = 2`) с `.bak`-циклом по FR-034, graceful-путь битого JSON (пустая история + error-лог, файл не затирается до первой успешной мутации). Каждая мутация — один read-modify-write; сериализация под существующим `SemaphoreSlim` провайдера (окно read→write как сегодня, last-write-wins — вне скоупа менять).
4. **WebUI:** `MessageSendPanel` переходит с `List<string>` на `IReadOnlyList<MessageHistoryRecord>`, коллбеки `OnSelectMessage(Guid)` / `OnRemoveFromHistory(Guid)`, превью `Body[..45]...` как сегодня. 4 страницы (`EventHub`, `ServiceBus`, `RabbitMq`, `StorageQueue`) держат `selectedMessageId: Guid?` + in-memory копию properties; правки редактора до `Send` — только память; на `Send` — резолв цели по FR-022 (выбранная → совпавшая по телу → новая), сравнение в памяти перед вызовом сервиса (0 writes при равенстве). `Configuration.razor` (`RemoveAllAsync(Guid)`) сигнатурно не меняется — только перекомпиляция. Дублирование 4 страниц не рефакторится (Non-Goal).
5. **Верификация:** тестового проекта в репо нет (`tests/` пуста, `sln` без тестов) — поэтому автоматические тесты в этом плане не планируются как обязательные; приёмка — ручные сценарии из спеки (§3, §11) + счётчик writes через временный мок/наблюдение mtime. Заведение `tests/*.csproj` (xUnit+Moq) — опциональный follow-up, вне данного плана.

## Затрагиваемые файлы

| Файл | Тип изменения | Описание |
|------|---------------|----------|
| `src/Domain/Models/MessageHistoryRecord.cs` | СОЗДАТЬ | Новая запись: `Id/Body/Properties/CreatedAt` |
| `src/Domain/Models/MessagesHistory.cs` | ИЗМЕНИТЬ | `Dictionary<Guid, List<MessageHistoryRecord>> Messages` + `int Version = 0` (дефолт 0 = legacy; сервис выставляет 2 при save — см. Шаг 1, иначе миграция не триггерится) |
| `src/Domain/Interfaces/Services/IMessageHistory.cs` | УДАЛИТЬ | Старый generic `IMessageHistory<TInput,TResult>` |
| `src/Domain/Interfaces/Services/IMessageHistoryService.cs` | СОЗДАТЬ | Новый контракт: `GetHistoryAsync / AddMessageAsync / UpdatePropertiesAsync / RemoveMessageAsync / RemoveAllAsync` |
| `src/Infrastructure/Providers/FileStorageProviders/MessageHistoryRecordListConverter.cs` | СОЗДАТЬ | `JsonConverter<List<MessageHistoryRecord>>`: чтение string+object, запись только object |
| `src/Infrastructure/Providers/FileStorageProviders/MessageHistoryProvider.cs` | ИЗМЕНИТЬ | Override опций (подключение конвертера) + отдельный метод миграционного save с `.bak`-циклом (FR-034) |
| `src/Domain/Interfaces/Providers/IMessageHistoryStorageProvider.cs` | СОЗДАТЬ | Узкое расширение `IFileStorageProvider<MessagesHistory>` под `.bak`-метод (DIP: сервис не зависит от конкретного провайдера) |
| `src/Infrastructure/IoC/InfrastructureRegistration.cs` | ИЗМЕНИТЬ | Регистрация `IMessageHistoryStorageProvider → MessageHistoryProvider` (Singleton сохранить) |
| `src/Application/Services/FileBasedMessageHistory.cs` | ИЗМЕНИТЬ | Полный рерайт: CRUD по `Id`, валидация, no-diff, миграционный persist + `.bak`, обработка битого JSON |
| `src/Application/IoC/ApplicationRegistration.cs` | ИЗМЕНИТЬ | Перерегистрация `IMessageHistoryService → FileBasedMessageHistory` (Singleton сохранить) |
| `src/WebUI/Components/Shared/MessageSendPanel.razor` | ИЗМЕНИТЬ | `List<string>` → записи, коллбеки по `Guid`, превью тела без смены вёрстки/`aria` |
| `src/WebUI/Components/Pages/EventHub.razor` | ИЗМЕНИТЬ | `selectedMessageId`, in-memory props, резолв FR-022, сравнение на `Send` |
| `src/WebUI/Components/Pages/ServiceBus.razor` | ИЗМЕНИТЬ | То же, что EventHub (копипаст-паттерн сохранить) |
| `src/WebUI/Components/Pages/RabbitMq.razor` | ИЗМЕНИТЬ | То же, что EventHub |
| `src/WebUI/Components/Pages/StorageQueue.razor` | ИЗМЕНИТЬ | То же + сохранить хинт «properties ignored» (`:44`) |
| `src/WebUI/Components/Pages/Configuration.razor` | ПРОВЕРИТЬ | `RemoveAllAsync(Guid)` не меняется — только убедиться в компиляции (fire-and-forget без `await` как сегодня) |
| `src/Domain/Models/OutgoingMessage.cs` | НЕ ТРОГАТЬ | Только референс правил валидации |
| `src/Infrastructure/Providers/FileStorageProviders/BaseFileStorageProvider.cs` | ИЗМЕНИТЬ ТОЛЬКО ПРИ НЕОБХОДИМОСТИ | Минимальный `protected virtual` хук опций чтения/записи для Шага 2; иначе не трогать (семафор/пути без изменений) |
| `src/WebUI/Components/Shared/MessagePropertiesEditor.razor` | НЕ ТРОГАТЬ | Редактор уже отдаёт `IDictionary<string,string>` с валидацией |

## Шаги

### Шаг 1: Ввести доменную модель записей истории
**Статус:** ⬜ TODO
**Что:** Создать `MessageHistoryRecord`, расширить `MessagesHistory` полем `Version` и новым типом словаря. Конвертер и `.bak`-метод появятся в Шагах 2–3 — в этом шаге только модель, без ссылок на Infrastructure.
**Зачем:** Без стабильного `Id` per-запись невозможны identity-по-`Id`, привязка properties 1:1 и стабильная миграция.
**Файлы:** `src/Domain/Models/MessageHistoryRecord.cs`, `src/Domain/Models/MessagesHistory.cs`
**Зависит от:** нет
**Риски:** Искушение переиспользовать `OutgoingMessage.Properties` (`string→object`) — нельзя, в истории только `string→string` (FR-004). `Properties` никогда не `null` (пустой словарь), иначе `System.Text.Json` и сравнения усложняются. `CreatedAt` — `DateTimeOffset`, не `DateTime` (часовой пояс при миграции).
**Детали реализации:**
- `MessageHistoryRecord`: `sealed class`, свойства `Guid Id { get; set; }`, `string Body { get; set; } = string.Empty`, `Dictionary<string,string> Properties { get; set; } = new()`, `DateTimeOffset CreatedAt { get; set; }`. XML-доки на класс и свойства. Guard: конструктор по умолчанию для десериализатора; инварианты (non-whitespace Body, лимиты) проверяет сервис (Шаг 3), не модель.
- `MessagesHistory`: добавить `int Version { get; set; } = 0;`, сменить `Messages` на `Dictionary<Guid, List<MessageHistoryRecord>>`. КРИТИЧНО: дефолт именно `0`, а не `2` — `System.Text.Json` оставляет инициализатор нетронутым при отсутствии поля в JSON, поэтому legacy-файл без `Version` с дефолтом `2` никогда не определился бы как legacy и миграция (FR-032) не триггерилась бы. Новый формат всегда пишется с `Version = 2` (выставляет сервис/провайдер при save). Никаких `[JsonConverter]`-атрибутов в Domain (направление зависимостей!). Подключение конвертера — только через опции в `MessageHistoryProvider` (Шаг 2).
- Ограничения процесса: типы — `sealed`, nullable enable соблюдён, без логики сериализации в Domain.
- Верификация: `dotnet build src/Domain/Domain.csproj` — зелёный; `new MessagesHistory()` имеет `Version == 0`, после выставления `Version = 2` сериализуется дефолтным `System.Text.Json` в `{"Version":2,"Messages":{}}`.

---

### Шаг 2: Добавить двуформатный JSON-конвертер и чтение legacy
**Статус:** ⬜ TODO
**Что:** Создать `MessageHistoryRecordListConverter : JsonConverter<List<MessageHistoryRecord>>`, подключить его только к пайплайну истории, проверить чтение старого файла (`src/WebUI/Data/messagesHistory.json`, формат `{Messages:{guid:[string]}}` без `Version`, тела с `\u0022`-эскейпами) без записи.
**Зачем:** FR-030/031: файл должен открываться в обоих форматах; запись — всегда только новый формат.
**Файлы:** `src/Infrastructure/Providers/FileStorageProviders/MessageHistoryRecordListConverter.cs`, `src/Infrastructure/Providers/FileStorageProviders/MessageHistoryProvider.cs` (override опций + метод миграционного save с `.bak`-циклом FR-034), `src/Infrastructure/Providers/FileStorageProviders/BaseFileStorageProvider.cs` (только если понадобится `virtual` хук опций), `src/Infrastructure/IoC/InfrastructureRegistration.cs` (регистрация расширения под `.bak`-метод, если вводится новый интерфейс)
**Зависит от:** Шаг 1
**Риски:** `Deserialize` в `BaseFileStorageProvider.GetDataAsync:23` вызывается без опций — конвертер через опции провайдера не подхватится, пока не пробросить опции в чтение. Альтернативы: (A) добавить `protected virtual JsonSerializerOptions ReadOptions` в базу и override в `MessageHistoryProvider`; (B) положить конвертер-атрибут на Domain-модель (нарушает зависимости). Выбран (A) как минимальный. Второй риск: `Dictionary<Guid,…>` ключи + `PropertyNameCaseInsensitive=false` — `Version`/`Messages`/`Id`/`Body`/`Properties`/`CreatedAt` чувствительны к регистру; legacy-файл использует те же имена (`Messages`), ок. Третий: пустые/whitespace legacy-строки пропускать (FR-031), не создавать записи.
**Детали реализации:**
- `Read`: `Utf8JsonReader`, `TokenType.String` → `new MessageHistoryRecord { Id = Guid.NewGuid(), Body = stringValue, Properties = new(), CreatedAt = <единое время миграции> }`; `TokenType.StartObject` → стандартный разбор `Id/Body/Properties/CreatedAt` (дефолтные значения при отсутствии полей: `Id=NewGuid()`, `Properties=new()`, `CreatedAt=now`); `Null` → пропуск. Функции ≤ ~20 строк: разбить на `ReadRecord(ref reader)` / `ReadLegacyString(...)` / `ReadNewObject(...)`. ≤ 3 параметров, guard clauses (`reader`, `options` null-check не нужен — struct, но `typeToConvert` проверить).
- `Write`: всегда массив объектов `{Id,Body,Properties,CreatedAt}`; строковый формат никогда не пишется.
- Подключение: в `MessageHistoryProvider` override опций чтения/записи с `Converters.Add(new MessageHistoryRecordListConverter())`; `WriteIndented=true` сохранить. Если база не даёт хука — добавить `protected virtual JsonSerializerOptions GetReadOptions()/GetWriteOptions()` (по умолчанию текущее поведение), override только в `MessageHistoryProvider` (`AppConfigurationProvider` не затронут).
- `.bak`-метод (FR-034, здесь же, т.к. это Infrastructure): напр. `Task SaveMigratedAsync(MessagesHistory data)` — внутри: `File.Copy(json → .bak)` → `SaveDataAsync(Version=2)` → контрольный `GetDataAsync` + проверка `Version==2` → удалить `.bak`; при неуспехе — восстановить из `.bak`, `.bak` сохранить, бросить типизированное исключение (сервис залогирует + покажет дружелюбную ошибку, файл новым не считается). Весь файловый код — только здесь, сервис в `System.IO` не ходит. Контракт метода — через узкий `IMessageHistoryStorageProvider : IFileStorageProvider<MessagesHistory>` (объявляется в Шаге 3 в Domain, реализуется здесь).
- `CreatedAt` для всех legacy-записей одного чтения — одно значение `DateTimeOffset.UtcNow` на весь `Read` списка (порядок списка = порядок JSON, FR-031).
- Верификация: временный ручной тест — скопировать реальный `src/WebUI/Data/messagesHistory.json` во временную папку, прочитать через провайдер: все непустые тела на месте в том же порядке, `Properties` пустые, `Id` уникальны, исключений нет. Записи в файл на этом шаге быть не должно.

---

### Шаг 3: Переписать сервисный слой (контракт + CRUD + миграция + .bak)
**Статус:** ⬜ TODO
**Что:** Удалить `IMessageHistory<Guid,List<string>>`, ввести `IMessageHistoryService`, полностью переписать `FileBasedMessageHistory`: CRUD по `Id`, валидация по `MessagePropertiesLimits`, order-independent no-diff, дедуп по телу + eviction 10, однократный миграционный persist с `.bak`-циклом (FR-034), graceful-путь битого JSON (FR-033), structured-логи без тел/значений.
**Зачем:** Ядро фичи: вся файловая логика в одном месте, UI становится тонким; сегодняшние дедуп/лимит живут в 4 страницах копипастом — переезд в сервис устраняет рассинхрон (см. риски).
**Файлы:** `src/Domain/Interfaces/Services/IMessageHistory.cs` (удалить), `src/Domain/Interfaces/Services/IMessageHistoryService.cs` (создать), `src/Domain/Interfaces/Providers/IMessageHistoryStorageProvider.cs` (создать — узкое расширение под `.bak`-метод Шага 2), `src/Application/Services/FileBasedMessageHistory.cs`, `src/Application/IoC/ApplicationRegistration.cs`
**Зависит от:** Шаг 2
**Риски:**
- Смена контракта ломает 4 pages + `Configuration.razor` одновременно — компиляция красная до Шага 4-5 by design; митигация: сначала этот шаг до зелёного `dotnet build src/Application`, затем поочерёдно страницы.
- `.bak`-цикл (FR-034, решено 2026-09-06): отдельный метод в `MessageHistoryProvider` (напр. миграционный save с backup+verify); сервис вызывает его, в `System.IO` напрямую не ходит. Чтобы не нарушить DIP (сервис зависит от абстракции, а не от конкретного провайдера): ввести узкий интерфейс напр. `IMessageHistoryStorageProvider : IFileStorageProvider<MessagesHistory>` с методом миграционного save (объявить в Domain/Interfaces/Providers рядом с `IFileStorageProvider`, реализовать в `MessageHistoryProvider`, зарегистрировать в `InfrastructureRegistration`), сервис зависит только от него. `File.Copy/.bak`-логика — только в Infrastructure.
- Окно read-modify-write между `GetDataAsync` и `SaveDataAsync` вне одного семафора (как сегодня) — last-write-wins из двух вкладок; не чинить в этом плане (Non-Goal), только не ухудшить (один read + ≤1 write на операцию).
- `GetHistoryAsync` сегодня возвращает внутреннюю ссылку — обязательно возвращать глубокие копии (`new MessageHistoryRecord { … new Dictionary(props) }`), иначе UI будет мутировать закэшированный объект провайдера без `Save`.
- `RemoveAllAsync(Guid)` сегодня fire-and-forget из `Configuration.razor` без `await` — сигнатуру не менять, поведение no-op при отсутствии ключа сохранить.
**Детали реализации:**
- Новый интерфейс (XML-доки, `Task`-суффиксы у async):
  `Task<IReadOnlyList<MessageHistoryRecord>> GetHistoryAsync(Guid configId);`
  `Task<MessageHistoryRecord?> AddMessageAsync(Guid configId, string body, IReadOnlyDictionary<string,string>? properties = null);`
  `Task UpdatePropertiesAsync(Guid configId, Guid messageId, IReadOnlyDictionary<string,string> properties);`
  `Task RemoveMessageAsync(Guid configId, Guid messageId);`
  `Task RemoveAllAsync(Guid configId);`
- `GetHistoryAsync`: `Guid.Empty` → `[]`; `try GetDataAsync catch JsonException → error-лог + []`, файл НЕ трогать (FR-033) — тот же graceful-путь обязателен во ВСЕХ методах, вызывающих `GetDataAsync` (`Add/Update/Remove`: при битом JSON — no-op + error-лог, без `Save`, чтобы не затереть файл до первой успешной мутации валидных данных); `null` → `[]`; после десериализации если `Version < 2` (legacy без поля десериализуется как `0` благодаря дефолту Шага 1) → один миграционный save (`Version=2`) через `.bak`-метод провайдера (FR-032/034) + лог `Migrated legacy history {Configs},{Entries}` (только counts); повторные вызовы файл не переписывают. Возврат — копии, старые→новые.
- `AddMessageAsync`: guards (`Guid.Empty`/whitespace body → `null`, без записи — как сегодня no-op в UI, теперь и в сервисе); валидация properties по лимитам (`>30`/`key empty`/`>256` → `ArgumentException` с указанием ключа); дедуп по точному `Body ==` (case-sensitive, ordinal — как `List.Contains` сегодня); eviction: пока `Count >= 10` удалять индекс 0; создать запись `{ NewGuid(), Body, new Dictionary(props ?? {}), UtcNow }`; 1 `SaveDataAsync`; вернуть копию созданной. Пустые properties → пустой словарь, не `null` (FR-012).
- `UpdatePropertiesAsync`: guards; неизвестный `configId`/`messageId` → no-op без исключения и без записи; валидация лимитов; order-independent сравнение (`Count` + все `TryGetValue` равны, ordinal) — при равенстве 0 writes + лог `History write skipped (no diff) {ConfigId},{MessageId}`; при диффе — снапшот целиком перезаписывает (`new Dictionary`), 1 write. Дополнительно: если при обновлении выбранной записи её новое тело совпало бы с другой — этот кейс резолвится на UI (FR-022), сервис тела здесь не меняет (только properties).
- `RemoveMessageAsync/RemoveAllAsync`: no-op без исключения при отсутствии; `Remove` записи удаляет и properties атомарно (одна запись — один write, FR-013).
- Логи: `Getting message history {ConfigId}` (Information), миграция, skip-no-diff; НИКОГДА тела/значения. Сырые `IOException/JsonException` в UI не пробрасывать как тосты — только дружелюбное сообщение на UI-слое (Шаг 4-5).
- Ограничения: функции ≤ ~20 строк (разбить на `ValidateProperties`, `AreEqual`, `Clone`, `EnsureMigratedAsync`, `SaveWithBackupIfMigratingAsync`), ≤ 3 параметров, guard clauses сверху, без `catch {}` (только типизированные `catch (JsonException)` / `catch (IOException)` с логом).
- Верификация: `dotnet build src/Application/Application.csproj` + `src/Domain` + `src/Infrastructure` зелёные; страницы и `Configuration.razor` красные (ожидаемо до Шагов 4-5).

---

### Шаг 4: Перевести `MessageSendPanel` на записи с `Id`
**Статус:** ⬜ TODO
**Что:** Заменить `List<string> MessagesHistory` на `IReadOnlyList<MessageHistoryRecord>`, коллбеки `EventCallback<string>` → `EventCallback<Guid>`, рендер превью из `Body`, сохранить вёрстку/`aria`/стили.
**Зачем:** Панель — единственная точка дропдауна истории; без неё страницы не смогут работать с `Id`.
**Файлы:** `src/WebUI/Components/Shared/MessageSendPanel.razor`
**Зависит от:** Шаг 3
**Риски:** Раздуть компонент логикой сравнения/снапшотов — нельзя, панель остаётся dumb (только рендер + проброс `Id`). `TrimHistoryMessage(string)` → `TrimHistoryMessage(MessageHistoryRecord)` или `GetPreview(Guid)` — null/пустой `Body` не должен ронять рендер. `@key` для `<li>` — по `record.Id`, не по индексу/телу (иначе дубли тел после миграции до дедупа прыгают).
**Детали реализации:**
- Параметры: `IReadOnlyList<MessageHistoryRecord> MessagesHistory { get; set; } = []` (или `Array.Empty`), `EventCallback<Guid> OnSelectMessage`, `EventCallback<Guid> OnRemoveFromHistory`; `MessageToSend/ShowHistory/OnSend/OnToggleHistory` без изменений. `@using Domain.Models`.
- Рендер: `@foreach (var record in MessagesHistory)` с `@key="record.Id"`; кнопка выбора показывает `Preview(record.Body)` (первые ~50 символов: `>50 → [..45]...`, как сегодня), кнопка удаления вызывает `OnRemoveFromHistory.InvokeAsync(record.Id)`.
- Пустой `Body` (не должен прийти из сервиса, но защита): превью `"(empty)"`, без исключения.
- Верификация: `dotnet build src/WebUI` всё ещё красный только на 4 страницах (ожидаемо); панель сама компилируется; визуально дропдаун не изменился кроме `@key`.

---

### Шаг 5: Перевести 4 bus-страницы на `selectedMessageId` + in-memory properties
**Статус:** ⬜ TODO
**Что:** Во всех 4 страницах (`EventHub`, `ServiceBus`, `RabbitMq`, `StorageQueue`) заменить `List<string> messagesHistoryList` на `List<MessageHistoryRecord>`, добавить `selectedMessageId: Guid?` + in-memory snapshot properties, реализовать резолв FR-022 и сравнение на `Send`, обновить `Toggle/Select/Remove`, сохранить `MAX_HISTORY_MESSAGES=10` как клиентский пред-чек (источник истины — сервис), сохранить хинт StorageQueue про игнор properties.
**Зачем:** G1/G2: выбор записи подставляет тело+properties; правки до `Send` — только память; повторный `Send` без диффа — 0 writes.
**Файлы:** `src/WebUI/Components/Pages/EventHub.razor`, `ServiceBus.razor`, `RabbitMq.razor`, `StorageQueue.razor`
**Зависит от:** Шаг 4
**Риски:**
- 4× копипаст (~90% идентичны) — править все 4 одинаково, иначе рассинхрон; рефакторинг общего базового компонента — Non-Goal, не делать.
- Потеря несохранённых правок при закрытии — by design (§11); добавить `[SHOULD]`-подсказку «Properties сохраняются при Send» рядом с редактором (одна строка `form-text`, без смены структуры/`aria`).
- `StorageQueue` игнорирует properties на бэкенде, но историю properties всё равно хранить (как сегодня `props` передаётся, игнор на продюсере) — поведение не менять, только хинт сохранить.
- `SelectMessage` сегодня только подставляет тело — теперь подставляет и properties (копия словаря, не ссылка на запись!), иначе правки в редакторе мутируют список напрямую.
- `Send` с пустым телом — no-op (как сегодня), `selectedMessageId` не сбрасывать.
**Детали реализации (одинаково в 4 файлах):**
- Поля: `private List<MessageHistoryRecord> messagesHistoryList = []; private Guid? selectedMessageId;` (`messageProperties: IDictionary<string,string>` остаётся in-memory). Инъекция: `IMessageHistoryService? MessagesHistoryService`.
- `ToggleMessageHistory`: как сегодня (lazy-load если `show && Count==0`), но `GetHistoryAsync(Id)` возвращает копии записей; пустая история → дропдаун не открывать.
- `SelectMessage(Guid messageId)`: найти запись по `Id` (если нет — no-op); `messageToSend = record.Body; messageProperties = new Dictionary(record.Properties); selectedMessageId = messageId; showMessageHistory = false;`
- `SendMessage`: построить `props` для продюсера как сегодня (`Count==0 → null`, иначе `ToDictionary(kv=>Key,(object)Value)`); `await producer.SendMessagesAsync(...)`; затем история:
  1. `selectedMessageId` существует в списке → `UpdatePropertiesAsync` только если in-memory props != `record.Properties` (order-independent сравнение в памяти, дублирует сервисную проверку как защита от дубля); тело при этом не меняется; после — удалить прочие записи с тем же телом (дедуп, FR-022) через `RemoveMessageAsync` + локально;
  2. иначе найти запись с `Body == messageToSend` → обновить её properties при диффе, `selectedMessageId = её Id`;
  3. иначе `AddMessageAsync(Id, body, snapshot)` → `selectedMessageId = новой записи.Id`; локальный eviction-чек `while (Count >= 10) RemoveFromHistory(list[0].Id)` оставить как пред-чек (сервис — источник истины).
  Снапшот для сервиса: `new Dictionary<string,string>(messageProperties)` (копия). Сравнение: `Count` + `TryGetValue` ordinal, без хешей (коллизий нет по построению).
- `RemoveFromHistory(Guid messageId)`: локально `RemoveAll(r => r.Id == id)`, сервис `RemoveMessageAsync`; если удалена выбранная — `selectedMessageId = null`; пустой список → закрыть дропдаун. `DisposeAsync`: `Clear()` + `selectedMessageId = null` как сегодня.
- `Configuration.razor`: убедиться, что `RemoveAllAsync(configId)` компилируется без изменений (сигнатура та же); fire-and-forget оставить как есть.
- Ограничения: новые приватные методы ≤ ~20 строк (`FindRecord`, `PropertiesEqual`, `SnapshotProperties`), guard clauses, без `catch {}`; тосты ошибок — дружелюбные, без тел/значений и без сырых `IOException/JsonException`.
- Верификация: `dotnet build EventHubExplorer.sln` зелёный; ручные приёмки (см. ниже): (1) новая отправка → запись с properties, рестарт → выбор подставляет оба; (2) правки черновика до Send → mtime неизменен; (3) повторный Send без изменений → файл байт-в-байт; (4) legacy-файл → все тела в порядке, props пустые, `.bak` создан и удалён, второй запуск без записи; (5) лимит 10 + eviction oldest-first на каждой из 4 страниц; (6) битый JSON → «Message history unavailable, starting fresh.», файл цел.

---

## Открытые вопросы

- [x] OQ-1 — старый `IMessageHistory<Guid,List<string>>` удалить сразу или держать obsolete-адаптер? → Решено (2026-09-06): удалить сразу в том же PR, единственный потребитель — 4 pages — правится вместе.
- [x] OQ-2 — делать ли `.bak` перед миграционным save? → Решено (2026-09-06): да; `.bak` → запись → контрольное чтение → удаление `.bak` (FR-034); при неуспехе — ошибка пользователю, восстановление из `.bak`, `.bak` сохраняется.
- [x] `.bak`-цикл: где реализовать → Решено (2026-09-06): отдельный метод в `MessageHistoryProvider` (Application в `System.IO` не ходит; `BaseFileStorageProvider` и общий `IFileStorageProvider` не трогаем сверх необходимого).
- [x] Тесты → Решено (2026-09-06): только ручная приёмка по 6 сценариям Шага 5; заведение `tests/*.csproj` — отдельный follow-up вне скоупа.

## Вне scope

- История версий properties (только последний снапшот), типизированные значения (`byte[]/int/Guid/…` — только `string`), синхронизация между вкладками/процессами сверх `SemaphoreSlim` (last-write-wins), смена лимита 10, рефакторинг дублирования 4 страниц в общий компонент, шифрование файла истории, заведение тестового проекта с нуля.
