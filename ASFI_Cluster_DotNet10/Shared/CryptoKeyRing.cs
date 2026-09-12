using System.Text.Json;

namespace Asfi.Shared;

public sealed class CryptoKeyEntry
{
    public string KeyId { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string Algorithm { get; set; } = string.Empty;
    public int? IntValue { get; set; }
    public string? TextValue { get; set; }
    public string? ValueBase64 { get; set; }
    public int[]? Matrix { get; set; }
    public string? PrivateKeyPem { get; set; }
    public string? PHex { get; set; }
    public string? GHex { get; set; }
    public string? XHex { get; set; }
    public string? Notes { get; set; }
}

public sealed class CryptoKeyRing
{
    private readonly Dictionary<string, CryptoKeyEntry> _byId;
    private readonly List<CryptoKeyEntry> _entries;

    private CryptoKeyRing(List<CryptoKeyEntry> entries)
    {
        _entries = entries;
        _byId = entries.Where(x => !string.IsNullOrWhiteSpace(x.KeyId))
            .ToDictionary(x => $"{x.KeyId}|{x.Version}", StringComparer.OrdinalIgnoreCase);
    }

    public static CryptoKeyRing Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"No se encontró el llavero ASFI: {path}");
        var json = File.ReadAllText(path);
        var entries = JsonSerializer.Deserialize<List<CryptoKeyEntry>>(json, JsonOptions()) ?? [];
        return new CryptoKeyRing(entries);
    }

    public CryptoKeyEntry Resolve(CryptoEnvelopeDto envelope)
    {
        if (!string.IsNullOrWhiteSpace(envelope.KeyId) && _byId.TryGetValue($"{envelope.KeyId}|{envelope.KeyVersion}", out var byId))
            return byId;

        var normalized = CryptoPrimitives.NormalizeAlgorithm(envelope.Algorithm);
        var byAlgorithm = _entries.FirstOrDefault(x => CryptoPrimitives.NormalizeAlgorithm(x.Algorithm) == normalized && x.Version == envelope.KeyVersion)
                          ?? _entries.FirstOrDefault(x => CryptoPrimitives.NormalizeAlgorithm(x.Algorithm) == normalized);
        if (byAlgorithm is not null) return byAlgorithm;

        if (normalized == "ATBASH") return new CryptoKeyEntry { KeyId = "atbash-no-key", Algorithm = "Atbash", Version = 1 };
        throw new KeyNotFoundException($"No existe llave para algoritmo={envelope.Algorithm}, KeyId={envelope.KeyId ?? "(vacío)"}, versión={envelope.KeyVersion}.");
    }

    private static JsonSerializerOptions JsonOptions() => new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
}
