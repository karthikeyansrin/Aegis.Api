using Microsoft.Extensions.DependencyInjection;
using Aegis.Application.Interfaces;
using Aegis.Application.Services;
using Aegis.Application.Engines;

namespace Aegis.Application.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<SimpleThreatEngine>();
        services.AddSingleton<IThreatEngine, ThreatEngine>();
        services.AddSingleton<IPersonaEngine, PersonaEngine>();
        services.AddSingleton<IIntelligenceEngine, IntelligenceEngine>();
        
        services.AddSingleton<IConversationEngine, ConversationEngine>();
        return services;
    }
}