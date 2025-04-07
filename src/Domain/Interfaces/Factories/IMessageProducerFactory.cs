using Domain.Interfaces.Providers;

namespace Domain.Interfaces.Factories;

public interface IMessageProducerFactory
{
    IMessageProducerProvider CreateProducer(Guid configId);
}