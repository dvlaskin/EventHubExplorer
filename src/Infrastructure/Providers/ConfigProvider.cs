using System.Text.Json;
using Domain.Configs;
using Domain.Interfaces.Providers;

namespace Infrastructure.Providers;

public class ConfigProvider : IConfigProvider
{
    private const string ConfigPath = "Data/appConfig.json";
    private static readonly string ConfigDirectory = Path.GetDirectoryName(ConfigPath) ?? string.Empty;
    private static readonly JsonSerializerOptions JsSerializerOptions = new JsonSerializerOptions { WriteIndented = true };

    public async Task<AppConfiguration?> LoadConfigAsync()
    {
        if (!File.Exists(ConfigPath))
            return null;

        var json = await File.ReadAllTextAsync(ConfigPath);
        return JsonSerializer.Deserialize<AppConfiguration>(json);
    }

    public async Task SaveConfigAsync(AppConfiguration newConfig)
    {
        var json = JsonSerializer.Serialize(newConfig, JsSerializerOptions);
        
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }
        
        await File.WriteAllTextAsync(ConfigPath, json);
    } 
}