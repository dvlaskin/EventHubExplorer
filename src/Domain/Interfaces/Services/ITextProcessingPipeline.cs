namespace Domain.Interfaces.Services;

public interface ITextProcessingPipeline
{
    void AddFormatters(IReadOnlyList<IMessageFormatter> messageFormatters);
    string Process(string input);
}