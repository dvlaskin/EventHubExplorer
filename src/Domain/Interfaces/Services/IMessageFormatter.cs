using Domain.Enums;

namespace Domain.Interfaces.Services;

public interface IMessageFormatter
{
    MessageFormatterType Type { get; }
    
    string Name { get; }
    
    string Transform(string inputText);
}