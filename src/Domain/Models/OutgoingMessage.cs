using System.Collections.ObjectModel;

namespace Domain.Models;

/// <summary>
/// Immutable envelope for an outgoing message: text body plus optional key-value properties understood by every bus provider.
/// </summary>
public sealed record OutgoingMessage
{
    public string Message { get; }
    public Func<string, BinaryData>? MessageModifier { get; }
    public IReadOnlyDictionary<string, object>? Properties { get; }


    public OutgoingMessage(
        string message, 
        Func<string, BinaryData>? messageModifier = null, 
        IReadOnlyDictionary<string, object>? properties = null
    )
    {
        ArgumentNullException.ThrowIfNull(message);
        ValidateProperties(properties);

        this.Message = message;
        this.MessageModifier = messageModifier;
        this.Properties = properties is null
            ? null
            : new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(properties));
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
