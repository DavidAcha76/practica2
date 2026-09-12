using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Asfi.Main.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Asfi.Main.Api.Services;

public sealed record QuoteSnapshot(decimal Rate, DateTime CapturedAtUtc, string RawJson);

public sealed class QuoteClient(HttpClient http, IOptions<BcbOptions> options)
{
    private readonly BcbOptions _options = options.Value;

    public async Task<QuoteSnapshot> GetAsync(CancellationToken ct)
    {
        var url = new Uri(new Uri(_options.BaseUrl.TrimEnd('/') + "/"), _options.Endpoint.TrimStart('/'));
        using var res = await http.GetAsync(url, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(json);
        if (!TryFindRate(doc.RootElement, out var rate))
            throw new InvalidDataException($"El BCB respondió, pero no se encontró una cotización numérica válida. Respuesta: {json[..Math.Min(json.Length, 500)]}");
        return new QuoteSnapshot(decimal.Round(rate, 4, MidpointRounding.AwayFromZero), DateTime.UtcNow, json);
    }

    private static bool TryFindRate(JsonElement root, out decimal rate)
    {
        string[] priority = ["valorBolivianoPorDolar", "cotizacion", "tipocambio", "tipo_cambio", "rate", "valor", "oficial", "venta", "compra"];
        foreach (var key in priority)
            if (TryPropertyRecursive(root, key, out var el) && TryDecimal(el, out rate) && rate is > 1m and < 20m) return true;
        return TryAnyPlausible(root, out rate);
    }

    private static bool TryPropertyRecursive(JsonElement root, string wanted, out JsonElement found)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in root.EnumerateObject())
            {
                var normalized = p.Name.Replace("_", "").Replace("-", "").ToLowerInvariant();
                if (normalized == wanted.Replace("_", "").Replace("-", "").ToLowerInvariant()) { found = p.Value; return true; }
                if (TryPropertyRecursive(p.Value, wanted, out found)) return true;
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
            foreach (var e in root.EnumerateArray()) if (TryPropertyRecursive(e, wanted, out found)) return true;
        found = default; return false;
    }

    private static bool TryAnyPlausible(JsonElement root, out decimal rate)
    {
        if (TryDecimal(root, out rate) && rate is > 1m and < 20m) return true;
        if (root.ValueKind == JsonValueKind.Object) foreach (var p in root.EnumerateObject()) if (TryAnyPlausible(p.Value, out rate)) return true;
        if (root.ValueKind == JsonValueKind.Array) foreach (var e in root.EnumerateArray()) if (TryAnyPlausible(e, out rate)) return true;
        rate = 0; return false;
    }

    private static bool TryDecimal(JsonElement el, out decimal value)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out value)) return true;
        if (el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString()?.Replace(',', '.');
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }
        value = 0; return false;
    }
}
