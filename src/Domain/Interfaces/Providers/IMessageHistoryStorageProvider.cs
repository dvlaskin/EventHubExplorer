using Domain.Models;

namespace Domain.Interfaces.Providers;

/// <summary>
/// A storage abstraction for the history of sent messages.
/// </summary>
public interface IMessageHistoryStorageProvider : IFileStorageProvider<MessagesHistory>
{
    /// <summary>
    /// Persists history with a backup cycle: copy to <c>.bak</c>, save with
    /// <c>Version = 2</c>, verify by re-reading, then delete the backup.
    /// On failure restores the backup, keeps the <c>.bak</c> file and throws.
    /// </summary>
    /// <param name="data">History to persist.</param>
    /// <exception cref="IOException">Thrown when the migrated file cannot be verified.</exception>
    Task SaveMigratedAsync(MessagesHistory data);
}
