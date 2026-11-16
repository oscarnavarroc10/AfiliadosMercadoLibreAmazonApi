using System.Text.Json;
using AfiliadosMercadoLibreAmazon.Api.Models.Enums;
using AfiliadosMercadoLibreAmazon.Api.Security;
using AfiliadosMercadoLibreAmazon.Application.Abstractions.AI;
using AfiliadosMercadoLibreAmazon.Application.Abstractions.Notifications;
using AfiliadosMercadoLibreAmazon.Application.Features.Offers;
using Microsoft.AspNetCore.Mvc;

namespace AfiliadosMercadoLibreAmazon.Api.Controllers;

[ApiController]
[Route("api/batch")]
[ServiceFilter(typeof(TelegramApiKeyFilter))]
public sealed class BatchController(IOfferImageAnalyzer analyzer, ITelegramPublisher publisher, OfferCaptionBuilder captionBuilder) : ControllerBase
{
    private const int MaxItems = 5;
    private const long MaxFileBytes = 10 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public sealed record BatchItemResult(int Index, string Status, string? Error = null, AnalyzedOffer? Offer = null);
    public sealed record BatchOffer(string Title, string Store, decimal Price, decimal? OriginalPrice,
        string AffiliateUrl, string? Description, int? InterestFreeMonths, decimal? MonthlyPayment,
        bool InterestFreeMonthsIsUpTo);

    [HttpPost("analyze")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(55 * 1024 * 1024)]
    public async Task<IActionResult> Analyze([FromForm] List<IFormFile> images, CancellationToken ct)
    {
        if (images.Count is < 1 or > MaxItems)
            return BadRequest(new { message = "Selecciona entre 1 y 5 imágenes." });

        var results = new List<BatchItemResult>();
        for (var i = 0; i < images.Count; i++)
        {
            try
            {
                var file = images[i];
                await ValidateImage(file, ct);
                await using var stream = file.OpenReadStream();
                var offer = await analyzer.AnalyzeAsync(stream, file.FileName, ct);
                results.Add(new(i, "analyzed", Offer: offer));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                results.Add(new(i, "failed", $"No se pudo analizar: {SafeError(ex)}"));
            }
        }
        return Ok(new { results });
    }

    [HttpPost("publish")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(55 * 1024 * 1024)]
    public async Task<IActionResult> Publish([FromForm] List<IFormFile> images, [FromForm] string? offersJson, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(offersJson)) return BadRequest(new { message = "Falta offersJson." });
        List<BatchOffer>? offers;
        try { offers = JsonSerializer.Deserialize<List<BatchOffer>>(offersJson, JsonOptions); }
        catch (JsonException) { return BadRequest(new { message = "offersJson no es JSON válido." }); }
        if (offers is null || offers.Count is < 1 or > MaxItems || offers.Count != images.Count)
            return BadRequest(new { message = "Debes enviar entre 1 y 5 ofertas, cada una con su imagen en el mismo orden." });

        var results = new List<BatchItemResult>();
        for (var i = 0; i < offers.Count; i++)
        {
            try
            {
                var offer = offers[i];
                var file = images[i];
                await ValidateImage(file, ct);
                if (file.Name != "images") throw new ArgumentException("Campo de imagen incorrecto.");
                if (offer.Store is not ("Amazon" or "MercadoLibre")) throw new ArgumentException("Tienda inválida.");
                if (offer.Title?.Length is < 3 or > 200) throw new ArgumentException("El título debe tener entre 3 y 200 caracteres.");
                if (offer.Description?.Length > 400) throw new ArgumentException("Descripción: máximo 400 caracteres.");
                if (offer.Price <= 0) throw new ArgumentException("El precio debe ser mayor a cero.");
                if (offer.InterestFreeMonths is < 1 or > 60) throw new ArgumentException("MSI: entre 1 y 60 meses.");
                var caption = captionBuilder.Build(new OfferCaptionInput(offer.Title, offer.Store, offer.Price,
                    offer.OriginalPrice, offer.AffiliateUrl, offer.Description, offer.InterestFreeMonths,
                    offer.MonthlyPayment, offer.InterestFreeMonthsIsUpTo));
                await using var stream = file.OpenReadStream();
                await publisher.PublishOfferAsync(stream, file.FileName, caption, offer.AffiliateUrl, offer.Store, ct);
                results.Add(new(i, "published"));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (ArgumentException ex) { results.Add(new(i, "failed", ex.Message)); }
            catch (Exception) { results.Add(new(i, "uncertain", "No se pudo confirmar la publicación. Revisa el canal antes de reintentar para evitar duplicados.")); }
        }
        return Ok(new { results });
    }

    private static async Task ValidateImage(IFormFile file, CancellationToken ct)
    {
        if (file.Length is <= 0 or > MaxFileBytes) throw new ArgumentException("Imagen vacía o mayor de 10 MB.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg"))
            throw new ArgumentException("Telegram requiere imágenes PNG o JPG (no WebP).");
        await using var stream = file.OpenReadStream();
        var header = new byte[8];
        var read = await stream.ReadAsync(header.AsMemory(), ct);
        var valid = ext == ".png"
            ? read == 8 && header.AsSpan().SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            : read >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255;
        if (!valid) throw new ArgumentException("La imagen no corresponde a su extensión (JPG/PNG).");
    }

    private static string SafeError(Exception ex) => ex is ArgumentException ? ex.Message : "Error de análisis; completa los campos manualmente.";
}
