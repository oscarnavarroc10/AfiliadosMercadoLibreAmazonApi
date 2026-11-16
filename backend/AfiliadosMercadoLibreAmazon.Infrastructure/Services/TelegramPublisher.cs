using System.Net.Http.Json;
using System.Text.Json;
using AfiliadosMercadoLibreAmazon.Application.Abstractions.Notifications;
using AfiliadosMercadoLibreAmazon.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AfiliadosMercadoLibreAmazon.Infrastructure.Services;

public sealed class TelegramPublisher(HttpClient httpClient, IOptions<TelegramOptions> options) : ITelegramPublisher
{
    /// <summary>
    /// Publica mensaje de texto en Telegram
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    public async Task PublishMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        using var response = await httpClient.PostAsJsonAsync(
            $"./bot{settings.BotToken}/sendMessage",
            new
            {
                chat_id = settings.ChatId,
                text = message
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
    

    /// <summary>
    ///  Crea la publicación en Telegram soportando URL de imagen
    /// </summary>
    /// <param name="imageUrl"></param>
    /// <param name="caption"></param>
    /// <param name="cancellationToken"></param>
    public async Task PublishOfferAsync(string imageUrl, string caption, string affiliateUrl, string store, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        using var response = await httpClient.PostAsJsonAsync(
            $"./bot{settings.BotToken}/sendPhoto",
            new
            {
                chat_id = settings.ChatId,
                photo = imageUrl,
                caption,
                parse_mode = "HTML",
                reply_markup = BuildPurchaseButton(affiliateUrl, store)
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
    
    /// <summary>
    /// Crea la publicación en Telegram soportando URL de imagen o Archivo de imagen
    /// </summary>
    /// <param name="imageStream"></param>
    /// <param name="fileName"></param>
    /// <param name="caption"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    public async Task PublishOfferAsync(Stream imageStream, string fileName, string caption, string affiliateUrl, string store, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(caption);

        if (!imageStream.CanRead)
        {
            throw new ArgumentException("El Stream de la imagen debe permitir lectura.", nameof(imageStream));
        }

        var settings = options.Value;

        using var form = new MultipartFormDataContent();

        form.Add(new StringContent(settings.ChatId), "chat_id");

        form.Add(new StringContent(caption), "caption");

        form.Add(new StringContent("HTML"), "parse_mode");
        form.Add(new StringContent(JsonSerializer.Serialize(BuildPurchaseButton(affiliateUrl, store))), "reply_markup");

        using var imageContent = new StreamContent(imageStream);

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        var contentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => throw new NotSupportedException(
                $"Formato no soportado para Telegram: {extension}")
        };

        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

        form.Add(imageContent, "photo", fileName);

        using var response = await httpClient.PostAsync($"./bot{settings.BotToken}/sendPhoto", form, cancellationToken);

        response.EnsureSuccessStatusCode();
    }
    private static object BuildPurchaseButton(string affiliateUrl, string store)
    {
        if (!Uri.TryCreate(affiliateUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("El enlace de afiliado debe ser una URL HTTPS válida.", nameof(affiliateUrl));

        var buttonText = store switch
        {
            "Amazon" => "🛒 Comprar en Amazon",
            "MercadoLibre" => "🛒 Comprar en Mercado Libre",
            _ => throw new ArgumentException("Tienda no soportada.", nameof(store))
        };

        return new { inline_keyboard = new[] { new[] { new { text = buttonText, url = affiliateUrl } } } };
    }

}