using System.Text.Json;
using System.Diagnostics;
using Asfi.Shared;
using BankApi.Shared;

namespace Practica.Bootstrap;

public sealed partial class Workspace
{
    public async Task VerifyCryptoAsync()
    {
        var processor = new CryptoProcessor(CryptoKeyRing.Load(KeyPath));
        foreach (var directory in BankDirectories)
        {
            var (bank, configuration) = LoadBank(directory);
            var input = new PlainAccountInput { CuentaId = "00001234", NroCuenta = "00001234", Identificacion = "012345", Nombres = "María José", Apellidos = "Ñúñez", SaldoUSD = 123.4567m };
            var cipher = CipherFactory.Create(configuration);
            var encrypted = cipher.Encrypt(JsonSerializer.Serialize(input));
            var record = new EncryptedAccountRecord { BancoId = bank.BancoId, Algoritmo = bank.Algorithm, CipherText = encrypted.CipherText, Metadata = encrypted.Metadata };
            var item = BankRecordAdapter.Adapt(JsonSerializer.SerializeToElement(record), bank.BancoId, bank.Nombre, bank.Algorithm);
            var result = await processor.ProcessBatchAsync(new WorkBatchRequest { RunId = Guid.NewGuid(), BatchId = 1, ExchangeRate = 6.96m, Items = [item] }, 1, "VERIFY", CancellationToken.None);
            var actual = result.Results.Single();
            if (!actual.Success || actual.Nombres != input.Nombres || actual.Apellidos != input.Apellidos || actual.Identificacion != input.Identificacion || actual.NroCuenta != input.NroCuenta || actual.SaldoUSD != input.SaldoUSD || actual.SaldoBs != decimal.Round(input.SaldoUSD * 6.96m, 4, MidpointRounding.AwayFromZero))
                throw new InvalidDataException($"Cifrado banco {bank.BancoId} ({bank.Algorithm}): {actual.Error ?? "el resultado no coincide"}");
        }
        Console.WriteLine("OK: los 14 cifrados bancarios se adaptan, descifran y convierten correctamente en ASFI.");
    }

    public async Task ImportAsync(string csvPath)
    {
        var rows = CsvAccounts.Read(csvPath, Path.Combine(Root, ".runtime/csv-rejected.csv"));
        await VerifyCryptoAsync();
        // Comprobar los 15 destinos antes de escribir evita una carga parcial por una base ausente.
        await CheckDatabasesAsync();
        var grouped = rows.GroupBy(row => row.BankId).ToDictionary(group => group.Key, group => group.ToList());
        var added = 0;
        var cryptoParallelism = Math.Max(1, Environment.ProcessorCount);
        // El presupuesto compartido evita que ElGamal y ECC sobrepasen los hilos
        // logicos disponibles cuando ambos bancos cifran al mismo tiempo.
        using var asymmetricCipherSlots = new SemaphoreSlim(cryptoParallelism, cryptoParallelism);
        await Parallel.ForEachAsync(BankDirectories, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (directory, cancellationToken) =>
        {
            var (bank, configuration) = LoadBank(directory);
            if (!grouped.TryGetValue(bank.BancoId, out var accounts)) return;
            var repository = RepositoryFactory.Create(bank);
            try
            {
                var bankStopwatch = Stopwatch.StartNew();
                var existing = (await repository.GetAllAsync()).Select(record => record.RecordId).ToHashSet(StringComparer.Ordinal);
                var pending = accounts.Where(row => !existing.Contains(row.RecordId)).ToList();
                var cipher = CipherFactory.Create(configuration);
                // SQL Server recibe una sola operacion SqlBulkCopy por bloque, por eso
                // puede admitir lotes mayores sin ejecutar un INSERT por cada cuenta.
                var batchSize = bank.DatabaseEngine.Equals("SQLServer", StringComparison.OrdinalIgnoreCase) ? 2000 : 500;
                foreach (var batch in pending.Chunk(batchSize))
                {
                    EncryptedAccountRecord Encrypt(CsvAccount row)
                    {
                        var envelope = cipher.Encrypt(JsonSerializer.Serialize(row.Account));
                        return new EncryptedAccountRecord { RecordId = row.RecordId, BancoId = bank.BancoId, Algoritmo = bank.Algorithm, CipherText = envelope.CipherText, Metadata = envelope.Metadata };
                    }

                    EncryptedAccountRecord[] encrypted;
                    if (bank.Algorithm.Equals("ECC", StringComparison.OrdinalIgnoreCase) || bank.Algorithm.Equals("ELGAMAL", StringComparison.OrdinalIgnoreCase))
                    {
                        // Los cifrados asimetricos conservan solo claves inmutables; cada
                        // cuenta crea su propio motor criptografico, por lo que el lote es seguro en paralelo.
                        encrypted = new EncryptedAccountRecord[batch.Length];
                        await Parallel.ForEachAsync(Enumerable.Range(0, batch.Length), new ParallelOptions { MaxDegreeOfParallelism = cryptoParallelism, CancellationToken = cancellationToken }, async (index, token) =>
                        {
                            await asymmetricCipherSlots.WaitAsync(token);
                            try { encrypted[index] = Encrypt(batch[index]); }
                            finally { asymmetricCipherSlots.Release(); }
                        });
                    }
                    else encrypted = batch.Select(Encrypt).ToArray();
                    await repository.InsertBatchAsync(encrypted);
                    Interlocked.Add(ref added, encrypted.Length);
                }
                bankStopwatch.Stop();
                Console.WriteLine($"Banco {bank.BancoId:00}: {pending.Count} importadas; {accounts.Count - pending.Count} ya existentes; {bankStopwatch.Elapsed.TotalSeconds:N2} s.");
            }
            finally { if (repository is IAsyncDisposable disposable) await disposable.DisposeAsync(); }
        });
        Console.WriteLine($"CSV cargado: {added} nuevas / {rows.Count} cuentas. Las bases bancarias contienen el JSON cifrado.");
    }
}
