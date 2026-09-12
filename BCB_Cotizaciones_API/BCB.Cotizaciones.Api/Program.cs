using System.Text.Json;
using BCB.Cotizaciones.Api.Models;
using BCB.Cotizaciones.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BcbOptions>(
    builder.Configuration.GetSection(BcbOptions.SectionName));

builder.Services.AddSingleton<CotizacionService>();

var app = builder.Build();

// El endpoint administrativo usa una API key. No se configura CORS,
// autenticación general, autorización ni HTTPS redirection.
// El HTML de prueba se sirve desde esta misma API para trabajar en el mismo origen.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/cotizacion", (CotizacionService service) =>
{
    return Results.Ok(service.ObtenerCotizacion());
});

app.MapGet("/api/configuracion", (CotizacionService service) =>
{
    return Results.Ok(new
    {
        tipoCambioInicial = service.TipoCambioBase,
        actualizacion = "manual",
        precisionDecimales = 4
    });
});

app.MapPut("/api/admin/cotizacion", (
    ActualizarCotizacionRequest solicitud,
    HttpContext context,
    CotizacionService service) =>
{
    if (!context.Request.Headers.TryGetValue("X-Api-Key", out var clave) ||
        !service.EsClaveAdministracionValida(clave))
    {
        return Results.Unauthorized();
    }

    try
    {
        service.ActualizarCotizacion(solicitud.ValorBolivianoPorDolar);
        return Results.Ok(service.ObtenerCotizacion());
    }
    catch (ArgumentOutOfRangeException)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["valorBolivianoPorDolar"] = ["El valor debe ser mayor que 0."]
        });
    }
});

// Server-Sent Events (SSE): notifica una nueva cotización tras cada
// actualización manual autorizada.
app.MapGet("/api/cotizacion/stream", async (HttpContext context, CotizacionService service) =>
{
    context.Response.Headers["Cache-Control"] = "no-cache";
    context.Response.Headers["Connection"] = "keep-alive";
    context.Response.ContentType = "text/event-stream";

    var cancellationToken = context.RequestAborted;
    var actual = service.ObtenerCotizacion();

    await EnviarEventoAsync(context, actual, cancellationToken);
    var versionConocida = actual.Version;

    try
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await service.EsperarActualizacionAsync(versionConocida, cancellationToken);

            actual = service.ObtenerCotizacion();
            versionConocida = actual.Version;

            await EnviarEventoAsync(context, actual, cancellationToken);
        }
    }
    catch (OperationCanceledException)
    {
        // El navegador/cliente cerró la conexión. No requiere acción adicional.
    }
});

app.MapGet("/api", () => Results.Ok(new
{
    servicio = "API académica de cotización USD/BOB - Banco Central de Bolivia",
    endpoints = new[]
    {
        "GET /api/cotizacion",
        "GET /api/cotizacion/stream",
        "GET /api/configuracion",
        "PUT /api/admin/cotizacion (requiere X-Api-Key)"
    },
    pruebaHtml = "/"
}));

app.Run();

static async Task EnviarEventoAsync(
    HttpContext context,
    CotizacionResponse cotizacion,
    CancellationToken cancellationToken)
{
    var json = JsonSerializer.Serialize(cotizacion, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    await context.Response.WriteAsync("event: cotizacion\n", cancellationToken);
    await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
    await context.Response.Body.FlushAsync(cancellationToken);
}
