using System.Text.Json;
using Domain.Interfaces.Providers;

namespace Infrastructure.Providers.FileStorageProviders;

public abstract class BaseFileStorageProvider<T> : IFileStorageProvider<T>
{
    protected abstract string DataFilePath { get; }
    private readonly JsonSerializerOptions jsSerializerOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private string? dataDirectoryPath = null;
    
    public async Task<T?> GetDataAsync()
    {
        await semaphore.WaitAsync();
        try
        {
            if (!File.Exists(DataFilePath))
                return default;

            var json = await File.ReadAllTextAsync(DataFilePath);
            return JsonSerializer.Deserialize<T>(json);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task SaveDataAsync(T data)
    {
        var json = JsonSerializer.Serialize(data, jsSerializerOptions);

        await semaphore.WaitAsync();

        try
        {
            if (!Directory.Exists(GetDataDirectoryPath()))
            {
                Directory.CreateDirectory(GetDataDirectoryPath());
            }
            
            await File.WriteAllTextAsync(DataFilePath, json);
        }
        finally
        {
            semaphore.Release();
        }
    }
    
    private string GetDataDirectoryPath()
    {
        dataDirectoryPath ??= Path.GetDirectoryName(DataFilePath) ?? string.Empty;
        return dataDirectoryPath;
    }
}