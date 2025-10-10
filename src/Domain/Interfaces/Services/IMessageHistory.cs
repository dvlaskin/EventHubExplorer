namespace Domain.Interfaces.Services;

public interface IMessageHistory<in TInput, TResult>
{
    Task<TResult> GetHistoryAsync(TInput input);
    Task AddMessageAsync(TInput input, string message);
    Task RemoveMessageAsync(TInput input, string message);
}