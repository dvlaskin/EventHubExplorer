using Domain.Interfaces;
using Domain.Interfaces.Providers;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Providers;

public class InMemoryCacheProvider : ICacheProvider
{
    private readonly IMemoryCache cache;

    public InMemoryCacheProvider(IMemoryCache cache)
    {
        this.cache = cache;
    }
    
    public void SetValue<T>(string key, T value, TimeSpan expirationTimeSpan = default)
    {
        if (expirationTimeSpan == TimeSpan.Zero)
            cache.Set(key, value);
        else
            cache.Set(key, value, expirationTimeSpan);
    }
    
    public bool TryGetValue<T>(string key, out T? value)
    {
        return cache.TryGetValue(key, out value);
    }
}