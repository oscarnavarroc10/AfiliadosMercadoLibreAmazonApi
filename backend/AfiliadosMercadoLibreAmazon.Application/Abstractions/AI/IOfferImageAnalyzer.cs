using AfiliadosMercadoLibreAmazon.Application.Features.Offers;

namespace AfiliadosMercadoLibreAmazon.Application.Abstractions.AI;

public interface IOfferImageAnalyzer
{
    /// <summary>
    /// Analiza una imagen de un producto y extrae información relevante.
    /// </summary>
    /// <param name="imageStream"></param>
    /// <param name="fileName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AnalyzedOffer> AnalyzeAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default);
}