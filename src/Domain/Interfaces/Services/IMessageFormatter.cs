namespace Domain.Interfaces.Services;

public interface IMessageFormatter
{
    string Name { get; }
    
    string Transform(string inputText);
}