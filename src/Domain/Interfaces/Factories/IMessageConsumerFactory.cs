using Domain.Interfaces.Services;

namespace Domain.Interfaces.Factories;

public interface IMessageConsumerFactory
{
    IMessageConsumerService CreateConsumer(Guid configId);
}