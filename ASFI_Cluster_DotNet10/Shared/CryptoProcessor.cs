using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Asfi.Shared;

public sealed class CryptoProcessor
{
    private readonly CryptoKeyRing _keys;

    public CryptoProcessor(CryptoKeyRing keys) => _keys = keys;

    public async Task<WorkBatchResponse> ProcessBatchAsync(WorkBatchRequest request, int configuredParallelism, string workerName, CancellationToken ct)
    {
        var maxParallelism = configuredParallelism <= 0 ? Environment.ProcessorCount : configuredParallelism;
        var bag = new ConcurrentBag<ProcessedAccountDto>();
        var sw = Stopwatch.StartNew();

        await Parallel.ForEachAsync(request.Items, new ParallelOptions { MaxDegreeOfParallelism = maxParallelism, CancellationToken = ct },
            (item, token) =>
            {
                bag.Add(ProcessOne(request, item, workerName));
                return ValueTask.CompletedTask;
            });

        sw.Stop();
        return new WorkBatchResponse
        {
            BatchId = request.BatchId,
            WorkerName = workerName,
            ProcessorCount = Environment.ProcessorCount,
            MaxParallelism = maxParallelism,
            DurationMs = sw.ElapsedMilliseconds,
            Results = bag.OrderBy(x => x.RecordId, StringComparer.Ordinal).ToList()
        };
    }

    private ProcessedAccountDto ProcessOne(WorkBatchRequest batch, EncryptedWorkItem item, string workerName)
    {
        var now = DateTime.UtcNow;
        try
        {
            var key = _keys.Resolve(item.Envelope);
            var plainBytes = CryptoPrimitives.DecryptToPlainBytes(item.Envelope, key);
            using var doc = JsonDocument.Parse(plainBytes);
            var root = doc.RootElement;

            var cuentaId = ReadScalar(root, "cuentaId", "CuentaId", "accountId") ?? item.RecordId;
            var saldoText = ReadScalar(root, "saldoUSD", "saldoUsd", "SaldoUSD", "balanceUSD", "saldo");
            if (!decimal.TryParse(saldoText, NumberStyles.Any, CultureInfo.InvariantCulture, out var saldoUsd) &&
                !decimal.TryParse(saldoText, NumberStyles.Any, CultureInfo.CurrentCulture, out saldoUsd))
                throw new InvalidDataException($"No se pudo obtener saldoUSD del JSON descifrado (valor='{saldoText}').");

            saldoUsd = decimal.Round(saldoUsd, 4, MidpointRounding.AwayFromZero);
            var saldoBs = decimal.Round(saldoUsd * batch.ExchangeRate, 4, MidpointRounding.AwayFromZero);
            var code = VerificationCode(batch.RunId, item.BankId, cuentaId, saldoUsd, saldoBs, batch.ExchangeRate);
            var hash = IntegrityHash(batch.RunId, item.BankId, cuentaId, saldoUsd, saldoBs, batch.ExchangeRate, code);

            return new ProcessedAccountDto
            {
                RunId = batch.RunId,
                BatchId = batch.BatchId,
                RecordId = item.RecordId,
                CuentaId = cuentaId,
                BancoId = item.BankId,
                Identificacion = ReadScalar(root, "identificacion", "ci", "documento"),
                Nombres = ReadScalar(root, "nombres", "nombre", "firstName"),
                Apellidos = ReadScalar(root, "apellidos", "apellido", "lastName"),
                NroCuenta = ReadScalar(root, "nroCuenta", "numeroCuenta", "accountNumber"),
                SaldoUSD = saldoUsd,
                SaldoBs = saldoBs,
                TipoCambio = batch.ExchangeRate,
                FechaConversionUtc = now,
                CodigoVerificacion = code,
                WorkerName = workerName,
                IntegrityHash = hash,
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new ProcessedAccountDto
            {
                RunId = batch.RunId,
                BatchId = batch.BatchId,
                RecordId = item.RecordId,
                CuentaId = item.RecordId,
                BancoId = item.BankId,
                TipoCambio = batch.ExchangeRate,
                FechaConversionUtc = now,
                WorkerName = workerName,
                Success = false,
                Error = ex.GetBaseException().Message
            };
        }
    }

    private static string? ReadScalar(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        foreach (var p in root.EnumerateObject())
        {
            if (!names.Any(n => string.Equals(n, p.Name, StringComparison.OrdinalIgnoreCase))) continue;
            return p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString(),
                JsonValueKind.Number => p.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => p.Value.GetRawText()
            };
        }
        return null;
    }

    private static string VerificationCode(Guid runId, int bankId, string cuentaId, decimal usd, decimal bs, decimal rate)
    {
        var canonical = $"{runId:N}|{bankId}|{cuentaId}|{usd:F4}|{bs:F4}|{rate:F4}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash.AsSpan(0, 4));
    }

    private static string IntegrityHash(Guid runId, int bankId, string cuentaId, decimal usd, decimal bs, decimal rate, string code)
    {
        var canonical = $"{runId:N}|{bankId}|{cuentaId}|{usd:F4}|{bs:F4}|{rate:F4}|{code}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
