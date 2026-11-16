namespace AfiliadosMercadoLibreAmazon.Application.Features.Offers;

/// <summary>
/// Representa una oferta analizad con IA a partir de una imagen.
/// </summary>
/// <param name="Title"></param>
/// <param name="Store"></param>
/// <param name="Price"></param>
/// <param name="OriginalPrice"></param>
/// <param name="Currency"></param>
/// <param name="Description"></param>
/// <param name="AffiliateUrl"></param>
public sealed record AnalyzedOffer(
    string? Title,
    string? Store,
    decimal? Price,
    decimal? OriginalPrice,
    string? Currency,
    string? Description,
    string? AffiliateUrl,
    int? InterestFreeMonths = null,
    decimal? MonthlyPayment = null,
    bool InterestFreeMonthsIsUpTo = false);