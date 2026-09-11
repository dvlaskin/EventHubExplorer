namespace Domain.Models;

/// <summary>
/// Limits for outgoing message properties shared by all buses and the WebUI editor.
/// Single source of truth for validation.
/// </summary>
public static class MessagePropertiesLimits
{
    /// <summary>Maximum number of key-value pairs per message.</summary>
    public const int MaxPairs = 30;

    /// <summary>Maximum length of a property key.</summary>
    public const int MaxKeyLength = 256;

    /// <summary>Maximum length of a string property value.</summary>
    public const int MaxValueLength = 256;
}
