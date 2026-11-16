using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AfiliadosMercadoLibreAmazon.Api.Security;

public sealed class TelegramApiKeyFilter(
    IConfiguration configuration) : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var expectedKey = configuration["Security:ApiKey"];

        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            context.Result = new ObjectResult(new
            {
                message = "La seguridad de la API no está configurada."
            })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };

            return Task.CompletedTask;
        }

        var providedKey = context.HttpContext.Request.Headers["X-API-Key"].ToString();

        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);

        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                message = "API Key inválida o ausente."
            });
        }

        return Task.CompletedTask;
    }
}