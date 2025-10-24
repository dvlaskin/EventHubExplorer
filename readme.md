# EventHub Explorer

EventHub Explorer is a developer tool that allows you to interact with the Azure EventHubs service using a graphical user interface. It supports both real Azure Event Hubs and the [eventhubs-emulator](https://learn.microsoft.com/en-us/azure/event-hubs/overview-emulator).

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
* Receive messages **without checkpoints** (always fetch the newest messages)
* Receive messages **with checkpoints** (using external storage to track received message IDs)

> Note: The application uses the `$Default` consumer group by default.

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

## Requirements

* Docker (for emulator and Azurite usage)
* Azure Event Hubs and Blob Storage credentials (for real Azure usage)

## Install options

* Clone solution in the repo
* Or use docker image ```docker pull dvlaskin/eventhubexplorer```
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
