using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Aegis.Application.Interfaces;
using Aegis.Infrastructure.AI;
using Aegis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Aegis.Shared.Options;

namespace Aegis.Infrastructure.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAIOptions>(configuration.GetSection(OpenAIOptions.SectionName));
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        // Register Groq HTTP client — typed to GroqProvider
        services.AddHttpClient<GroqProvider>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? "https://api.groq.com/openai/" : options.BaseUrl;
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler());

        // Register concrete providers
        services.AddSingleton<OpenAIProvider>();

        // Register ProviderFactory which aggregates all ILLMProvider instances
        services.AddSingleton<ProviderFactory>(sp =>
        {
            var groq = sp.GetRequiredService<GroqProvider>();
            var openai = sp.GetRequiredService<OpenAIProvider>();
            var opts = sp.GetRequiredService<IOptions<OpenAIOptions>>();
            return new ProviderFactory(new ILLMProvider[] { groq, openai }, opts);
        });

        // Expose ILLMProvider — resolved from the factory using DefaultProvider setting
        services.AddSingleton<ILLMProvider>(sp =>
            sp.GetRequiredService<ProviderFactory>().GetDefault());

        services.AddSingleton<IConversationStore, ConversationStore>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return new ConversationStore(TimeSpan.FromMinutes(options.ExpiryMinutes));
        });

                services.AddDbContext<AegisDbContext>(options =>
            options.UseNpgsql(configuration.GetSection(DatabaseOptions.SectionName)["ConnectionString"]));

        services.AddScoped<IConversationRepository, ConversationRepository>();
        return services;
    }
}