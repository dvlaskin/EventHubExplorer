using System.Text.Json;
using Domain.Entities;
using Domain.Interfaces.Providers;

namespace Infrastructure.Providers;

public class ConfigProvider : IConfigProvider
{
    private const string ConfigPath = "Data/appConfig.json";

    public async Task<AppConfiguration?> LoadConfigAsync()
    {
        if (!File.Exists(ConfigPath))
            return null;

        var json = await File.ReadAllTextAsync(ConfigPath);
        return JsonSerializer.Deserialize<AppConfiguration>(json);
    }

    public async Task SaveConfigAsync(AppConfiguration newConfig)
    {
        var json = JsonSerializer.Serialize(newConfig, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(ConfigPath, json);
    } 
}