using Domain.Interfaces.Services;

namespace Domain.Models;

public class MessageOptions
{
    public bool UseGzipCompression { get; set; }
    
    public bool UseBase64Coding { get; set; }

    public ITextProcessingPipeline? TextProcessingPipeline { get; set; }
}