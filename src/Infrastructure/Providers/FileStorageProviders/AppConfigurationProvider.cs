using Domain.Configs;

namespace Infrastructure.Providers.FileStorageProviders;

public sealed class AppConfigurationProvider : BaseFileStorageProvider<AppConfiguration>
{
    private const string ConfigPath = "Data/appConfig.json";

    protected override string DataFilePath => ConfigPath;
}