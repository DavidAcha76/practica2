namespace BCB.Cotizaciones.Api.Models;

public sealed record CotizacionResponse(
    string Fuente,
    string MonedaBase,
    string MonedaCotizada,
    decimal ValorBolivianoPorDolar,
    DateTimeOffset FechaActualizacion,
    long Version);
