using System.Text.Json;

namespace Asfi.Shared;

public sealed class CryptoEnvelopeDto
{
    public string Algorithm { get; set; } = string.Empty;
    public string? KeyId { get; set; }
    public int KeyVersion { get; set; } = 1;
    public string Payload { get; set; } = string.Empty;
    public string? Encoding { get; set; }
    public string? IvBase64 { get; set; }
    public string? NonceBase64 { get; set; }
    public string? TagBase64 { get; set; }
    public string? AuxiliaryBase64 { get; set; }
    public int? PlainLength { get; set; }
}

public sealed class EncryptedWorkItem
{
    public string RecordId { get; set; } = string.Empty;
    public int BankId { get; set; }
    public string BankName { get; set; } = string.Empty;
    public CryptoEnvelopeDto Envelope { get; set; } = new();
}

public sealed class WorkBatchRequest
{
    public Guid RunId { get; set; }
    public int BatchId { get; set; }
    public decimal ExchangeRate { get; set; }
    public List<EncryptedWorkItem> Items { get; set; } = [];
}

public sealed class ProcessedAccountDto
{
    public Guid RunId { get; set; }
    public int BatchId { get; set; }
    public string RecordId { get; set; } = string.Empty;
    public string CuentaId { get; set; } = string.Empty;
    public int BancoId { get; set; }
    public string? Identificacion { get; set; }
    public string? Nombres { get; set; }
    public string? Apellidos { get; set; }
    public string? NroCuenta { get; set; }
    public decimal SaldoUSD { get; set; }
    public decimal SaldoBs { get; set; }
    public decimal TipoCambio { get; set; }
    public DateTime FechaConversionUtc { get; set; }
    public string CodigoVerificacion { get; set; } = string.Empty;
    public string WorkerName { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public sealed class WorkBatchResponse
{
    public int BatchId { get; set; }
    public string WorkerName { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public int MaxParallelism { get; set; }
    public long DurationMs { get; set; }
    public List<ProcessedAccountDto> Results { get; set; } = [];
}

public sealed class WorkerHealthResponse
{
    public string NodeName { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public int MaxParallelism { get; set; }
    public DateTime UtcNow { get; set; }
}

public sealed class BankRecordAdapter
{
    private static readonly string[] EnvelopeNames = ["envelope", "cryptoEnvelope", "saldoUsdCifrado", "encryptedPayload", "encryptedData", "crypto"];

    public static EncryptedWorkItem Adapt(JsonElement element, int configuredBankId, string bankName, string configuredAlgorithm)
    {
        var envelopeRoot = FindObjectDeep(element, EnvelopeNames) ?? element;
        var algorithm = ReadStringDeep(envelopeRoot, "algorithm", "algoritmo", "encryptionAlgorithm")
                        ?? ReadStringDeep(element, "algorithm", "algoritmo", "encryptionAlgorithm")
                        ?? configuredAlgorithm;
        var payload = ReadStringDeep(envelopeRoot, "payload", "cipherText", "ciphertext", "datosCifrados", "encryptedPayload")
                      ?? ReadStringDeep(element, "payload", "cipherText", "ciphertext", "datosCifrados", "encryptedData", "encryptedPayload")
                      ?? throw new InvalidOperationException("El registro cifrado no contiene un campo payload/cipherText reconocible.");

        var bankId = ReadIntDeep(element, "bankId", "bancoId") ?? configuredBankId;
        var recordId = ReadStringDeep(element, "recordId", "id", "_id") ?? Guid.NewGuid().ToString("N");

        return new EncryptedWorkItem
        {
            RecordId = recordId,
            BankId = bankId,
            BankName = bankName,
            Envelope = new CryptoEnvelopeDto
            {
                Algorithm = algorithm,
                KeyId = ReadStringDeep(envelopeRoot, "keyId", "claveId") ?? ReadStringDeep(element, "keyId", "claveId"),
                KeyVersion = ReadIntDeep(envelopeRoot, "keyVersion", "versionClave") ?? ReadIntDeep(element, "keyVersion", "versionClave") ?? 1,
                Payload = payload,
                Encoding = ReadStringDeep(envelopeRoot, "encoding", "codificacion"),
                IvBase64 = ReadStringDeep(envelopeRoot, "ivBase64", "iv", "ivB64"),
                NonceBase64 = ReadStringDeep(envelopeRoot, "nonceBase64", "nonce", "nonceB64"),
                TagBase64 = ReadStringDeep(envelopeRoot, "tagBase64", "tag", "authTagBase64"),
                AuxiliaryBase64 = ReadStringDeep(envelopeRoot, "auxiliaryBase64", "auxiliary", "wrappedKeyBase64", "ephemeralPublicKeyBase64", "wrappedKey", "ephemeralPublicKey"),
                PlainLength = ReadIntDeep(envelopeRoot, "plainLength", "originalLength")
            }
        };
    }

    private static JsonElement? FindObjectDeep(JsonElement root, params string[] names)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in root.EnumerateObject())
            {
                if (names.Any(n => string.Equals(n, p.Name, StringComparison.OrdinalIgnoreCase)) && p.Value.ValueKind == JsonValueKind.Object)
                    return p.Value;
                var nested = FindObjectDeep(p.Value, names);
                if (nested.HasValue) return nested;
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in root.EnumerateArray())
            {
                var nested = FindObjectDeep(e, names);
                if (nested.HasValue) return nested;
            }
        }
        return null;
    }

    private static string? ReadStringDeep(JsonElement root, params string[] names)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in root.EnumerateObject())
            {
                if (names.Any(n => string.Equals(n, p.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    var direct = Scalar(p.Value);
                    if (direct is not null) return direct;
                }
            }
            foreach (var p in root.EnumerateObject())
            {
                var nested = ReadStringDeep(p.Value, names);
                if (nested is not null) return nested;
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in root.EnumerateArray())
            {
                var nested = ReadStringDeep(e, names);
                if (nested is not null) return nested;
            }
        }
        return null;
    }

    private static string? Scalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        _ => null
    };

    private static int? ReadIntDeep(JsonElement root, params string[] names)
    {
        var text = ReadStringDeep(root, names);
        return int.TryParse(text, out var value) ? value : null;
    }
}
