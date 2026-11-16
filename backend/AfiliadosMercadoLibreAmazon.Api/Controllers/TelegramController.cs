using System.Globalization;
using System.Net;
using AfiliadosMercadoLibreAmazon.Api.Models.Enums;
using AfiliadosMercadoLibreAmazon.Api.Models.Requests;
using AfiliadosMercadoLibreAmazon.Application.Abstractions.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace AfiliadosMercadoLibreAmazon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TelegramController(ITelegramPublisher publisher) : ControllerBase
{
    private const long MaxImageSize = 10 * 1024 * 1024;
    
    /// <summary>
    /// Sends a message to a Telegram channel.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("message")]
    public async Task<IActionResult> Test(CancellationToken cancellationToken)
    {
        await publisher.PublishMessageAsync("⚡ ¡Primera publicación desde nuestra API .NET 10!", cancellationToken);

        return Ok(new
        {
            success = true,
            message = "Mensaje enviado a Telegram."
        });
    }
    
    /// <summary>
    /// Envia un mensaje a Telegram con el contenido proporcionado.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("publish")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> PublishOffer([FromForm] PublishOfferRequest request, CancellationToken cancellationToken)
    {
        // 1. Validar título.
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new
            {
                message = "El título de la oferta es obligatorio."
            });
        }

        // 2. Validar tienda.
        var store = request.Store.Value switch
        {
            StoreName.Amazon => "Amazon",
            StoreName.MercadoLibre => "Mercado Libre",
            _ => throw new ArgumentOutOfRangeException()
        };

        // 3. Validar precios.
        if (request.Price <= 0)
        {
            return BadRequest(new
            {
                message = "El precio debe ser mayor que cero."
            });
        }

        if (request.OriginalPrice.HasValue && request.OriginalPrice.Value <= request.Price)
        {
            return BadRequest(new
            {
                message = "OriginalPrice debe ser mayor que Price."
            });
        }

        // 4. Validar enlace de afiliado.
        if (!Uri.TryCreate(request.AffiliateUrl, UriKind.Absolute, out var affiliateUri) || affiliateUri.Scheme != Uri.UriSchemeHttps)
        {
            return BadRequest(new
            {
                message = "AffiliateUrl debe ser una URL HTTPS válida."
            });
        }

        // 5. Validar imagen.
        var hasFile = request.Image is { Length: > 0 };
        var hasUrl = !string.IsNullOrWhiteSpace(request.ImageUrl);

        if (hasFile == hasUrl)
        {
            return BadRequest(new
            {
                message = "Debes proporcionar Image o ImageUrl, pero no ambas."
            });
        }

        if (hasFile)
        {
            if (request.Image!.Length > MaxImageSize)
            {
                return BadRequest(new
                {
                    message = "La imagen no puede superar los 10 MB."
                });
            }

            var extension = Path.GetExtension(request.Image.FileName).ToLowerInvariant();

            if (extension is not (".jpg" or ".jpeg" or ".png"))
            {
                return BadRequest(new
                {
                    message = "Solo se admiten JPG, JPEG y PNG por ahora."
                });
            }
        }

        if (hasUrl && (!Uri.TryCreate(request.ImageUrl, UriKind.Absolute, out var imageUri) || imageUri.Scheme != Uri.UriSchemeHttps))
        {
            return BadRequest(new
            {
                message = "ImageUrl debe ser una URL HTTPS válida."
            });
        }

        // 6. Construir el texto de la oferta.
        var culture = CultureInfo.GetCultureInfo("es-MX");

        var title = WebUtility.HtmlEncode(request.Title.Trim());
        var description = WebUtility.HtmlEncode(request.Description?.Trim() ?? string.Empty);

        var affiliateUrl = WebUtility.HtmlEncode(request.AffiliateUrl);

        var caption = $"""
                       ⚡ <b>¡OFERTA RELÁMPAGO!</b>

                       🛒 <b>{title}</b>

                       💰 <b>{request.Price.ToString("C", culture)}</b>
                       """;

        if (request.OriginalPrice is { } originalPrice)
        {
            var discount = (int)Math.Round((1 - request.Price / originalPrice) * 100, MidpointRounding.AwayFromZero);

            caption += $"\n🔥 <s>{originalPrice.ToString("C", culture)}</s> | {discount}% de descuento";
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            caption += $"\n\n{description}";
        }

        caption += $"\n\n🛍️ <b>Comprar en {store}:</b>\n{affiliateUrl}";

        // Límite conservador de Telegram.
        if (caption.Length > 1024)
        {
            return BadRequest(new
            {
                message = "La publicación supera el límite de texto de Telegram."
            });
        }

        // 7. Publicar la oferta.
        if (hasFile)
        {
            await using var stream = request.Image!.OpenReadStream();

            await publisher.PublishOfferAsync(stream, request.Image.FileName, caption, cancellationToken);
        }
        else
        {
            await publisher.PublishOfferAsync(request.ImageUrl!, caption, cancellationToken);
        }

        return Ok(new
        {
            success = true,
            message = "Oferta publicada correctamente en Telegram."
        });
    }
}