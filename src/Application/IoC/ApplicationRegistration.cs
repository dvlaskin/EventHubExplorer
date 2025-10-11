using Application.Services;
using Application.Services.MessageFormatters;
using Domain.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.IoC;

public static class ApplicationRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IMessageHistory<Guid, List<string>>, FileBasedMessageHistory>();
        
        services.AddSingleton<IMessageFormatter, JsonFormatter>();
        services.AddSingleton<IMessageFormatter, GuidReplacer>();
        services.AddSingleton<IMessageFormatter, DateReplacer>();
        services.AddSingleton<IMessageFormatter, DateTimeReplacer>();

        services.AddTransient<ITextProcessingPipeline, TextProcessingPipeline>();
        
        
        return services;
    }
}