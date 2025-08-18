using System.Text.RegularExpressions;
using Domain.Enums;
using Domain.Interfaces.Services;

namespace Application.Services.MessageFormatters;

public partial class DateReplacer : IMessageFormatter
{
    private const string FormatterName = "Date replacer";
    private readonly List<Regex> dateRegexes;
    
    public MessageFormatterType Type => MessageFormatterType.BeforeSend;
    public string Name => FormatterName;

    
    public DateReplacer()
    {
        dateRegexes = 
        [
            DateYyyyRegex(),
            DateMmRegex1(),
            DateDdRegex2(),
        ];
    }
    
    public string Transform(string inputText)
    {
        var result = inputText;
        var newDate = DateTime.Now.ToString("dd.MM.yyyy");
        
        foreach (var regex in dateRegexes)
        {
            result = regex.Replace(result, match =>
            {
                // check if it's a real date
                if (!TryParseDate(match.Value, out DateTime parsedDate)) 
                    return match.Value;
                
                // save original separator
                var separator = match.Value.Contains('.') 
                    ? "." 
                    : match.Value.Contains('/') ? "/" : "-";
                    
                return DateTime.Now.ToString($"yyyy{separator}MM{separator}dd");
            });
        }
        
        return result;
    }
    
    private static bool TryParseDate(string dateString, out DateTime date)
    {
        date = default;
        
        // Различные форматы дат
        string[] formats =
        [
            "dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy",
            "yyyy.MM.dd", "yyyy/MM/dd", "yyyy-MM-dd",
            "MM.dd.yyyy", "MM/dd/yyyy", "MM-dd-yyyy"
        ];
        
        return DateTime.TryParseExact(
            dateString, formats, null, System.Globalization.DateTimeStyles.None, out date
        );
    }

    
    // yyyy.MM.dd, yyyy/MM/dd, yyyy-MM-dd
    [GeneratedRegex(@"\b(\d{4})[\.\/\-](\d{1,2})[\.\/\-](\d{1,2})\b")]
    private static partial Regex DateYyyyRegex();
    
    // MM.dd.yyyy, MM/dd/yyyy, MM-dd-yyyy
    [GeneratedRegex(@"\b(\d{1,2})[\.\/\-](\d{1,2})[\.\/\-](\d{4})\b")]
    private static partial Regex DateMmRegex1();
    
    // dd.MM.yyyy, dd/MM/yyyy, dd-MM-yyyy
    [GeneratedRegex(@"\b(\d{1,2})[\.\/\-](\d{1,2})[\.\/\-](\d{4})\b")]
    private static partial Regex DateDdRegex2();
}