using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Aegis.Application.Interfaces;
using Aegis.Infrastructure.AI;
using Aegis.Infrastructure.Persistence;

namespace Aegis.Infrastructure.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var groqBase = configuration["GROQ_BASE_URL"] ?? "https://api.groq.com/openai/";
        var groqApiKey = configuration["GROQ_API_KEY"];

        if (string.IsNullOrWhiteSpace(groqApiKey))
        {
            throw new InvalidOperationException("GROQ_API_KEY is not configured");
        }

        services.AddHttpClient<IGroqService, GroqService>(client =>
        {
            client.BaseAddress = new Uri(groqBase);
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler());

        services.AddSingleton(new ConversationStore(TimeSpan.FromMinutes(45)));

        return services;
    }
}