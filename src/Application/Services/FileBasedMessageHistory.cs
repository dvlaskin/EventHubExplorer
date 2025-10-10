using Domain.Interfaces.Providers;
using Domain.Interfaces.Services;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class FileBasedMessageHistory : IMessageHistory<Guid, HashSet<string>>
{
    private readonly ILogger<FileBasedMessageHistory> logger;
    private readonly IFileStorageProvider<MessagesHistory> messagesStorageProvider;


    public FileBasedMessageHistory(
        ILogger<FileBasedMessageHistory> logger, IFileStorageProvider<MessagesHistory> messagesStorageProvider
    )
    {
        this.logger = logger;
        this.messagesStorageProvider = messagesStorageProvider;
    }
    
    
    public async Task<HashSet<string>> GetHistoryAsync(Guid input)
    {
        logger.LogInformation("Getting message history for event hub config {Input}", input);
        MessagesHistory? fullHistory = await messagesStorageProvider.GetDataAsync();
        
        if (fullHistory is not null && fullHistory.Messages.TryGetValue(input, out var history))
        {
            logger.LogInformation("History for {Input} found, {CountItems}", input, history.Count);
            return history;
        }
        
        logger.LogInformation("History for {Input} not found", input);
        return [];
    }

    public async Task AddMessageAsync(Guid input, string message)
    {
        MessagesHistory? fullHistory = await messagesStorageProvider.GetDataAsync();
        
        if (fullHistory is null)
        {
            fullHistory = new MessagesHistory
            {
                Messages = new Dictionary<Guid, HashSet<string>>
                {
                    { input, [message] }
                }
            };
        }
        else if (fullHistory.Messages.TryGetValue(input, out var history))
        {
            if (!history.Add(message))
                return;
        }
        else
        {
            fullHistory.Messages.Add(input, [message]);
        }

        await messagesStorageProvider.SaveDataAsync(fullHistory);
    }

    public async Task RemoveMessageAsync(Guid input, string message)
    {
        MessagesHistory? fullHistory = await messagesStorageProvider.GetDataAsync();
        
        if (fullHistory is null)
            return;
        
        if (fullHistory.Messages.TryGetValue(input, out var history))
        {
            history.Remove(message);
            await messagesStorageProvider.SaveDataAsync(fullHistory);
        }
    }
}