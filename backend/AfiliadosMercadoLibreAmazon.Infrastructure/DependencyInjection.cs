using AfiliadosMercadoLibreAmazon.Application.Abstractions.AI;
using AfiliadosMercadoLibreAmazon.Application.Abstractions.Notifications;
using AfiliadosMercadoLibreAmazon.Infrastructure.Configuration;
using AfiliadosMercadoLibreAmazon.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AfiliadosMercadoLibreAmazon.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection(TelegramOptions.SectionName))
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.BotToken)
                    && !string.IsNullOrWhiteSpace(options.ChatId),
                "Telegram BotToken y ChatId son obligatorios.")
            .ValidateOnStart();

        services.AddHttpClient<ITelegramPublisher, TelegramPublisher>(
            client =>
            {
                client.BaseAddress =
                    new Uri("https://api.telegram.org/");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        // GitHub Copilot SDK - Análisis de imágenes con IA
        services.AddScoped<IOfferImageAnalyzer, CopilotOfferImageAnalyzer>();

        return services;
    }
}