using Domain.Interfaces.Providers;

namespace Domain.Interfaces.Factories;

public interface IMessageConsumerFactory
{
    IMessageConsumerProvider CreateConsumer(Guid configId);
}