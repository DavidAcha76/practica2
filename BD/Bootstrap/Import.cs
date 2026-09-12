using System.Text.Json;
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
        await Parallel.ForEachAsync(BankDirectories, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (directory, _) =>
        {
            var (bank, configuration) = LoadBank(directory);
            if (!grouped.TryGetValue(bank.BancoId, out var accounts)) return;
            var repository = RepositoryFactory.Create(bank);
            try
            {
                var existing = (await repository.GetAllAsync()).Select(record => record.RecordId).ToHashSet(StringComparer.Ordinal);
                var pending = accounts.Where(row => !existing.Contains(row.RecordId)).ToList();
                var cipher = CipherFactory.Create(configuration);
                foreach (var batch in pending.Chunk(500))
                {
                    var encrypted = batch.Select(row =>
                    {
                        var envelope = cipher.Encrypt(JsonSerializer.Serialize(row.Account));
                        return new EncryptedAccountRecord { RecordId = row.RecordId, BancoId = bank.BancoId, Algoritmo = bank.Algorithm, CipherText = envelope.CipherText, Metadata = envelope.Metadata };
                    }).ToArray();
                    await repository.InsertBatchAsync(encrypted);
                    Interlocked.Add(ref added, encrypted.Length);
                }
                Console.WriteLine($"Banco {bank.BancoId:00}: {pending.Count} importadas; {accounts.Count - pending.Count} ya existentes.");
            }
            finally { if (repository is IAsyncDisposable disposable) await disposable.DisposeAsync(); }
        });
        Console.WriteLine($"CSV cargado: {added} nuevas / {rows.Count} cuentas. Las bases bancarias contienen el JSON cifrado.");
    }
}
