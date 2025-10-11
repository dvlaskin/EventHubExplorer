using System.Text.Json;
using Domain.Interfaces.Providers;

namespace Infrastructure.Providers.FileStorageProviders;

public abstract class BaseFileStorageProvider<T> : IFileStorageProvider<T>, IDisposable, IAsyncDisposable
{
    private bool disposed;
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
    
    
    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
            return;
        
        if (disposing)
        {
            semaphore.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
        disposed = true;
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (disposed)
            return;
        
        if (semaphore is IAsyncDisposable semaphoreAsyncDisposable)
            await semaphoreAsyncDisposable.DisposeAsync();
        else
            semaphore.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
        disposed = true;
    }

    ~BaseFileStorageProvider()
    {
        Dispose(false);
    }
}