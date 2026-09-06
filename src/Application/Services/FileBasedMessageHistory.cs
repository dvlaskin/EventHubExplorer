using System.Text.Json;
using Domain.Interfaces.Providers;
using Domain.Interfaces.Services;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Application.Services;

/// <summary>
/// File-backed <see cref="IMessageHistoryService"/>: CRUD by entry <see cref="MessageHistoryRecord.Id"/>,
/// validation against <see cref="MessagePropertiesLimits"/>.
/// </summary>
public sealed class FileBasedMessageHistory : IMessageHistoryService
{
    private const int MaxHistoryMessages = 10;
    private const int MigratedVersion = 2;

    private readonly ILogger<FileBasedMessageHistory> logger;
    private readonly IMessageHistoryStorageProvider messagesStorageProvider;


    public FileBasedMessageHistory(
        ILogger<FileBasedMessageHistory> logger, IMessageHistoryStorageProvider messagesStorageProvider
    )
    {
        this.logger = logger;
        this.messagesStorageProvider = messagesStorageProvider;
    }


    /// <inheritdoc />
    public async Task<IReadOnlyList<MessageHistoryRecord>> GetHistoryAsync(Guid configId)
    {
        logger.LogInformation("Getting message history {ConfigId}", configId);

        if (configId == Guid.Empty)
            return [];

        var fullHistory = await LoadFullHistoryAsync();

        if (fullHistory is null)
            return [];

        await EnsureMigratedAsync(fullHistory);

        if (!fullHistory.Messages.TryGetValue(configId, out var history))
            return [];

        logger.LogInformation("History for {ConfigId} found, {CountItems}", configId, history.Count);
        return CloneList(history);
    }

    /// <inheritdoc />
    public async Task<MessageHistoryRecord?> AddMessageAsync(
        Guid configId, string body, IReadOnlyDictionary<string, string>? properties = null
    )
    {
        if (configId == Guid.Empty || string.IsNullOrWhiteSpace(body))
            return null;

        ValidateProperties(properties);

        var fullHistory = await LoadFullHistoryAsync();

        if (fullHistory is null)
            return null;

        var history = GetOrCreateHistory(fullHistory, configId);
        var existing = FindByBody(history, body);

        if (existing is not null)
            return Clone(existing);

        EvictOldest(history);

        var record = new MessageHistoryRecord
        {
            Id = Guid.NewGuid(),
            Body = body,
            Properties = Snapshot(properties),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        history.Add(record);

        await SaveAsync(fullHistory);
        return Clone(record);
    }

    /// <inheritdoc />
    public async Task UpdatePropertiesAsync(
        Guid configId, Guid messageId, IReadOnlyDictionary<string, string> properties
    )
    {
        ArgumentNullException.ThrowIfNull(properties);

        if (configId == Guid.Empty || messageId == Guid.Empty)
            return;

        ValidateProperties(properties);

        var fullHistory = await LoadFullHistoryAsync();

        if (fullHistory is null)
            return;

        var record = FindById(fullHistory, configId, messageId);

        if (record is null)
            return;

        if (AreEqual(record.Properties, properties))
        {
            logger.LogInformation("History write skipped (no diff) {ConfigId},{MessageId}", configId, messageId);
            return;
        }

        record.Properties = Snapshot(properties);
        await SaveAsync(fullHistory);
    }

    /// <inheritdoc />
    public async Task RemoveMessageAsync(Guid configId, Guid messageId)
    {
        if (configId == Guid.Empty || messageId == Guid.Empty)
            return;

        var fullHistory = await LoadFullHistoryAsync();

        if (fullHistory is null)
            return;

        if (!fullHistory.Messages.TryGetValue(configId, out var history))
            return;

        var removed = history.RemoveAll(r => r.Id == messageId) > 0;

        if (removed)
            await SaveAsync(fullHistory);
    }

    /// <inheritdoc />
    public async Task RemoveAllAsync(Guid configId)
    {
        if (configId == Guid.Empty)
            return;

        var fullHistory = await LoadFullHistoryAsync();

        if (fullHistory is null)
            return;

        if (fullHistory.Messages.Remove(configId))
            await SaveAsync(fullHistory);
    }


    private async Task<MessagesHistory?> LoadFullHistoryAsync()
    {
        try
        {
            return await messagesStorageProvider.GetDataAsync();
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Message history could not be parsed, starting fresh.");
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Message history unreadable, starting fresh.");
            return null;
        }
    }

    private async Task EnsureMigratedAsync(MessagesHistory fullHistory)
    {
        if (fullHistory.Version >= MigratedVersion)
            return;

        var configsCount = fullHistory.Messages.Count;
        var entriesCount = fullHistory.Messages.Values.Sum(h => h.Count);

        try
        {
            await messagesStorageProvider.SaveMigratedAsync(fullHistory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Failed to persist migrated message history.");
            throw;
        }

        logger.LogInformation("Migrated legacy history {MessagesCount},{Entries}", configsCount, entriesCount);
    }

    private async Task SaveAsync(MessagesHistory fullHistory)
    {
        if (fullHistory.Version < MigratedVersion)
        {
            await EnsureMigratedAsync(fullHistory);
            return;
        }

        try
        {
            await messagesStorageProvider.SaveDataAsync(fullHistory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Failed to save message history.");
            throw;
        }
    }

    private static List<MessageHistoryRecord> GetOrCreateHistory(MessagesHistory fullHistory, Guid configId)
    {
        if (!fullHistory.Messages.TryGetValue(configId, out var history))
        {
            history = [];
            fullHistory.Messages[configId] = history;
        }

        return history;
    }

    private static MessageHistoryRecord? FindByBody(List<MessageHistoryRecord> history, string body)
    {
        foreach (var record in history)
        {
            if (string.Equals(record.Body, body, StringComparison.Ordinal))
                return record;
        }

        return null;
    }

    private static MessageHistoryRecord? FindById(MessagesHistory fullHistory, Guid configId, Guid messageId)
    {
        if (!fullHistory.Messages.TryGetValue(configId, out var history))
            return null;

        foreach (var record in history)
        {
            if (record.Id == messageId)
                return record;
        }

        return null;
    }

    private static void EvictOldest(List<MessageHistoryRecord> history)
    {
        while (history.Count >= MaxHistoryMessages)
            history.RemoveAt(0);
    }

    private static void ValidateProperties(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null)
            return;

        if (properties.Count > MessagePropertiesLimits.MaxPairs)
            throw new ArgumentException($"Too many properties (max {MessagePropertiesLimits.MaxPairs}).", nameof(properties));

        foreach (var (key, value) in properties)
            ValidateSingleProperty(key, value);
    }

    private static void ValidateSingleProperty(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Invalid message property: key must be a non-empty string.", nameof(key));

        if (key.Length > MessagePropertiesLimits.MaxKeyLength)
            throw new ArgumentException(
                $"Invalid message property '{key}': key length exceeds {MessagePropertiesLimits.MaxKeyLength} characters.",
                nameof(key)
            );

        if (value is null)
            throw new ArgumentException($"Invalid message property '{key}': value must not be null.", nameof(value));

        if (value.Length > MessagePropertiesLimits.MaxValueLength)
            throw new ArgumentException(
                $"Invalid message property '{key}': string value exceeds {MessagePropertiesLimits.MaxValueLength} characters.",
                nameof(value)
            );
    }

    private static bool AreEqual(IReadOnlyDictionary<string, string> current, IReadOnlyDictionary<string, string> next)
    {
        if (current.Count != next.Count)
            return false;

        foreach (var (key, value) in next)
        {
            if (!current.TryGetValue(key, out var currentValue))
                return false;

            if (!string.Equals(currentValue, value, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static Dictionary<string, string> Snapshot(IReadOnlyDictionary<string, string>? source)
    {
        return source is null ? new Dictionary<string, string>() : new Dictionary<string, string>(source);
    }

    private static MessageHistoryRecord Clone(MessageHistoryRecord source)
    {
        return new MessageHistoryRecord
        {
            Id = source.Id,
            Body = source.Body,
            Properties = new Dictionary<string, string>(source.Properties),
            CreatedAt = source.CreatedAt,
        };
    }

    private static List<MessageHistoryRecord> CloneList(List<MessageHistoryRecord> source)
    {
        var clones = new List<MessageHistoryRecord>(source.Count);

        foreach (var record in source)
            clones.Add(Clone(record));

        return clones;
    }
}
