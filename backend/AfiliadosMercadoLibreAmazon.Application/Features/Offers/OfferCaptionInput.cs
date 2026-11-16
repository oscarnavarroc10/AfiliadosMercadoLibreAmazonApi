namespace AfiliadosMercadoLibreAmazon.Application.Features.Offers;

public sealed record OfferCaptionInput(
    string Title,
    string Store,
    decimal Price,
    decimal? OriginalPrice,
    string AffiliateUrl,
    string? Description);