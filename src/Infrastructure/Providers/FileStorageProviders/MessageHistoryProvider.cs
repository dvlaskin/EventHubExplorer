using System.Text.Json;
using Domain.Models;

namespace Infrastructure.Providers.FileStorageProviders;

public sealed class MessageHistoryProvider : BaseFileStorageProvider<MessagesHistory>
{
    private const string ConfigPath = "Data/messagesHistory.json";
    private const string BackupExtension = ".bak";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        Converters = { new MessageHistoryRecordListConverter() },
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Converters = { new MessageHistoryRecordListConverter() },
    };

    
    protected override string DataFilePath => ConfigPath;

    protected override JsonSerializerOptions GetReadOptions() => ReadOptions;

    protected override JsonSerializerOptions GetWriteOptions() => WriteOptions;

    
    /// <summary>
    /// Persists migrated history with a backup cycle: copy to <c>.bak</c>, save with
    /// <c>Version = 2</c>, verify by re-reading, then delete the backup.
    /// On failure restores the backup, keeps the <c>.bak</c> file and throws.
    /// </summary>
    /// <param name="data">Migrated history to persist.</param>
    /// <exception cref="IOException">Thrown when the migrated file cannot be verified.</exception>
    public async Task SaveMigratedAsync(MessagesHistory data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.Version = 2;

        var backupPath = GetBackupPath();
        var backupCreated = CreateBackup(backupPath);
        var persisted = false;

        try
        {
            await SaveDataAsync(data);
            await VerifyMigratedAsync();
            persisted = true;
            DeleteBackup(backupPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            if (!persisted)
            {
                RestoreBackup(backupPath, backupCreated);
            }

            throw new IOException("Failed to persist migrated message history; backup restored.", ex);
        }
    }

    private string GetBackupPath() => $"{DataFilePath}{BackupExtension}";

    private bool CreateBackup(string backupPath)
    {
        if (!File.Exists(DataFilePath))
        {
            return false;
        }

        File.Copy(DataFilePath, backupPath, overwrite: true);
        return true;
    }

    private async Task VerifyMigratedAsync()
    {
        var reloaded = await GetDataAsync();

        if (reloaded?.Version != 2)
        {
            throw new IOException("Migrated message history verification failed.");
        }
    }

    private static void DeleteBackup(string backupPath)
    {
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }
    }

    private void RestoreBackup(string backupPath, bool backupCreated)
    {
        if (backupCreated && File.Exists(backupPath))
        {
            File.Copy(backupPath, DataFilePath, overwrite: true);
        }
    }
}
