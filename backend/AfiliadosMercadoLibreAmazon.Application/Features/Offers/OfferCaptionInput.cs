namespace AfiliadosMercadoLibreAmazon.Application.Features.Offers;

/// <summary>
/// Representa los datos requeridos de la oferta
/// </summary>
/// <param name="Title"></param>
/// <param name="Store"></param>
/// <param name="Price"></param>
/// <param name="OriginalPrice"></param>
/// <param name="AffiliateUrl"></param>
/// <param name="Description"></param>
public sealed record OfferCaptionInput(
    string Title,
    string Store,
    decimal Price,
    decimal? OriginalPrice,
    string AffiliateUrl,
    string? Description);