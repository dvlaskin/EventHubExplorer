namespace Domain.Models;

public class MessagesHistory
{
    public Dictionary<Guid, List<string>> Messages { get; set; } = new();
}