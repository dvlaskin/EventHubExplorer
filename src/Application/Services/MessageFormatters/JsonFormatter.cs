using System.Text.Json;
using Domain.Interfaces.Services;

namespace Application.Services.MessageFormatters;

public class JsonFormatter : IMessageFormatter
{
    private const string FormatterName = "Json formatter";
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    
    public string Name => FormatterName;

    public string Transform(string inputText)
    {
        if (string.IsNullOrWhiteSpace(inputText))
            return inputText;

        try
        {
            using var document = JsonDocument.Parse(inputText);
            return JsonSerializer.Serialize(document.RootElement, jsonOptions);
        }
        catch (JsonException)
        {
            return inputText;
        }
    }
}