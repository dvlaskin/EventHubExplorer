namespace Domain.Interfaces.Providers;

public interface ICacheProvider
{
    void SetValue<T>(string key, T value, TimeSpan expirationTimeSpan = default);
    bool TryGetValue<T>(string key, out T? value);
}