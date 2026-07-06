using Microsoft.Extensions.DependencyInjection;
using Aegis.Application.Interfaces;
using Aegis.Application.Services;

namespace Aegis.Application.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<SimpleScamAnalysisService>();
        services.AddSingleton<IScamAnalysisService, ScamDetectionService>();
        services.AddSingleton<HoneypotAgentService>();
        services.AddSingleton<IntelligenceExtractionService>();
        
        return services;
    }
}