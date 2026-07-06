using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Aegis.Api.Middleware;

namespace Aegis.Api.Extensions;

public static class SecurityExtensions
{
    public static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var apiKey = configuration["AEGIS_API_KEY"] ?? "dev-secret-key";
        services.AddSingleton(new ApiKeyOptions(apiKey));
        
        return services;
    }
}