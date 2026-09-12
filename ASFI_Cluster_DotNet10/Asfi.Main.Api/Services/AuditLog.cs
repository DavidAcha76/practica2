using System.Text.Json;
using Asfi.Main.Api.Configuration;
using Asfi.Shared;
using Microsoft.Extensions.Options;

namespace Asfi.Main.Api.Services;

public sealed class AuditLog
{
    private readonly string _dir;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AuditLog(IWebHostEnvironment env, IOptions<AsfiOptions> options)
    {
        _dir = Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.AuditDirectory));
        Directory.CreateDirectory(_dir);
    }

    public async Task AppendAsync(Guid runId, IEnumerable<ProcessedAccountDto> results, CancellationToken ct)
    {
        var path = Path.Combine(_dir, $"run-{runId:N}.jsonl");
        await _gate.WaitAsync(ct);
        try
        {
            await using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 64 * 1024, true);
            await using var sw = new StreamWriter(fs);
            foreach (var r in results)
            {
                var line = JsonSerializer.Serialize(new
                {
                    timestampUtc = r.FechaConversionUtc,
                    tipoCambio = r.TipoCambio,
                    cuentaId = r.CuentaId,
                    bancoId = r.BancoId,
                    codigoVerificacion = r.CodigoVerificacion,
                    worker = r.WorkerName,
                    success = r.Success,
                    error = r.Error
                });
                await sw.WriteLineAsync(line.AsMemory(), ct);
            }
        }
        finally { _gate.Release(); }
    }
}
