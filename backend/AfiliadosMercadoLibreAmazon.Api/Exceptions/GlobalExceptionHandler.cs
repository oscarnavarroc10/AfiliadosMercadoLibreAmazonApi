using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AfiliadosMercadoLibreAmazon.Api.Exceptions;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "Solicitud inválida"),

            HttpRequestException => (StatusCodes.Status502BadGateway, "Error de Telegram"),

            _ => (StatusCodes.Status500InternalServerError, "Error interno")
        };

        if (statusCode >= 500)
        {
            // Evitamos registrar el mensaje completo de HttpRequestException,
            // porque podría contener la URL con el token del bot.
            logger.LogError("Error procesando la solicitud. Tipo: {ExceptionType}", exception.GetType().Name);
        }

        httpContext.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode == 400
                ? exception.Message
                : "No fue posible completar la operación.",
            Instance = httpContext.Request.Path
        };

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}