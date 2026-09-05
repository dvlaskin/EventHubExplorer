using System.Collections.ObjectModel;

namespace Domain.Models;

/// <summary>
/// Immutable envelope for an outgoing message: text body plus optional key-value properties understood by every bus provider.
/// </summary>
public sealed record OutgoingMessage
{
    public string Message { get; init; }
    public Func<string, BinaryData>? MessageModifier { get; init; }
    public IReadOnlyDictionary<string, object>? Properties { get; init; }


    public OutgoingMessage(
        string Message,
        Func<string, BinaryData>? MessageModifier = null,
        IReadOnlyDictionary<string, object>? Properties = null
    )
    {
        ArgumentNullException.ThrowIfNull(Message);
        ValidateProperties(Properties);

        this.Message = Message;
        this.MessageModifier = MessageModifier;
        this.Properties = Properties is null
            ? null
            : new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(Properties));
    }


    private static void ValidateProperties(IReadOnlyDictionary<string, object>? properties)
    {
        if (properties is null)
        {
            return;
        }

        if (properties.Count > MessagePropertiesLimits.MaxPairs)
        {
            throw new ArgumentException(
                $"Too many properties (max {MessagePropertiesLimits.MaxPairs}).", nameof(Properties)
            );
        }

        foreach (var (key, value) in properties)
        {
            ValidateSingleProperty(key, value);
        }
    }

    private static void ValidateSingleProperty(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "Invalid message property: key must be a non-empty string.", nameof(Properties)
            );
        }

        if (key.Length > MessagePropertiesLimits.MaxKeyLength)
        {
            throw new ArgumentException(
                $"Invalid message property '{key}': key length exceeds {MessagePropertiesLimits.MaxKeyLength} characters.",
                nameof(Properties));
        }

        if (value is null)
        {
            throw new ArgumentException(
                $"Invalid message property '{key}': value must not be null.", nameof(Properties)
            );
        }

        if (value is not (string or byte[] or int or long or double or bool or Guid or DateTimeOffset))
        {
            throw new ArgumentException(
                $"Property '{key}' has unsupported type '{value.GetType().FullName}'.", nameof(Properties)
            );
        }

        if (value is string { Length: > MessagePropertiesLimits.MaxValueLength })
        {
            throw new ArgumentException(
                $"Invalid message property '{key}': string value exceeds {MessagePropertiesLimits.MaxValueLength} characters.",
                nameof(Properties)
            );
        }
    }
}
