using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Aegis.Application.Interfaces;
using Aegis.Application.Services;
using Aegis.Application.Engines;
using Aegis.Shared.Options;
using Aegis.Infrastructure.Policies;
using Aegis.Infrastructure.Personas;

namespace Aegis.Application.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DecisionEngineOptions>(configuration.GetSection(DecisionEngineOptions.SectionName));

        services.AddScoped<SimpleThreatEngine>();
        services.AddScoped<IThreatEngine, ThreatEngine>();
        services.AddScoped<IPersonaEngine, PersonaEngine>();
        services.AddScoped<IThreatIndicatorEngine, ThreatIndicatorEngine>();

        // Global cross-session intelligence store (Singleton — holds the shared registry)
        services.AddSingleton<IThreatIntelligenceEngine, ThreatIntelligenceEngine>();

        // Persona repository (in-memory default)
        services.AddSingleton<IPersonaRepository, InMemoryPersonaRepository>();

        // Decision pipeline: PolicyEngine → DecisionEngine (fallback)
        services.AddSingleton<IPolicyRepository, InMemoryPolicyRepository>();
        services.AddScoped<IDecisionEngine, DecisionEngine>();
        services.AddScoped<IPolicyEngine, PolicyEngine>();

        services.AddScoped<IConversationEngine, ConversationEngine>();
        return services;
    }
}