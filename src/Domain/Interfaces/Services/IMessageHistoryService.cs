using Domain.Models;

namespace Domain.Interfaces.Services;

/// <summary>
/// Message history keyed by configuration with per-entry identity.
/// Stores the last sent body snapshot together with its properties snapshot.
/// </summary>
public interface IMessageHistoryService
{
    /// <summary>
    /// Returns deep copies of history entries for the configuration.
    /// </summary>
    /// <param name="configId">Configuration identifier.</param>
    /// <returns>History entries; empty when unknown or unreadable.</returns>
    Task<IReadOnlyList<MessageHistoryRecord>> GetHistoryAsync(Guid configId);

    /// <summary>
    /// Adds a new entry unless the exact body already exists (deduplication).
    /// </summary>
    /// <param name="configId">Configuration identifier.</param>
    /// <param name="body">Message body.</param>
    /// <param name="properties">Properties snapshot; null means empty.</param>
    /// <returns>Copy of the created (or already existing) entry; null when input is invalid or storage is unreadable.</returns>
    /// <exception cref="ArgumentException">Thrown when properties violate <see cref="MessagePropertiesLimits"/>.</exception>
    Task<MessageHistoryRecord?> AddMessageAsync(
        Guid configId, string body, IReadOnlyDictionary<string, string>? properties = null
    );

    /// <summary>
    /// Replaces the properties snapshot of an existing entry. No write when nothing changed.
    /// </summary>
    /// <param name="configId">Configuration identifier.</param>
    /// <param name="messageId">History entry identifier.</param>
    /// <param name="properties">New properties snapshot.</param>
    /// <exception cref="ArgumentException">Thrown when properties violate <see cref="MessagePropertiesLimits"/>.</exception>
    Task UpdatePropertiesAsync(Guid configId, Guid messageId, IReadOnlyDictionary<string, string> properties);

    /// <summary>
    /// Removes a single entry together with its properties snapshot. No-op when unknown.
    /// </summary>
    /// <param name="configId">Configuration identifier.</param>
    /// <param name="messageId">History entry identifier.</param>
    Task RemoveMessageAsync(Guid configId, Guid messageId);

    /// <summary>
    /// Removes all entries of a configuration. No-op when unknown.
    /// </summary>
    /// <param name="configId">Configuration identifier.</param>
    Task RemoveAllAsync(Guid configId);
}
