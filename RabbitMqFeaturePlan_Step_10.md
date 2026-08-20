# Шаг 10: Docs — раздел RabbitMQ в `readme.md` + финальная проверка

_Файл: `RabbitMqFeaturePlan_Step_10.md`_

**Статус:** ⬜ TODO
**Что:** Добавить в `readme.md` раздел про RabbitMQ: описание фич (отправка/получение/Queue/Exchange), команду запуска Docker (`rabbitmq:management`), примеры AMQP URI, раздел Requirements. Затем — финальная сборка решения и ручная smoke-проверка всей фичи.
**Зачем:** G5 (документация), FR-остаток (приемка). `compose.yaml` не меняется — RabbitMQ запускается отдельной командой (Non-Goals спецификации).
**Файлы:**
- `readme.md` — ИЗМЕНИТЬ

**Зависит от:** все шаги 1–9
**Риски:**
- Не менять структуру readme — добавить блоки в существующие секции (Features, Example Connection Strings, Requirements).
- AMQP URI по умолчанию: `amqp://guest:guest@localhost:5672` (vhost по умолчанию `/`; в URI vhost `/` можно не указывать или указать `%2f`).
- `rabbitmq:management` — образ с Management UI на порту 15672; для нового vhost/пользователя — инструкция через rabbitmqctl.

**Детали реализации:**

**1. Features** — после секции «Receiving Messages from Service Bus» добавить:
```markdown
### Sending Messages to RabbitMQ

* Override GUID, DateTime values in a message before sending
* Ability to compress and encode a message before sending
* Send a **single message**
* Send a **batch of messages**
* Send a **batch of messages** with a **time delay** between each message
* Supports both **Queue** and **Exchange** (direct/fanout/topic) entity types

### Receiving Messages from RabbitMQ

* Format a message to JSON if it is a JSON string
* Ability to decompress and decode a message after receiving
* Receive messages from a **Queue** or from a **Queue bound to an Exchange** (routing key)
```

**2. Example Connection Strings** — после Service Bus раздела:
```markdown
### RabbitMQ (local Docker)

Run RabbitMQ with Management UI:

```
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:management
```

Management UI: http://localhost:15672 (default login: guest / guest)

**Local default vhost (`/`):**
```
amqp://guest:guest@localhost:5672
```

**Custom vhost:**
```
amqp://guest:guest@localhost:5672/myvhost
```

**TLS (if configured on the broker):**
```
amqps://user:pass@hostname:5671/vhost
```

> **Note:** Queues and exchanges are declared automatically (durable) by the application when you send or start receiving. For Exchange entity type, specify the Routing Key and the Queue Name used to bind a queue for receiving.
```

**3. Requirements** — добавить в список:
```
* RabbitMQ*
```

**Критерии завершения:**
- readme содержит полный раздел RabbitMQ (Features, docker run, AMQP URI, Requirements).
- Решение собирается; все acceptance criteria из раздела 12.3 спецификации подтверждены ручной проверкой.
- G1–G6 достигнуты, Application-слой не изменён (G4).