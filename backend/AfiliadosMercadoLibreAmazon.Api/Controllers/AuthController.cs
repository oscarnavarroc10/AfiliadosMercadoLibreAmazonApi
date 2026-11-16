using AfiliadosMercadoLibreAmazon.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace AfiliadosMercadoLibreAmazon.Api.Controllers;

/// <summary>Lightweight credential check; never consumes AI credits or publishes anything.</summary>
[ApiController]
[Route("api/auth")]
[ServiceFilter(typeof(TelegramApiKeyFilter))]
public sealed class AuthController : ControllerBase
{
    [HttpGet("validate")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Validate() => Ok(new { valid = true });
}
