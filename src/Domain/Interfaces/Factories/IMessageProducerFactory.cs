using Domain.Interfaces.Services;

namespace Domain.Interfaces.Factories;

public interface IMessageProducerFactory
{
    IMessageProducerService CreateProducer(Guid configId);
}