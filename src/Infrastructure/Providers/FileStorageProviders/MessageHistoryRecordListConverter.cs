using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Models;

namespace Infrastructure.Providers.FileStorageProviders;

/// <summary>
/// Reads history entry lists in both formats (legacy JSON string = body only,
/// new JSON object = full record) and always writes the new object format.
/// </summary>
public sealed class MessageHistoryRecordListConverter : JsonConverter<List<MessageHistoryRecord>>
{
    public override List<MessageHistoryRecord> Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options
    )
    {
        var records = new List<MessageHistoryRecord>();

        if (reader.TokenType == JsonTokenType.Null)
        {
            return records;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected StartArray, got {reader.TokenType}.");
        }

        var migratedAt = DateTimeOffset.UtcNow;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var record = ReadRecord(ref reader, options, migratedAt);
            if (record is not null)
            {
                records.Add(record);
            }
        }

        return records;
    }

    public override void Write(
        Utf8JsonWriter writer, List<MessageHistoryRecord> value, JsonSerializerOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartArray();

        foreach (var record in value)
        {
            WriteRecord(writer, record, options);
        }

        writer.WriteEndArray();
    }


    private static MessageHistoryRecord? ReadRecord(
        ref Utf8JsonReader reader, JsonSerializerOptions options, DateTimeOffset migratedAt
    )
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => ReadLegacyString(ref reader, migratedAt),
            JsonTokenType.StartObject => ReadNewObject(ref reader, options, migratedAt),
            JsonTokenType.Null => null,
            _ => SkipUnknown(ref reader),
        };
    }

    private static MessageHistoryRecord? ReadLegacyString(
        ref Utf8JsonReader reader, DateTimeOffset migratedAt
    )
    {
        var body = reader.GetString();

        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        return new MessageHistoryRecord
        {
            Id = Guid.NewGuid(),
            Body = body,
            Properties = new Dictionary<string, string>(),
            CreatedAt = migratedAt,
        };
    }

    private static MessageHistoryRecord ReadNewObject(
        ref Utf8JsonReader reader, JsonSerializerOptions options, DateTimeOffset migratedAt
    )
    {
        var id = Guid.NewGuid();
        var body = string.Empty;
        var properties = new Dictionary<string, string>();
        var createdAt = migratedAt;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var name = reader.GetString();
            if (!reader.Read())
            {
                break;
            }

            switch (name)
            {
                case nameof(MessageHistoryRecord.Id):
                    id = ReadId(ref reader);
                    break;
                case nameof(MessageHistoryRecord.Body):
                    body = ReadBody(ref reader);
                    break;
                case nameof(MessageHistoryRecord.Properties):
                    properties = ReadProperties(ref reader, options);
                    break;
                case nameof(MessageHistoryRecord.CreatedAt):
                    createdAt = ReadCreatedAt(ref reader, migratedAt);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return new MessageHistoryRecord
        {
            Id = id,
            Body = body,
            Properties = properties,
            CreatedAt = createdAt,
        };
    }

    private static Guid ReadId(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return Guid.NewGuid();
        }

        var text = reader.GetString();
        return Guid.TryParse(text, out var id) ? id : Guid.NewGuid();
    }

    private static string ReadBody(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return string.Empty;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return string.Empty;
        }

        return reader.GetString() ?? string.Empty;
    }

    private static Dictionary<string, string> ReadProperties(
        ref Utf8JsonReader reader, JsonSerializerOptions options
    )
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return new Dictionary<string, string>();
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return new Dictionary<string, string>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options)
                ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            // Non-string property values (hand-edited file): keep the body, drop the props
            // of this single entry instead of failing the whole history file.
            return new Dictionary<string, string>();
        }
    }

    private static DateTimeOffset ReadCreatedAt(ref Utf8JsonReader reader, DateTimeOffset fallback)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return fallback;
        }

        try
        {
            return reader.GetDateTimeOffset();
        }
        catch (FormatException)
        {
            return fallback;
        }
    }

    private static MessageHistoryRecord? SkipUnknown(ref Utf8JsonReader reader)
    {
        reader.Skip();
        return null;
    }

    private static void WriteRecord(
        Utf8JsonWriter writer, MessageHistoryRecord record, JsonSerializerOptions options
    )
    {
        writer.WriteStartObject();
        writer.WriteString(nameof(MessageHistoryRecord.Id), record.Id);
        writer.WriteString(nameof(MessageHistoryRecord.Body), record.Body ?? string.Empty);
        writer.WritePropertyName(nameof(MessageHistoryRecord.Properties));
        JsonSerializer.Serialize(writer, record.Properties ?? new Dictionary<string, string>(), options);
        writer.WriteString(nameof(MessageHistoryRecord.CreatedAt), record.CreatedAt);
        writer.WriteEndObject();
    }
}
