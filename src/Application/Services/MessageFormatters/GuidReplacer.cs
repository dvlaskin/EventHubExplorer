using System.Text.RegularExpressions;
using Domain.Enums;
using Domain.Interfaces.Services;

namespace Application.Services.MessageFormatters;

public partial class GuidReplacer : IMessageFormatter
{
    private const string FormatterName = "Guid replacer";
    private readonly Regex guidRegex = GuidTextRegex();

    public MessageFormatterType Type => MessageFormatterType.BeforeSend;
    public string Name => FormatterName;
    
    public string Transform(string inputText)
    {
        return guidRegex.Replace(inputText, match => Guid.NewGuid().ToString());
    }

    
    [GeneratedRegex(@"\b[A-Fa-f0-9]{8}-?[A-Fa-f0-9]{4}-?[A-Fa-f0-9]{4}-?[A-Fa-f0-9]{4}-?[A-Fa-f0-9]{12}\b")]
    private static partial Regex GuidTextRegex();
}