namespace Domain.Interfaces;

public interface IFormattingConfig
{
    bool UseGzipCompression { get; set; }
    bool UseBase64Coding { get; set; }
    Dictionary<string, bool> MessageFormatters { get; set; }
}
