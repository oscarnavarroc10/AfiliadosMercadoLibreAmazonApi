
using AfiliadosMercadoLibreAmazon.Api.Models.Requests;
using AfiliadosMercadoLibreAmazon.Api.Security;
using AfiliadosMercadoLibreAmazon.Application.Abstractions.Notifications;
using AfiliadosMercadoLibreAmazon.Application.Features.Offers;
using Microsoft.AspNetCore.Mvc;

namespace AfiliadosMercadoLibreAmazon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[ServiceFilter(typeof(TelegramApiKeyFilter))]
public sealed class TelegramController(ITelegramPublisher publisher, OfferCaptionBuilder captionBuilder) : ControllerBase
{
    private const long MaxImageSize = 10 * 1024 * 1024;

    [HttpPost("text-message")]
    public async Task<IActionResult> TextMessage(
        CancellationToken cancellationToken)
    {
        await publisher.PublishMessageAsync(
            "🚀 ¡Mensaje desde nuestra API .NET 10!",
            cancellationToken);

        return Ok(new
        {
            success = true,
            message = "Mensaje enviado a Telegram."
        });
    }

    [HttpPost("publish")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> PublishOffer([FromForm] PublishOfferRequest request, CancellationToken cancellationToken)
    {
        // Validar enum.
        if (!request.Store.HasValue || !Enum.IsDefined(request.Store.Value))
        {
            return BadRequest(new
            {
                message = "Store debe ser Amazon o MercadoLibre."
            });
        }

        // Exigir exactamente una fuente de imagen.
        var hasFile = request.Image is { Length: > 0 };
        var hasUrl = !string.IsNullOrWhiteSpace(request.ImageUrl);

        if (hasFile == hasUrl)
        {
            return BadRequest(new
            {
                message = "Debes enviar Image o ImageUrl, pero no ambas."
            });
        }

        if (hasFile && request.Image!.Length > MaxImageSize)
        {
            return BadRequest(new
            {
                message = "La imagen no puede superar los 10 MB."
            });
        }

        if (hasUrl && (!Uri.TryCreate(request.ImageUrl, UriKind.Absolute, out var imageUri) || imageUri.Scheme != Uri.UriSchemeHttps))
        {
            return BadRequest(new
            {
                message = "ImageUrl debe ser una URL HTTPS válida."
            });
        }

        // Construir el caption en Application.
        var caption = captionBuilder.Build(
            new OfferCaptionInput(
                request.Title,
                request.Store.Value.ToString(),
                request.Price,
                request.OriginalPrice,
                request.AffiliateUrl,
                request.Description,
                request.InterestFreeMonths,
                request.MonthlyPayment,
                request.InterestFreeMonthsIsUpTo));

        //Si viene la imagen en el request
        if (hasFile)
        {
            var file = request.Image!;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (extension is not (".jpg" or ".jpeg" or ".png"))
            {
                return BadRequest(new
                {
                    message = "Los archivos deben ser JPG, JPEG o PNG."
                });
            }

            await using var stream = file.OpenReadStream();

            // Validar la firma real de la imagen.
            var header = new byte[8];
            var bytesRead = await stream.ReadAsync(header.AsMemory(), cancellationToken);

            var isJpeg = bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

            var isPng = bytesRead == 8 && header.AsSpan().SequenceEqual(
                new byte[]
                {
                    0x89, 0x50, 0x4E, 0x47,
                    0x0D, 0x0A, 0x1A, 0x0A
                });

            var validImage = extension switch
            {
                ".jpg" or ".jpeg" => isJpeg,
                ".png" => isPng,
                _ => false
            };

            if (!validImage)
            {
                return BadRequest(new
                {
                    message = "El contenido no corresponde a una imagen válida."
                });
            }

            // Regresar al inicio para enviar el archivo completo.
            if (!stream.CanSeek)
            {
                return BadRequest(new
                {
                    message = "No se pudo procesar el archivo."
                });
            }

            stream.Position = 0;

            await publisher.PublishOfferAsync(stream, file.FileName, caption, request.AffiliateUrl, request.Store.Value.ToString(), cancellationToken);
        }
        //Si viene la imagen solo en URL
        else
        {
            await publisher.PublishOfferAsync(request.ImageUrl!, caption, request.AffiliateUrl, request.Store.Value.ToString(), cancellationToken);
        }

        return Ok(new
        {
            success = true,
            message = "Oferta publicada correctamente en Telegram."
        });
    }
}
