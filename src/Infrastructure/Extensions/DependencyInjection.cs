using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Aegis.Application.Interfaces;
using Aegis.Infrastructure.AI;
using Aegis.Infrastructure.Persistence;
using Aegis.Shared.Options;

namespace Aegis.Infrastructure.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAIOptions>(configuration.GetSection(OpenAIOptions.SectionName));
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        services.AddHttpClient<IGroqService, GroqService>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? "https://api.groq.com/openai/" : options.BaseUrl;
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler());

        services.AddSingleton<ConversationStore>(sp => 
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return new ConversationStore(TimeSpan.FromMinutes(options.ExpiryMinutes));
        });

        return services;
    }
}