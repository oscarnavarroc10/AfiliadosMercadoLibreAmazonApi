namespace AfiliadosMercadoLibreAmazon.Application.Abstractions.Notifications;

public interface ITelegramPublisher
{
    Task PublishMessageAsync(string message, CancellationToken cancellationToken = default);
    Task PublishOfferAsync(string imageUrl, string message, string affiliateUrl, string store, CancellationToken cancellationToken = default);
    Task PublishOfferAsync(Stream imageStream, string fileName, string message, string affiliateUrl, string store, CancellationToken cancellationToken = default);
}