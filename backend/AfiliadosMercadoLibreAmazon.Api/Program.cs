using AfiliadosMercadoLibreAmazon.Infrastructure;
using AfiliadosMercadoLibreAmazon.Api.Security;
using AfiliadosMercadoLibreAmazon.Application.Features.Offers;
using AfiliadosMercadoLibreAmazon.Api.Exceptions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.Services.AddScoped<TelegramApiKeyFilter>();
builder.Services.AddSingleton<OfferCaptionBuilder>();

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Global exception handling.
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection();

app.MapControllers();

app.Run();