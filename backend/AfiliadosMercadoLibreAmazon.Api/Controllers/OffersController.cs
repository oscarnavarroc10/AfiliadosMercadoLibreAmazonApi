using AfiliadosMercadoLibreAmazon.Api.Security;
using AfiliadosMercadoLibreAmazon.Application.Abstractions.AI;
using AfiliadosMercadoLibreAmazon.Application.Features.Offers;
using Microsoft.AspNetCore.Mvc;

namespace AfiliadosMercadoLibreAmazon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[ServiceFilter(typeof(TelegramApiKeyFilter))]
public sealed class OffersController(IOfferImageAnalyzer imageAnalyzer) : ControllerBase
{
    private const long MaxImageSize = 10 * 1024 * 1024;

    [HttpPost("analyze-image")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(AnalyzedOffer), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AnalyzedOffer>> AnalyzeImage([FromForm] IFormFile? image, CancellationToken cancellationToken)
    {
        if (image is null || image.Length == 0)
        {
            return BadRequest(new
            {
                message = "Debes seleccionar una imagen."
            });
        }

        if (image.Length > MaxImageSize)
        {
            return BadRequest(new
            {
                message = "La imagen no puede superar los 10 MB."
            });
        }

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();

        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            return BadRequest(new
            {
                message = "Solo se permiten imágenes JPG, PNG o WebP."
            });
        }

        await using var stream = image.OpenReadStream();

        var result = await imageAnalyzer.AnalyzeAsync(stream, image.FileName, cancellationToken);

        return Ok(result);
    }
}