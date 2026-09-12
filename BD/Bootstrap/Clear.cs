using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using MySqlConnector;
using Neo4j.Driver;
using Npgsql;

namespace Practica.Bootstrap;

public sealed partial class Workspace
{
    private sealed record ClearTarget(int? BankId, string Name, string Engine, string Database,
        string ConnectionString, string User = "", string Password = "");

    private List<ClearTarget> ClearTargets()
    {
        var banks = BankDirectories.Select(directory => LoadBank(directory).Settings).OrderBy(bank => bank.BancoId).ToList();
        if (!banks.Select(bank => bank.BancoId).SequenceEqual(Enumerable.Range(1, 14)))
            throw new InvalidDataException("El vaciado requiere exactamente los bancos 1 al 14, sin identificadores repetidos.");

        var targets = banks.Select(bank => new ClearTarget(bank.BancoId, bank.Nombre, bank.DatabaseEngine,
            bank.DatabaseName, bank.ConnectionString, bank.DatabaseUser, bank.DatabasePassword)).ToList();
        var main = new ConfigurationBuilder().SetBasePath(Path.Combine(Root, "ASFI_Cluster_DotNet10/Asfi.Main.Api"))
            .AddJsonFile("appsettings.json").Build();
        var asfiConnection = main.GetConnectionString("Asfi") ?? throw new InvalidDataException("Falta la conexion de ASFI.");
        targets.Add(new ClearTarget(null, "ASFI", "SQLServer", new SqlConnectionStringBuilder(asfiConnection).InitialCatalog, asfiConnection));

        foreach (var target in targets)
        {
            if (string.IsNullOrWhiteSpace(target.Database) || new[] { "master", "model", "msdb", "tempdb", "postgres", "template0", "template1", "mysql", "sys", "information_schema", "performance_schema", "admin", "config", "local", "system" }.Contains(target.Database, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException($"Destino de vaciado invalido para {target.Name}.");
            var connectionDatabase = target.Engine switch
            {
                "PostgreSQL" => new NpgsqlConnectionStringBuilder(target.ConnectionString).Database,
                "MySQL" => new MySqlConnectionStringBuilder(target.ConnectionString).Database,
                "SQLServer" => new SqlConnectionStringBuilder(target.ConnectionString).InitialCatalog,
                "MongoDB" or "Neo4j" => target.Database,
                _ => throw new InvalidDataException($"Motor no soportado: {target.Engine}")
            };
            if (!string.Equals(connectionDatabase, target.Database, StringComparison.Ordinal))
                throw new InvalidDataException($"La base de la cadena de conexion no coincide con DatabaseName para {target.Name}.");
        }
        return targets;
    }

    public void ShowClearPlan()
    {
        foreach (var target in ClearTargets())
            Console.WriteLine($"{target.BankId?.ToString("00") ?? "15"}/15 | {target.Name} | {target.Engine} | {target.Database}");
        Console.WriteLine("Plan: 14 bancos + ASFI. Solo lectura de configuracion; no se conecta a las bases.");
    }

    public async Task CheckDatabasesAsync()
    {
        foreach (var target in ClearTargets())
        {
            try
            {
                var count = await CountAsync(target);
                Console.WriteLine($"BD OK {target.BankId?.ToString("00") ?? "15"}/15: {target.Name} / {target.Database} / {count} registros.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Sin acceso a {target.Name} ({target.Engine}, {target.Database}): {ex.GetBaseException().Message}", ex);
            }
        }
    }

    public async Task ClearAsync()
    {
        var targets = ClearTargets();
        // Revisar todos los destinos antes del primer borrado evita empezar si falta una base o una tabla.
        foreach (var target in targets)
        {
            var count = await CountAsync(target);
            Console.WriteLine($"Verificado: {target.Name} / {target.Database} / {count} registros.");
        }

        var completed = 0;
        try
        {
            foreach (var target in targets)
            {
                await ClearTargetAsync(target);
                if (await CountAsync(target) != 0) throw new InvalidOperationException($"Persisten datos en {target.Name}.");
                completed++;
                Console.WriteLine($"VACIADO {completed:00}/15: {target.Name} ({target.Engine}, {target.Database}).");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Vaciado interrumpido: {completed}/15 destinos completados. No hay una transaccion comun entre motores; corrige el error y repite el vaciado. {ex.GetBaseException().Message}", ex);
        }
        Console.WriteLine("Vaciado verificado: 15/15. Se conservaron las bases, tablas, indices y restricciones.");
    }

    private static DbConnection RelationalConnection(ClearTarget target) => target.Engine switch
    {
        "PostgreSQL" => new NpgsqlConnection(target.ConnectionString),
        "MySQL" => new MySqlConnection(target.ConnectionString),
        "SQLServer" => new SqlConnection(target.ConnectionString),
        _ => throw new InvalidOperationException("El motor no es relacional.")
    };

    private static async Task<long> CountAsync(ClearTarget target)
    {
        if (target.Engine == "MongoDB")
        {
            var collection = new MongoClient(target.ConnectionString).GetDatabase(target.Database).GetCollection<BsonDocument>("encrypted_accounts");
            return await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        }
        if (target.Engine == "Neo4j")
        {
            await using var driver = GraphDatabase.Driver(target.ConnectionString, AuthTokens.Basic(target.User, target.Password));
            await using var session = driver.AsyncSession(options => options.WithDatabase(target.Database));
            var cursor = await session.RunAsync("MATCH (a:EncryptedAccount {bankId:$bank}) RETURN count(a) AS total", new { bank = target.BankId!.Value });
            return (await cursor.SingleAsync())["total"].As<long>();
        }
        await using var connection = RelationalConnection(target);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 120;
        command.CommandText = target.BankId is null
            ? "SELECT (SELECT COUNT_BIG(*) FROM dbo.Cuentas) + (SELECT COUNT_BIG(*) FROM dbo.ConversionResultados) + (SELECT COUNT_BIG(*) FROM dbo.ConversionRuns) + (SELECT COUNT_BIG(*) FROM dbo.Bancos)"
            : "SELECT COUNT(*) FROM encrypted_accounts";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task ClearTargetAsync(ClearTarget target)
    {
        if (target.Engine == "MongoDB")
        {
            var collection = new MongoClient(target.ConnectionString).GetDatabase(target.Database).GetCollection<BsonDocument>("encrypted_accounts");
            await collection.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
            return;
        }
        if (target.Engine == "Neo4j")
        {
            await using var driver = GraphDatabase.Driver(target.ConnectionString, AuthTokens.Basic(target.User, target.Password));
            await using var session = driver.AsyncSession(options => options.WithDatabase(target.Database));
            var cursor = await session.RunAsync("MATCH (a:EncryptedAccount {bankId:$bank}) DETACH DELETE a", new { bank = target.BankId!.Value });
            await cursor.ConsumeAsync();
            return;
        }
        await using var connection = RelationalConnection(target);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 120;
        if (target.BankId is not null)
        {
            command.CommandText = "TRUNCATE TABLE encrypted_accounts";
            await command.ExecuteNonQueryAsync();
            return;
        }
        // Las cuatro tablas del esquema ASFI se vacian en una sola transaccion.
        // TRUNCATE reinicia tambien el IDENTITY de ConversionResultados.
        await using var transaction = await connection.BeginTransactionAsync();
        command.Transaction = transaction;
        command.CommandText = "TRUNCATE TABLE dbo.Cuentas; TRUNCATE TABLE dbo.ConversionResultados; TRUNCATE TABLE dbo.ConversionRuns; TRUNCATE TABLE dbo.Bancos;";
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }
}
