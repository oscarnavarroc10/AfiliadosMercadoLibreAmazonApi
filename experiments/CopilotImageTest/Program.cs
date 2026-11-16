
using System.Text.Json;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;

var imagePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "Downloads",
    "oferta.png");

if (!File.Exists(imagePath))
{
    Console.WriteLine($"❌ Imagen no encontrada: {imagePath}");
    return;
}

Console.WriteLine("🚀 Iniciando GitHub Copilot SDK...");
Console.WriteLine("🤖 Modelo: Claude Haiku 5.5");
Console.WriteLine($"📷 Imagen: {imagePath}");

await using var client = new CopilotClient();
await client.StartAsync();

await using var session = await client.CreateSessionAsync(
    new SessionConfig
    {
        Model = "claude-haiku-5.5"
    });

Console.WriteLine("🔍 Analizando producto...");

var response = await session.SendAndWaitAsync(
    new MessageOptions
    {
        Prompt = """
                 Analiza la imagen adjunta de un producto
                 de Mercado Libre o Amazon México.

                 Devuelve exclusivamente un objeto JSON válido.
                 No incluyas Markdown ni explicaciones.

                 Estructura requerida:
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
                 - title: nombre del producto visible.
                 - store: "MercadoLibre" o "Amazon".
                 - price: precio actual como número.
                 - originalPrice: precio anterior, si aparece.
                 - currency: "MXN" cuando sea identificable.
                 - description: descripción breve y objetiva.
                 - affiliateUrl: siempre null.
                 - No inventes información.
                 - Si no puedes leer un dato, utiliza null.
                 - No confundas mensualidades con el precio.
                 - No confundas descuentos de cupones
                   con el precio actual.
                 """,
        Attachments = new List<Attachment>
        {
            new AttachmentFile
            {
                Path = imagePath,
                DisplayName = Path.GetFileName(imagePath)
            }
        }
    },
    timeout: TimeSpan.FromSeconds(120));

var content = response?.Data.Content;

if (string.IsNullOrWhiteSpace(content))
{
    Console.WriteLine("❌ Copilot no devolvió información.");
    return;
}

Console.WriteLine();
Console.WriteLine("📦 Respuesta de Copilot:");

try
{
    using var json = JsonDocument.Parse(content);

    Console.WriteLine(
        JsonSerializer.Serialize(
            json.RootElement,
            new JsonSerializerOptions
            {
                WriteIndented = true
            }));
}
catch (JsonException)
{
    Console.WriteLine("⚠️ La respuesta no es JSON válido:");
    Console.WriteLine(content);
}
