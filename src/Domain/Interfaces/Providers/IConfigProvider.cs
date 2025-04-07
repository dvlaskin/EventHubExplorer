using Domain.Entities;

namespace Domain.Interfaces.Providers;

public interface IConfigProvider
{
    Task<AppConfiguration?> LoadConfigAsync();
    Task SaveConfigAsync(AppConfiguration newConfig);
}