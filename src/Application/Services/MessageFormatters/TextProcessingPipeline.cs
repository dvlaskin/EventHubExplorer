using Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Application.Services.MessageFormatters;

public class TextProcessingPipeline
{
    private readonly ILogger<TextProcessingPipeline> logger;
    private readonly List<IMessageFormatter> formatters = [];

    public TextProcessingPipeline(ILogger<TextProcessingPipeline> logger)
    {
        this.logger = logger;
    }
    
    
    public TextProcessingPipeline AddFormatter(IMessageFormatter formatter)
    {
        formatters.Add(formatter);
        return this;
    }
    
    public string Process(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
            
        var result = input;
        
        foreach (var formatter in formatters)
        {
            try
            {
                result = formatter.Transform(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while processing text: {Message}", ex.Message);
            }
        }
        
        return result;
    }
}