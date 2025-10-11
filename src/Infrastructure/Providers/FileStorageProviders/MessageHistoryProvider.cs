using Domain.Models;

namespace Infrastructure.Providers.FileStorageProviders;

public class MessageHistoryProvider : BaseFileStorageProvider<MessagesHistory>
{
    private const string ConfigPath = "Data/messagesHistory.json";
    
    protected override string DataFilePath => ConfigPath;
}