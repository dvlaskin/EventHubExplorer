using Domain.Interfaces.Services;

namespace Domain.Interfaces.Factories;

public interface IMessageProducerFactory
{
    IMessageProducerService CreateProducerService(Guid configId);
}