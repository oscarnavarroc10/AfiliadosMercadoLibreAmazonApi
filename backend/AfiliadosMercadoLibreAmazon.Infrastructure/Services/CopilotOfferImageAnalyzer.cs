
using System.Text.Json;
using AfiliadosMercadoLibreAmazon.Application.Abstractions.AI;
using AfiliadosMercadoLibreAmazon.Application.Features.Offers;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;

namespace AfiliadosMercadoLibreAmazon.Infrastructure.Services;

public sealed class CopilotOfferImageAnalyzer : IOfferImageAnalyzer
{
    private const string ModelId = "claude-haiku-5.5";

    private const int MaxImageBytes = 10 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<AnalyzedOffer> AnalyzeAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        if (!imageStream.CanRead) 
            throw new ArgumentException("No se puede leer la imagen.", nameof(imageStream));

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp"))
            throw new ArgumentException("Solo se permiten imágenes PNG, JPG, JPEG o WebP.");

        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");

        try
        {
            await using (var tempFile = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                var buffer = new byte[81920];
                var totalBytes = 0;

                int bytesRead;

                while ((bytesRead = await imageStream.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
                {
                    totalBytes += bytesRead;

                    if (totalBytes > MaxImageBytes)
                        throw new ArgumentException("La imagen no puede superar los 10 MB.");

                    await tempFile.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                }

                if (totalBytes == 0)
                    throw new ArgumentException("La imagen está vacía.");
            }

            await using var client = new CopilotClient();

            await client.StartAsync();

            await using var session = await client.CreateSessionAsync(
                new SessionConfig
                {
                    Model = ModelId
                });

            var response = await session.SendAndWaitAsync(
                new MessageOptions
                {
                    Prompt = """
                             Analiza exclusivamente la imagen adjunta.

                             Es una captura de un producto de
                             Mercado Libre o Amazon México.

                             Devuelve únicamente un objeto JSON válido.
                             No incluyas Markdown ni explicaciones.

                             Utiliza exactamente estas propiedades:

                             {
                               "title": null,
                               "store": null,
                               "price": null,
                               "originalPrice": null,
                               "currency": null,
                               "description": null,
                               "affiliateUrl": null
                             }

                             Reglas:
                             - title: nombre visible del producto.
                             - store: "MercadoLibre" o "Amazon".
                             - price: precio actual como número decimal.
                             - originalPrice: precio anterior, si aparece.
                             - currency: moneda identificable.
                             - description: descripción breve y objetiva.
                             - affiliateUrl: siempre null.
                             - No inventes información.
                             - Si un dato no es legible, utiliza null.
                             - No confundas mensualidades con precio total.
                             - No confundas cashback o cupones con precio.
                             """,
                    Attachments = new List<Attachment>
                    {
                        new AttachmentFile
                        {
                            Path = tempPath,
                            DisplayName = Path.GetFileName(tempPath)
                        }
                    }
                },
                timeout: TimeSpan.FromSeconds(120),
                cancellationToken: cancellationToken);

            var content = response?.Data.Content;

            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("Copilot no devolvió información.");

            try
            {
                var offer = JsonSerializer.Deserialize<AnalyzedOffer>(content, JsonOptions);

                return offer ?? throw new InvalidOperationException("Copilot devolvió una oferta vacía.");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Copilot devolvió un JSON inválido.", ex);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
