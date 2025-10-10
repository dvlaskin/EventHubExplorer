namespace Domain.Models;

public class MessagesHistory
{
    public Dictionary<Guid, HashSet<string>> Messages { get; set; } = new();
}