using Microsoft.Extensions.DependencyInjection;
using Aegis.Application.Interfaces;
using Aegis.Application.Services;
using Aegis.Application.Engines;

namespace Aegis.Application.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SimpleThreatEngine>();
        services.AddScoped<IThreatEngine, ThreatEngine>();
        services.AddScoped<IPersonaEngine, PersonaEngine>();
        services.AddScoped<IIntelligenceEngine, IntelligenceEngine>();
        
        services.AddScoped<IConversationEngine, ConversationEngine>();
        return services;
    }
}