using Azure.Storage.Blobs;
using Domain.Configs;
using Domain.Interfaces.Factories;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Factories;

public class BlobStorageFactory : IStorageClientFactory<BlobConfig, BlobContainerClient>
{
    private readonly ILogger<BlobStorageFactory> logger;
    
    public BlobStorageFactory(ILogger<BlobStorageFactory> logger)
    {
        this.logger = logger;
    }
    
    public BlobContainerClient CreateStorageClient(BlobConfig config)
    {
        logger.LogInformation("Creating BlobContainerClient for container: {ContainerName}", config.BlobContainerName);
        
        var blobServiceClient = new BlobServiceClient(config.ConnectionString);
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(config.BlobContainerName);
        blobContainerClient.CreateIfNotExists();
        return blobContainerClient;
    }
}