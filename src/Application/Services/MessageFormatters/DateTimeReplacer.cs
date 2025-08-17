using System.Text.RegularExpressions;
using Domain.Interfaces.Services;

namespace Application.Services.MessageFormatters;

public partial class DateTimeReplacer : IMessageFormatter
{
    private const string FormatterName = "Datetime replacer";
    private readonly List<Regex> dateTimeRegexes;
    
    public string Name => FormatterName;
    
    
    public DateTimeReplacer()
    {
        dateTimeRegexes =
        [
            DateTimeRegex(),
            DateTimeRegex1(),
            DateTimeRegex2(),
            DateTimeRegex3(),
            DateTimeRegex4(),
            DateTimeRegex5(),
        ];
    }
    
    public string Transform(string inputText)
    {
        var result = inputText;
        
        foreach (var regex in dateTimeRegexes)
        {
            result = regex.Replace(result, match =>
            {
                if (TryParseDateTime(match.Value, out DateTime parsedDateTime))
                {
                    var currentDateTime = DateTime.Now;
                    
                    // check format of parsed date
                    if (match.Value.Contains('T'))
                    {
                        // ISO формат
                        return currentDateTime.ToString(match.Value.Contains('.') 
                            ? "yyyy-MM-ddTHH:mm:ss.fff" 
                            : "yyyy-MM-ddTHH:mm:ss");
                    }
                    
                    // check date separator
                    var dateSeparator = match.Value.Contains('.') 
                        ? "." 
                        : match.Value.Contains('/') 
                            ? "/" 
                            : "-";
                    
                    // check seconds
                    var timePart = match.Value.Split(' ').LastOrDefault();
                    if (timePart != null && timePart.Split(':').Length == 3)
                    {
                        return currentDateTime.ToString($"dd{dateSeparator}MM{dateSeparator}yyyy HH:mm:ss");
                    }
                    
                    return currentDateTime.ToString($"dd{dateSeparator}MM{dateSeparator}yyyy HH:mm");
                }
                return match.Value;
            });
        }
        
        return result;
    }
    
    private static bool TryParseDateTime(string dateTimeString, out DateTime dateTime)
    {
        dateTime = default;
        
        string[] formats = {
            "dd.MM.yyyy HH:mm:ss", "dd/MM/yyyy HH:mm:ss", "dd-MM-yyyy HH:mm:ss",
            "yyyy.MM.dd HH:mm:ss", "yyyy/MM/dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss",
            "dd.MM.yyyy HH:mm", "dd/MM/yyyy HH:mm", "dd-MM-yyyy HH:mm",
            "yyyy.MM.dd HH:mm", "yyyy/MM/dd HH:mm", "yyyy-MM-dd HH:mm",
            "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.fff",
            "MM/dd/yyyy HH:mm:ss", "MM/dd/yyyy HH:mm"
        };
        
        return DateTime.TryParseExact(
            dateTimeString, formats, null, System.Globalization.DateTimeStyles.None, out dateTime
        );
    }

    
    // dd.MM.yyyy HH:mm:ss
    [GeneratedRegex(@"\b(\d{1,2})[\.\/\-](\d{1,2})[\.\/\-](\d{4})\s+(\d{1,2}):(\d{2}):(\d{2})\b")]
    private static partial Regex DateTimeRegex();
    
    // yyyy.MM.dd HH:mm:ss
    [GeneratedRegex(@"\b(\d{4})[\.\/\-](\d{1,2})[\.\/\-](\d{1,2})\s+(\d{1,2}):(\d{2}):(\d{2})\b")]
    private static partial Regex DateTimeRegex1();
    
    // dd.MM.yyyy HH:mm
    [GeneratedRegex(@"\b(\d{1,2})[\.\/\-](\d{1,2})[\.\/\-](\d{4})\s+(\d{1,2}):(\d{2})\b")]
    private static partial Regex DateTimeRegex2();
    
    // yyyy.MM.dd HH:mm
    [GeneratedRegex(@"\b(\d{4})[\.\/\-](\d{1,2})[\.\/\-](\d{1,2})\s+(\d{1,2}):(\d{2})\b")]
    private static partial Regex DateTimeRegex3();
    
    // ISO формат: yyyy-MM-ddTHH:mm:ss
    [GeneratedRegex(@"\b(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})\b")]
    private static partial Regex DateTimeRegex4();
    
    // ISO формат с миллисекундами: yyyy-MM-ddTHH:mm:ss.fff
    [GeneratedRegex(@"\b(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})\.(\d{3})\b")]
    private static partial Regex DateTimeRegex5();
}