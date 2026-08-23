namespace Domain.Models;

public sealed class MessagesHistory
{
    public Dictionary<Guid, List<string>> Messages { get; set; } = new();
}