using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Practica.Bootstrap;

public sealed partial class Workspace
{
    public async Task VerifyRunAsync(Guid runId, string? csvPath)
    {
        var csv = csvPath is null ? null : CsvAccounts.Read(csvPath).ToDictionary(x => (x.BankId, x.RecordId));
        var csvCount = csv?.Count;
        var source = new HashSet<(int BankId, string RecordId)>();
        var bankCounts = new SortedDictionary<int, int>();
        foreach (var directory in BankDirectories)
        {
            var (bank, _) = LoadBank(directory);
            var repository = BankApi.Shared.RepositoryFactory.Create(bank);
            try
            {
                var records = await repository.GetAllAsync();
                bankCounts.Add(bank.BancoId, records.Count);
                foreach (var record in records)
                    if (record.BancoId != bank.BancoId || !source.Add((bank.BancoId, record.RecordId)))
                        throw new InvalidDataException($"Identidad bancaria incorrecta o duplicada en banco {bank.BancoId}.");
            }
            finally { if (repository is IAsyncDisposable disposable) await disposable.DisposeAsync(); }
        }
        if (!bankCounts.Keys.SequenceEqual(Enumerable.Range(1, 14))) throw new InvalidDataException("Se requieren los bancos 1 a 14.");
        var configuration = new ConfigurationBuilder().SetBasePath(Path.Combine(Root, "ASFI_Cluster_DotNet10/Asfi.Main.Api")).AddJsonFile("appsettings.json").Build();
        await using var connection = new SqlConnection(configuration.GetConnectionString("Asfi"));
        await connection.OpenAsync();
        decimal rate;
        double seconds;
        DateTime started, finished;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT Status,TotalRecords,SuccessRecords,FailedRecords,ExchangeRate,DurationSeconds,StartedAtUtc,FinishedAtUtc FROM dbo.ConversionRuns WHERE RunId=@run";
            command.Parameters.AddWithValue("@run", runId);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || reader.GetString(0) != "Completado" || reader.GetInt32(1) != source.Count || reader.GetInt32(2) != source.Count || reader.GetInt32(3) != 0 || reader.IsDBNull(5))
                throw new InvalidDataException("La corrida persistida no esta completa o no coincide con las cuentas de los 14 bancos.");
            rate = reader.GetDecimal(4); seconds = reader.GetDouble(5);
            started = DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc);
            finished = DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc);
        }
        var output = Path.Combine(Root, ".runtime", $"asfi-{runId}");
        Directory.CreateDirectory(output);
        var exports = new Dictionary<int, StreamWriter>();
        var outputPaths = new List<string>();
        StreamWriter OpenExport(string fileName)
        {
            var path = Path.Combine(output, fileName);
            outputPaths.Add(path);
            var writer = new StreamWriter(path + ".partial", false, new UTF8Encoding(true));
            writer.WriteLine("BancoId,CuentaId,Identificacion,Nombres,Apellidos,NroCuenta,SaldoUSD,SaldoBs,TipoCambio,FechaConversionUtc,CodigoVerificacion,WorkerName");
            return writer;
        }
        var verified = 0;
        var totals = bankCounts.Keys.ToDictionary(id => id, _ => 0m);
        var all = OpenExport("saldos-bolivianos.csv");
        try
        {
            foreach (var id in bankCounts.Keys) exports.Add(id, OpenExport($"banco-{id:00}.csv"));
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 300;
            command.CommandText = @"
SELECT r.RecordId,r.BancoId,r.CuentaId,r.Identificacion,r.Nombres,r.Apellidos,r.NroCuenta,
       r.SaldoUSD,r.SaldoBs,r.TipoCambio,r.FechaConversionUtc,r.CodigoVerificacion,r.WorkerName,r.Success,
       CASE WHEN EXISTS (
         SELECT c.Identificacion,c.Nombres,c.Apellidos,c.NroCuenta,c.SaldoUSD,c.SaldoBs,c.TipoCambio,c.FechaConversion,c.CodigoVerificacion,c.RunId
         FROM dbo.Cuentas c WHERE c.BancoId=r.BancoId AND c.CuentaId=r.CuentaId
         INTERSECT SELECT r.Identificacion,r.Nombres,r.Apellidos,r.NroCuenta,r.SaldoUSD,r.SaldoBs,r.TipoCambio,r.FechaConversionUtc,r.CodigoVerificacion,r.RunId
       ) THEN 1 ELSE 0 END
FROM dbo.ConversionResultados r WHERE r.RunId=@run ORDER BY r.BancoId,r.CuentaId";
            command.Parameters.AddWithValue("@run", runId);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var bankId = reader.GetInt32(1);
                var key = (bankId, reader.GetString(0));
                if (!source.Remove(key)) throw new InvalidDataException($"Resultado faltante en origen o duplicado; banco {bankId}.");
                string Text(int ordinal) => reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);
                var usd = reader.GetDecimal(7); var bs = reader.GetDecimal(8);
                if (!reader.GetBoolean(13) || reader.GetInt32(14) != 1 || reader.GetDecimal(9) != rate || bs != decimal.Round(usd * rate, 4, MidpointRounding.AwayFromZero))
                    throw new InvalidDataException($"Saldo o consolidacion ASFI incorrectos; banco {bankId}.");
                if (csv is not null && csv.Remove(key, out var row))
                {
                    var expected = row.Account;
                    if (Text(2) != expected.CuentaId || Text(3) != expected.Identificacion || Text(4) != expected.Nombres || Text(5) != expected.Apellidos || Text(6) != expected.NroCuenta || usd != decimal.Round(expected.SaldoUSD, 4, MidpointRounding.AwayFromZero))
                        throw new InvalidDataException($"El resultado ASFI no coincide con el CSV; banco {bankId}. Una cuenta existente puede tener datos distintos.");
                }
                var line = string.Join(",", new[] { bankId.ToString(CultureInfo.InvariantCulture), Text(2), Text(3), Text(4), Text(5), Text(6), usd.ToString("F4", CultureInfo.InvariantCulture), bs.ToString("F4", CultureInfo.InvariantCulture), rate.ToString("F4", CultureInfo.InvariantCulture), DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc).ToString("O"), Text(11), Text(12) }.Select(EscapeCsv));
                await all.WriteLineAsync(line);
                await exports[bankId].WriteLineAsync(line);
                totals[bankId] += bs;
                verified++;
            }
            if (source.Count != 0 || csv?.Count > 0) throw new InvalidDataException($"Faltan {source.Count} cuentas bancarias y {csv?.Count ?? 0} cuentas del CSV en ASFI.");
        }
        finally
        {
            await all.DisposeAsync();
            foreach (var writer in exports.Values) await writer.DisposeAsync();
        }
        // Publicar CSV finales solo despues de comprobar todas las filas; los fallos conservan .partial.
        foreach (var path in outputPaths) File.Move(path + ".partial", path, true);
        var summary = new
        {
            runId, verified = true, records = verified, csvRecords = csvCount, exchangeRate = rate,
            durationSeconds = seconds, durationMinutes = seconds / 60, recordsPerSecond = seconds > 0 ? verified / seconds : 0,
            startedAtUtc = started, finishedAtUtc = finished,
            timingScope = "ASFI: cotizacion, lectura de bancos, descifrado, conversion, persistencia y consolidacion. Excluye arranque, importacion y verificacion/exportacion.",
            banks = bankCounts.Select(b => new { bankId = b.Key, records = b.Value, saldoBs = totals[b.Key] }).ToArray()
        };
        await File.WriteAllTextAsync(Path.Combine(output, "resumen.json"), JsonSerializer.Serialize(summary, JsonOptions));
        foreach (var bank in bankCounts) Console.WriteLine($"Verificado banco {bank.Key:00}: {bank.Value} cuentas y saldos Bs correctos.");
        Console.WriteLine($"ASFI verificado: {verified} cuentas; {seconds:F3} segundos. CSV completo y 14 archivos por banco: {output}");
    }

    private static string EscapeCsv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
