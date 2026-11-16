
using System.Globalization;
using System.Net;

namespace AfiliadosMercadoLibreAmazon.Application.Features.Offers;

public sealed class OfferCaptionBuilder
{
    public string Build(OfferCaptionInput offer)
    {
        ArgumentNullException.ThrowIfNull(offer);

        if (string.IsNullOrWhiteSpace(offer.Title) || offer.Title.Trim().Length is < 3 or > 200)
        {
            throw new ArgumentException("El título debe tener entre 3 y 200 caracteres.");
        }

        if (offer.Price <= 0)
        {
            throw new ArgumentException("El precio debe ser mayor que cero.");
        }

        if (offer.OriginalPrice.HasValue && offer.OriginalPrice.Value <= offer.Price)
        {
            throw new ArgumentException("El precio original debe ser mayor que el de oferta.");
        }

        if (offer.Description?.Length > 400)
        {
            throw new ArgumentException("La descripción no puede superar 400 caracteres.");
        }

        if (offer.InterestFreeMonths is { } months && (months < 1 || months > 60))
            throw new ArgumentException("Los meses sin intereses deben estar entre 1 y 60.");

        if (offer.MonthlyPayment is { } monthlyPayment && monthlyPayment <= 0)
            throw new ArgumentException("La mensualidad debe ser mayor que cero.");

        if (!offer.InterestFreeMonths.HasValue && (offer.MonthlyPayment.HasValue || offer.InterestFreeMonthsIsUpTo))
            throw new ArgumentException("Para indicar mensualidades debes especificar los meses sin intereses.");

        if (!Uri.TryCreate(offer.AffiliateUrl, UriKind.Absolute, out var affiliateUri) || affiliateUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("El enlace de afiliado debe ser una URL HTTPS válida.");
        }

        var store = offer.Store switch
        {
            "Amazon" => "Amazon",
            "MercadoLibre" => "Mercado Libre",
            _ => throw new ArgumentException(
                "La tienda debe ser Amazon o MercadoLibre.")
        };

        var culture = CultureInfo.GetCultureInfo("es-MX");

        var title = WebUtility.HtmlEncode(offer.Title.Trim());
        var description = WebUtility.HtmlEncode(offer.Description?.Trim() ?? string.Empty);
        var affiliateUrl = WebUtility.HtmlEncode(offer.AffiliateUrl);

        var caption = $"""
                       ⚡ <b>¡OFERTA RELÁMPAGO!</b>

                       🛒 <b>{title}</b>

                       💰 <b>{offer.Price.ToString("C", culture)}</b>
                       """;

        if (offer.OriginalPrice is { } originalPrice)
        {
            var discount = (int)Math.Round((1 - offer.Price / originalPrice) * 100, MidpointRounding.AwayFromZero);

            caption += $"\n🔥 <s>{originalPrice.ToString("C", culture)}</s> | {discount}% de descuento";
        }

        if (offer.InterestFreeMonths is { } interestFreeMonths)
        {
            var prefix = offer.InterestFreeMonthsIsUpTo ? "Hasta " : "";
            caption += $"\n💳 <b>{prefix}{interestFreeMonths} meses sin intereses</b>";
            if (offer.MonthlyPayment is { } payment)
                caption += $"\n💳 {payment.ToString("C", culture)} al mes";
            caption += "\n<i>Sujeto a tarjetas participantes y condiciones de la tienda.</i>";
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            caption += $"\n\n{description}";
        }

        // Enlace de compra con formato HTML.
        caption += $"\n\n🛍️ <a href=\"{affiliateUrl}\"><b>Comprar en {store}</b></a>";

        // Límite conservador, contando también etiquetas HTML.
        if (caption.Length > 1024)
        {
            throw new ArgumentException(
                "El texto de la oferta supera el límite permitido.");
        }

        return caption;
    }
}
