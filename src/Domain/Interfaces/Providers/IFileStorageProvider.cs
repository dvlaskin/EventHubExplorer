namespace Domain.Interfaces.Providers;

public interface IFileStorageProvider<T>
{
    Task<T?> GetDataAsync();
    Task SaveDataAsync(T newConfig);
}