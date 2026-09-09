# EventHub Explorer

![.NET Version](https://img.shields.io/badge/.NET%20Version-10.0-blueviolet)
![License](https://img.shields.io/badge/license-MIT-green)
![Docker Pulls](https://img.shields.io/badge/docker-dvlaskin%2Feventhubexplorer-blue)

> Developer GUI tool to send, receive and inspect messages in **Azure Event Hubs**, **Azure Storage Queues**, **Azure Service Bus** and **RabbitMQ** — works with both real Azure services and local emulators.

---

## 📑 Table of Contents

- [1. Overview](#1-overview)
- [2. Supported Services](#2-supported-services)
- [3. Screenshots](#3-screenshots)
- [4. Features](#4-features)
  - [4.1 Common Operations (all services)](#41-common-operations-all-services)
  - [4.2 Service-Specific Differences](#42-service-specific-differences)
- [5. Requirements](#5-requirements)
- [6. Installation](#6-installation)
  - [6.1 Run Locally (from source)](#61-run-locally-from-source)
  - [6.2 Run with Docker](#62-run-with-docker)
  - [6.3 Docker Compose Example](#63-docker-compose-example)
- [7. Connection Strings](#7-connection-strings)
  - [7.1 Event Hubs](#71-event-hubs)
  - [7.2 Storage (Blob & Queue)](#72-storage-blob--queue)
  - [7.3 Service Bus](#73-service-bus)
  - [7.4 RabbitMQ](#74-rabbitmq)
- [8. Support the Project](#8-support-the-project)
- [9. License](#9-license)

---

## 1. Overview

**EventHub Explorer** is a lightweight web UI for developers working with messaging infrastructure. It abstracts away SDK boilerplate and lets you quickly push and pull messages, verify serialization/compression, and test consumer logic — without writing temporary console apps.

Use it against **production Azure** or fully **offline with emulators** (`eventhubs-emulator`, `Azurite`, `Service Bus emulator`) and **RabbitMQ**.

---

## 2. Supported Services

| Service | Real Azure / Broker | Emulator / Local |
| :--- | :--- | :--- |
| **Azure Event Hubs** | ✅ Namespace & Event Hub level connection strings | ✅ [eventhubs-emulator](https://learn.microsoft.com/en-us/azure/event-hubs/overview-emulator) |
| **Azure Storage Queue** | ✅ `*.queue.core.windows.net` | ✅ [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite) |
| **Azure Blob Storage** | ✅ Checkpoint storage for Event Hubs | ✅ Azurite (`UseDevelopmentStorage=true`) |
| **Azure Service Bus** | ✅ Namespace-level (Queue / Topic) | ✅ [Service Bus emulator](https://learn.microsoft.com/en-us/azure/service-bus-messaging/overview-emulator) |
| **RabbitMQ** | ✅ `amqp://` / `amqps://` | ✅ `rabbitmq:management` Docker image |

---

## 3. Screenshots

<p align="center">
  <img src="./docs/assets/Screenshot_01.png" alt="HomePage" width="90%"/>
</p>
<p align="center"><em>Home</em></p>

<p align="center">
  <img src="./docs/assets/Screenshot_02.png" alt="ConfigurationPage" width="90%"/>
</p>
<p align="center"><em>Configuration</em></p>

<p align="center">
  <img src="./docs/assets/Screenshot_03.png" alt="EventHubPage" width="90%"/>
</p>
<p align="center"><em>Message Hub</em></p>

---

## 4. Features

> All four messaging systems share the same core workflow — payload transformation, compression and batch sending are implemented once and reused across every service. Only entity types and receiving modes differ.

### 4.1 Common Operations (all services)

| Operation | What it does                                                                                                                                              |
| :--- |:----------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Override `GUID` / `DateTime`** | Auto-replaces detected GUID/Date/DateTime values with freshly generated ones before send |
| **Compress & encode** | GZip + Base64 on send, auto-decompress & decode on receive                                                                                                |
| **Send single message** | Push one message to the selected entity                                                                                                                   |
| **Send batch** | Push N messages at once                                                                                                                                   |
| **Send batch with delay** | Push N messages with a configurable interval between each                                                                                                 |
| **Auto-format JSON** | Pretty-prints payload on receive if it is valid JSON                                                                                                      |
| **Custom properties** | Add and read custom key-value properties                                                                                                                 |

### 4.2 Service-Specific Differences

| Capability | Event Hubs | Storage Queues | Service Bus | RabbitMQ |
| :--- | :---: | :---: | :---: | :---: |
| **Send to** | Event Hub (partitioned stream) | Queue | Queue **or** Topic | Queue **or** Exchange (`direct` / `fanout` / `topic`) |
| **Receive from** | Event Hub — with or without checkpoints* | Queue (polling) | Queue **or** Topic Subscription | Queue **or** Queue bound to Exchange (routing key) |
| **Checkpoint / offset tracking** | ✅ Without checkpoints (always newest) / With checkpoints via Blob storage (Azurite / Azure Blob) | — | — | — |
| **Custom properties** | ✅ | — | ✅ | ✅ |

> **\*** Event Hubs uses the `$Default` consumer group by default. Checkpoints require an external Blob storage — Azurite locally or Azure Blob in production.

---

## 5. Requirements

| Requirement | Notes |
| :--- | :--- |
| **Docker** | Required for emulators / containerized run |
| **.NET 10 SDK** | Only if building from source |
| **Azure Service** * | Only the service(s) you plan to use |
| **RabbitMQ** * | Only if using RabbitMQ (local Docker is enough) |

> `*` — you need at least one of the marked services to have something to connect to.

---

## 6. Installation

### 6.1 Run Locally (from source)

```bash
git clone https://github.com/dvlaskin/EventHubExplorer.git
cd EventHubExplorer
dotnet build
dotnet run --project src/WebUI/WebUI.csproj
# Open http://localhost:5235/
```

### 6.2 Run with Docker

```bash
docker pull dvlaskin/eventhubexplorer
docker run -d -p 5235:8080 --name eventhubexplorer dvlaskin/eventhubexplorer
# Open http://localhost:5235/
```

### 6.3 Docker Compose Example

```yaml
services:
  eventhubexplorer:
    image: dvlaskin/eventhubexplorer:latest
    container_name: eventhubexplorer
    ports:
      - "5235:8080"
    volumes:
      - data-volume:/app/Data
    networks:
      - docker-network

volumes:
  data-volume:

networks:
  docker-network:
    external: true
```

---

## 7. Connection Strings

> Replace placeholders like `<NamespaceName>`, `<KeyName>`, `<AccountName>` with your actual values.

### 7.1 Event Hubs

#### Event Hubs Emulator — static connection string, host varies by deployment

**App natively on same machine as emulator:**

```
Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

**App on different machine in same LAN (use emulator host IPv4):**

```
Endpoint=sb://192.168.x.y;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

**App container on same Docker bridge network (use container alias, default `eventhubs-emulator`):**

```
Endpoint=sb://eventhubs-emulator;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

**App container on different Docker bridge network:**

```
Endpoint=sb://host.docker.internal;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

#### Real Azure Event Hubs

**Namespace-level (access to all event hubs):**

```
Endpoint=sb://<NamespaceName>.servicebus.windows.net/;SharedAccessKeyName=<KeyName>;SharedAccessKey=<KeyValue>
```

**Event hub-level (access to one event hub):**

```
Endpoint=sb://<NamespaceName>.servicebus.windows.net/;SharedAccessKeyName=<KeyName>;SharedAccessKey=<KeyValue>;EntityPath=<EventHubName>
```

### 7.2 Storage (Blob & Queue)

#### Azurite Blob Storage (Emulator) — checkpoint store for Event Hubs

> Default Azurite well-known account: `devstoreaccount1` / `Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==`

**Localhost:**

```
DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;
```

**Same Docker bridge network (service name `azurite`):**

```
DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://azurite:10000/devstoreaccount1;
```

**Shortcut (local without Docker):**

```
UseDevelopmentStorage=true
```

#### Azurite Storage Queue (Emulator)

**Localhost:**

```
DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;
```

**Same Docker bridge network:**

```
DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;QueueEndpoint=http://azurite:10001/devstoreaccount1;
```

#### Real Azure Storage Queue

```
DefaultEndpointsProtocol=https;AccountName=<AccountName>;AccountKey=<AccountKey>;QueueEndpoint=https://<AccountName>.queue.core.windows.net;
```

### 7.3 Service Bus

#### Service Bus Emulator

**Same machine:**

```
Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

**Different machine in LAN:**

```
Endpoint=sb://192.168.x.y;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

**Same Docker bridge network (alias `servicebus-emulator`):**

```
Endpoint=sb://servicebus-emulator;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

**Different Docker bridge network:**

```
Endpoint=sb://host.docker.internal;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

> **Note (management operations):** For Administration Client (create/delete entities) append port `5300`: `Endpoint=sb://localhost:5300;...`

#### Real Azure Service Bus — namespace-level

```
Endpoint=sb://<NamespaceName>.servicebus.windows.net/;SharedAccessKeyName=<KeyName>;SharedAccessKey=<KeyValue>
```

### 7.4 RabbitMQ

**Run with Management UI:**

```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:management
```

Management UI: http://localhost:15672 (`guest` / `guest`)

**Default vhost (`/`):**

```
amqp://guest:guest@localhost:5672
```

**Custom vhost:**

```
amqp://guest:guest@localhost:5672/myvhost
```

**TLS:**

```
amqps://user:pass@hostname:5671/vhost
```

> **Note:** Queues and exchanges are declared automatically (durable) on send / receive. For Exchange entity type, specify both Routing Key and the Queue Name used to bind a queue for receiving.

---

## 8. Support the Project

If you find **EventHub Explorer** useful, consider giving it a ⭐ star on GitHub and Dockerhub.

> Your star is the best signal that this project is worth continuing. Thank you!

---

## 9. License

MIT.

[↑ Back to top](#eventhub-explorer)
