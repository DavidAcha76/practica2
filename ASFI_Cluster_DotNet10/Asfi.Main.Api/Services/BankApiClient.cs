using System.Text.Json;
using Asfi.Main.Api.Configuration;
using Asfi.Shared;

namespace Asfi.Main.Api.Services;

public sealed class BankApiClient(IHttpClientFactory factory, ILogger<BankApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<List<EncryptedWorkItem>> FetchAsync(BankOptions bank, CancellationToken ct)
    {
        var client = factory.CreateClient("banks");
        var url = Combine(bank.BaseUrl, bank.AccountsEndpoint);
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var items = new List<EncryptedWorkItem>();
        foreach (var element in EnumerateRecords(doc.RootElement))
        {
            try { items.Add(BankRecordAdapter.Adapt(element, bank.BankId, bank.Name, bank.Algorithm)); }
            catch (Exception ex) { logger.LogWarning(ex, "Registro no adaptable en {Bank}", bank.Name); }
        }
        logger.LogInformation("{Bank}: {Count} registros cifrados recibidos.", bank.Name, items.Count);
        return items;
    }

    private static IEnumerable<JsonElement> EnumerateRecords(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in root.EnumerateArray()) yield return e;
            yield break;
        }
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "cuentas", "items", "data", "results", "records" })
            {
                var prop = root.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in prop.Value.EnumerateArray()) yield return e;
                    yield break;
                }
            }
            yield return root;
        }
    }

    public static string Combine(string baseUrl, string path) => baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
}
