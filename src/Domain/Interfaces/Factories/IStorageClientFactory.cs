namespace Domain.Interfaces.Factories;

public interface IStorageClientFactory<in TConfig, out TResult>
{
    TResult CreateStorageClient(TConfig config);
}