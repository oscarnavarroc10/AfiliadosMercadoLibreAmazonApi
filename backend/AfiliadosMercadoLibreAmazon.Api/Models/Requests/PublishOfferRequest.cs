using System.ComponentModel.DataAnnotations;
using AfiliadosMercadoLibreAmazon.Api.Models.Enums;

namespace AfiliadosMercadoLibreAmazon.Api.Models.Requests;

public sealed class PublishOfferRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public StoreName? Store { get; set; } 

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal Price { get; set; }

    public decimal? OriginalPrice { get; set; }

    [Required]
    [Url]
    public string AffiliateUrl { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? Description { get; set; }

    [Range(1, 60)]
    public int? InterestFreeMonths { get; set; }

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal? MonthlyPayment { get; set; }

    public bool InterestFreeMonthsIsUpTo { get; set; }

    public IFormFile? Image { get; set; }

    public string? ImageUrl { get; set; }
}