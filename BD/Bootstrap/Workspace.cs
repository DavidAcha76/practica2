using System.Text.Json;
using System.Text.Json.Nodes;
using Asfi.Shared;
using BankApi.Shared;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Npgsql;

namespace Practica.Bootstrap;

public sealed partial class Workspace(string root)
{
    public string Root { get; } = root;
    private string BankRoot => Path.Combine(Root, "14_Bancos_APIs_Cifradas/src");
    private string KeyPath => Path.Combine(Root, "ASFI_Cluster_DotNet10/config/asfi-keys.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private string[] BankDirectories => Directory.GetDirectories(BankRoot, "Banco*.Api").OrderBy(x => x).ToArray();

    public void Configure()
    {
        var env = File.ReadLines(Path.Combine(Root, "BD/.env")).Where(x => !string.IsNullOrWhiteSpace(x) && !x.TrimStart().StartsWith('#'))
            .Select(x => x.Split('=', 2)).ToDictionary(x => x[0].Trim(), x => x[1].Trim());
        int Port(string name) => int.TryParse(env[name], out var port) && port is > 0 and <= 65535 ? port : throw new InvalidDataException($"Puerto invalido: {name}");
        var password = env["DATABASE_PASSWORD"];
        var keys = new List<CryptoKeyEntry>();
        var connectionStrings = new List<string> { "# Generado por Bootstrap configure; cadenas para .NET, segun BD/.env." };
        if (BankDirectories.Length != 14) throw new InvalidDataException("Se requieren exactamente 14 proyectos bancarios.");
        foreach (var directory in BankDirectories)
        {
            var path = Path.Combine(directory, "appsettings.json");
            var config = JsonNode.Parse(File.ReadAllText(path))!;
            var database = config["Database"]!;
            var name = database["DatabaseName"]!.GetValue<string>();
            database["InitializeOnStartup"] = true;
            database["ConnectionString"] = database["Engine"]!.GetValue<string>() switch
            {
                "PostgreSQL" => new NpgsqlConnectionStringBuilder { Host = "127.0.0.1", Port = Port("POSTGRES_PORT"), Database = name, Username = "postgres", Password = password }.ConnectionString,
                "MySQL" => new MySqlConnectionStringBuilder { Server = "127.0.0.1", Port = (uint)Port("MYSQL_PORT"), Database = name, UserID = "root", Password = password, AllowPublicKeyRetrieval = true, SslMode = MySqlSslMode.Disabled }.ConnectionString,
                "SQLServer" => SqlConnection(name, Port("SQLSERVER_PORT"), password),
                "MongoDB" => $"mongodb://127.0.0.1:{Port("MONGODB_PORT")}",
                "Neo4j" => $"bolt://127.0.0.1:{Port("NEO4J_PORT")}",
                _ => throw new InvalidDataException("Motor desconocido.")
            };
            if (database["Engine"]!.GetValue<string>() == "Neo4j")
            {
                database["User"] = "neo4j";
                database["Password"] = env["NEO4J_PASSWORD"];
            }
            File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            var bankId = config["Bank"]!["BancoId"]!.GetValue<int>();
            connectionStrings.Add($"BANCO_{bankId:00}_CONNECTION_STRING={database["ConnectionString"]!.GetValue<string>()}");
            var encryption = config["Encryption"]!;
            var key = new CryptoKeyEntry { KeyId = $"bank-{bankId}", Algorithm = config["Bank"]!["Algorithm"]!.GetValue<string>(), Notes = "Sincronizada desde la configuracion de las APIs bancarias." };
            switch (key.Algorithm.ToUpperInvariant())
            {
                case "CAESAR": key.IntValue = encryption["Shift"]!.GetValue<int>(); break;
                case "VIGENERE": case "PLAYFAIR": key.TextValue = encryption["Keyword"]!.GetValue<string>(); break;
                case "HILL": key.Matrix = [3, 3, 2, 5]; break;
                case "ATBASH": break;
                case "RSA": key.PrivateKeyPem = File.ReadAllText(Path.Combine(Root, "14_Bancos_APIs_Cifradas/asfi-keys/BancoFIE_RSA_PRIVATE.pem")); break;
                case "ECC": key.PrivateKeyPem = File.ReadAllText(Path.Combine(Root, "14_Bancos_APIs_Cifradas/asfi-keys/BDP_ECC_PRIVATE.pem")); break;
                case "ELGAMAL":
                    var privateKey = JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "14_Bancos_APIs_Cifradas/asfi-keys/BancoPYME_ElGamal_PRIVATE.json")))!;
                    key.PHex = privateKey["pHex"]!.GetValue<string>(); key.GHex = privateKey["gHex"]!.GetValue<string>(); key.XHex = privateKey["xHex"]!.GetValue<string>();
                    break;
                default: key.ValueBase64 = encryption["KeyBase64"]!.GetValue<string>(); break;
            }
            keys.Add(key);
        }
        File.WriteAllText(KeyPath, JsonSerializer.Serialize(keys.OrderBy(k => int.Parse(k.KeyId[5..])), JsonOptions) + Environment.NewLine);
        var mainPath = Path.Combine(Root, "ASFI_Cluster_DotNet10/Asfi.Main.Api/appsettings.json");
        var main = JsonNode.Parse(File.ReadAllText(mainPath))!;
        main["ConnectionStrings"]!["Asfi"] = SqlConnection("ASFI", Port("SQLSERVER_PORT"), password);
        main["Asfi"]!["InitializeDatabaseOnStartup"] = true;
        File.WriteAllText(mainPath, main.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        connectionStrings.Add($"ASFI_CONNECTION_STRING={main["ConnectionStrings"]!["Asfi"]!.GetValue<string>()}");
        File.WriteAllLines(Path.Combine(Root, "BD/connection_strings.env"), connectionStrings);
        Console.WriteLine("Configuradas las 14 bases bancarias, SQL Server ASFI y las claves de descifrado. Workers Tailscale conservados.");
    }

    private static string SqlConnection(string database, int port, string password) => new SqlConnectionStringBuilder
    {
        DataSource = $"127.0.0.1,{port}", InitialCatalog = database, UserID = "sa", Password = password,
        TrustServerCertificate = true, MultipleActiveResultSets = true
    }.ConnectionString;

    private (BankSettings Settings, IConfigurationRoot Configuration) LoadBank(string directory)
    {
        var configuration = new ConfigurationBuilder().SetBasePath(directory).AddJsonFile("appsettings.json").Build();
        foreach (var field in new[] { "PublicKeyFile", "PrivateKeyFile" })
            if (!string.IsNullOrWhiteSpace(configuration[$"Encryption:{field}"]))
                configuration[$"Encryption:{field}"] = Path.GetFullPath(configuration[$"Encryption:{field}"]!, directory);
        var settings = new BankSettings
        {
            BancoId = configuration.GetValue<int>("Bank:BancoId"), Nombre = configuration["Bank:Nombre"]!, Algorithm = configuration["Bank:Algorithm"]!,
            DatabaseEngine = configuration["Database:Engine"]!, DatabaseName = configuration["Database:DatabaseName"]!,
            ConnectionString = configuration["Database:ConnectionString"]!, DatabaseUser = configuration["Database:User"] ?? "", DatabasePassword = configuration["Database:Password"] ?? ""
        };
        return (settings, configuration);
    }

    public async Task InitializeAsync()
    {
        foreach (var directory in BankDirectories)
        {
            var (bank, _) = LoadBank(directory);
            await CreateDatabaseAsync(bank);
            var repository = RepositoryFactory.Create(bank);
            try { await repository.InitializeAsync(); }
            finally { if (repository is IAsyncDisposable disposable) await disposable.DisposeAsync(); }
            Console.WriteLine($"Base lista: banco {bank.BancoId} / {bank.DatabaseEngine} / {bank.DatabaseName}");
        }
    }

    private static async Task CreateDatabaseAsync(BankSettings bank)
    {
        if (bank.DatabaseEngine == "PostgreSQL")
        {
            var builder = new NpgsqlConnectionStringBuilder(bank.ConnectionString) { Database = "postgres" };
            await using var connection = new NpgsqlConnection(builder.ConnectionString); await connection.OpenAsync();
            await using var exists = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname=@name", connection);
            exists.Parameters.AddWithValue("name", bank.DatabaseName);
            if (await exists.ExecuteScalarAsync() is null)
            {
                await using var create = new NpgsqlCommand("CREATE DATABASE " + new NpgsqlCommandBuilder().QuoteIdentifier(bank.DatabaseName), connection);
                await create.ExecuteNonQueryAsync();
            }
        }
        else if (bank.DatabaseEngine == "MySQL")
        {
            var builder = new MySqlConnectionStringBuilder(bank.ConnectionString) { Database = "" };
            await using var connection = new MySqlConnection(builder.ConnectionString); await connection.OpenAsync();
            await using var command = new MySqlCommand("CREATE DATABASE IF NOT EXISTS " + new MySqlCommandBuilder().QuoteIdentifier(bank.DatabaseName) + " CHARACTER SET utf8mb4", connection);
            await command.ExecuteNonQueryAsync();
        }
        else if (bank.DatabaseEngine == "SQLServer")
        {
            var builder = new SqlConnectionStringBuilder(bank.ConnectionString) { InitialCatalog = "master" };
            await using var connection = new SqlConnection(builder.ConnectionString); await connection.OpenAsync();
            await using var command = new SqlCommand("IF DB_ID(@name) IS NULL BEGIN DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@name); EXEC sp_executesql @sql; END", connection);
            command.Parameters.AddWithValue("@name", bank.DatabaseName); await command.ExecuteNonQueryAsync();
        }
    }
}
