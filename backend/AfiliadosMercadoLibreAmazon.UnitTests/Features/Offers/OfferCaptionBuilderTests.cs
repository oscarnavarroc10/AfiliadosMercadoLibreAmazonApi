
using AfiliadosMercadoLibreAmazon.Application.Features.Offers;
using Xunit;

namespace AfiliadosMercadoLibreAmazon.UnitTests.Features.Offers;

public sealed class OfferCaptionBuilderTests
{
    private readonly OfferCaptionBuilder _builder = new();

    [Fact]
    public void Build_WhenOriginalPriceIsLowerThanPrice_ThrowsArgumentException()
    {
        // Arrange
        var offer = CreateValidOffer() with
        {
            Price = 593,
            OriginalPrice = 100
        };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => _builder.Build(offer));

        // Assert
        Assert.Equal("El precio original debe ser mayor que el de oferta.", exception.Message);
    }

    [Fact]
    public void Build_WhenPriceIsZero_ThrowsArgumentException()
    {
        // Arrange
        var offer = CreateValidOffer() with
        {
            Price = 0
        };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => _builder.Build(offer));

        // Assert
        Assert.Equal("El precio debe ser mayor que cero.", exception.Message);
    }

    [Fact]
    public void Build_WhenAffiliateUrlIsHttp_ThrowsArgumentException()
    {
        // Arrange
        var offer = CreateValidOffer() with
        {
            AffiliateUrl = "http://meli.la/1Da8xn2"
        };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => _builder.Build(offer));

        // Assert
        Assert.Equal("El enlace de afiliado debe ser una URL HTTPS válida.", exception.Message);
    }

    [Fact]
    public void Build_WhenOfferHasDiscount_Includes54PercentDiscount()
    {
        // Arrange
        var offer = CreateValidOffer() with
        {
            Price = 593,
            OriginalPrice = 1299
        };

        // Act
        var caption = _builder.Build(offer);

        // Assert
        Assert.Contains("54% de descuento", caption);
    }

    [Fact]
    public void Build_WhenOfferIsValid_IncludesPurchaseLink()
    {
        // Arrange
        var offer = CreateValidOffer();

        // Act
        var caption = _builder.Build(offer);

        // Assert
        Assert.Contains("https://meli.la/1Da8xn2", caption);

        Assert.Contains("Comprar en Mercado Libre", caption);

        Assert.Contains("Perfume Rasasi", caption);
    }

    [Fact]
    public void Build_WithAmazonLink_ShowsFullVisibleUrl()
    {
        var offer = CreateValidOffer() with { Store = "Amazon", AffiliateUrl = "https://amzn.to/abc123" };
        var caption = _builder.Build(offer);
        Assert.Contains("Comprar en Amazon:", caption);
        Assert.Contains("https://amzn.to/abc123", caption);
    }

    [Fact]
    public void Build_WhenTitleContainsHtml_EncodesSpecialCharacters()
    {
        // Arrange
        var offer = CreateValidOffer() with
        {
            Title = "<script>alert('test')</script>"
        };

        // Act
        var caption = _builder.Build(offer);

        // Assert
        Assert.DoesNotContain("<script>", caption);
        Assert.Contains("&lt;script&gt;", caption);
    }

    [Fact]
    public void Build_WithMsi_IncludesInstallmentsAndPayment()
    {
        var offer = CreateValidOffer() with { InterestFreeMonths = 12, MonthlyPayment = 50m, InterestFreeMonthsIsUpTo = true };
        var caption = _builder.Build(offer);
        Assert.Contains("Hasta 12 meses sin intereses", caption);
        Assert.Contains("$50.00", caption);
        Assert.Contains("Sujeto a tarjetas participantes", caption);
    }

    [Fact]
    public void Build_WithoutMsi_DoesNotMentionInstallments()
    {
        Assert.DoesNotContain("meses sin intereses", _builder.Build(CreateValidOffer()));
    }

    [Fact]
    public void Build_WithMonthlyPaymentButNoMsi_Throws()
    {
        Assert.Throws<ArgumentException>(() => _builder.Build(CreateValidOffer() with { MonthlyPayment = 100m }));
    }

    private static OfferCaptionInput CreateValidOffer()
    {
        return new OfferCaptionInput(
            Title: "Perfume Rasasi Hawas Kobra",
            Store: "MercadoLibre",
            Price: 593,
            OriginalPrice: 1299,
            AffiliateUrl: "https://meli.la/1Da8xn2",
            Description: "Perfume de 100ml");
    }
}
