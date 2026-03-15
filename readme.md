# EventHub Explorer

![.NET Version](https://img.shields.io/badge/.NET%20Version-10.0-blueviolet)

EventHub Explorer is a developer tool that allows you to interact with Azure Event Hubs, 
Azure Storage Queues and Azure Service Bus using a graphical user interface.
It supports real Azure services, and
the [eventhubs-emulator](https://learn.microsoft.com/en-us/azure/event-hubs/overview-emulator)
for Event Hubs,
the [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite)
for StorageQueue,
the [Service Bus emulator](https://learn.microsoft.com/en-us/azure/service-bus-messaging/overview-emulator)
for Service Bus.

<img src="./docs/assets/Screenshot_01.png" alt="HomePage" width="50%"/>
<img src="./docs/assets/Screenshot_02.png" alt="ConfigurationPage" width="50%"/>
<img src="./docs/assets/Screenshot_03.png" alt="EventHubPage" width="50%"/>


## Features

### Sending Messages to Event Hubs

* Override GUID, DateTime values in a message before sending
* Ability to compress and encode a message before sending
* Send a **single message**
* Send a **batch of messages**
* Send a **batch of messages** with a **time delay** between each message

### Receiving Messages from Event Hubs

* Format a message to JSON if it is a JSON string
* Ability to decompress and decode a message after receiving
* Receive messages **without checkpoints** (always fetch the newest messages, if is not yet received another consumer)
* Receive messages **with checkpoints** (using external storage to track received message IDs)

> Note: The application uses the `$Default` consumer group by default.

### Sending Messages to Storage Queues

* Override GUID, DateTime values in a message before sending
* Ability to compress and encode a message before sending
* Send a **single message**
* Send a **batch of messages**
* Send a **batch of messages** with a **time delay** between each message

### Receiving Messages from Storage Queues

* Format a message to JSON if it is a JSON string
* Ability to decompress and decode a message after receiving

### Sending Messages to Service Bus

* Override GUID, DateTime values in a message before sending
* Ability to compress and encode a message before sending
* Send a **single message**
* Send a **batch of messages**
* Send a **batch of messages** with a **time delay** between each message
* Supports both **Queue** and **Topic** entity types

### Receiving Messages from Service Bus

* Format a message to JSON if it is a JSON string
* Ability to decompress and decode a message after receiving
* Receive messages from a **Queue** or a **Topic Subscription**


## Example Connection Strings

### EventHubs Emulator

```
Endpoint=sb://eventhub-docker;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

> `eventhub-docker` is the Docker service name of the Event Hubs emulator running in the same Docker network.

### Azurite Blob Storage

```
DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://eventhub-azurite:10000/devstoreaccount1;
```

> `eventhub-azurite` is the Docker service name of the Azurite blob storage running in the same Docker network.

### Azurite Storage Queue

```
DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;QueueEndpoint=http://eventhub-azurite:10001/devstoreaccount1;
```

> `eventhub-azurite` is the Docker service name of the Azurite storage running in the same Docker network.

### Service Bus Emulator

```
Endpoint=sb://servicebus-docker;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

> `servicebus-docker` is the Docker service name of the Service Bus emulator running in the same Docker network. The `UseDevelopmentEmulator=true` flag switches the transport to AmqpTcp for local emulator connectivity.


## Requirements

* Docker
* Azure Event Hubs*
* Blob Storage* 
* Storage Queue*
* Service Bus*
  
`*`- the service you want to use

## Install options

1. Option 1
* Clone solution from the repo
* Build and Run the solution locally
* Open in browser http://localhost:5235/

2. Option 2
* Pull and Run docker image ```docker pull dvlaskin/eventhubexplorer```
* Open in browser http://localhost:5235/

Example of a docker compose file
```
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

## License

MIT