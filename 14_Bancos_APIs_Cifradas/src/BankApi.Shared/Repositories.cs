
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MongoDB.Bson;
using MongoDB.Driver;
using MySqlConnector;
using Neo4j.Driver;
using Npgsql;

namespace BankApi.Shared;

public interface IEncryptedAccountRepository
{
    Task InitializeAsync();
    Task<IReadOnlyList<EncryptedAccountRecord>> GetAllAsync();
    Task<EncryptedAccountRecord?> GetByIdAsync(string id);
    Task InsertAsync(EncryptedAccountRecord record);
    Task InsertBatchAsync(IReadOnlyList<EncryptedAccountRecord> records);
}

internal abstract class RelationalRepositoryBase : IEncryptedAccountRepository
{
    protected readonly string ConnectionString;
    protected RelationalRepositoryBase(string connectionString) => ConnectionString = connectionString;
    protected abstract Task<System.Data.Common.DbConnection> OpenAsync();
    protected abstract string CreateTableSql { get; }
    protected virtual string SelectAllSql => "SELECT record_id, bank_id, algorithm, cipher_text, metadata_json, created_at_utc FROM encrypted_accounts ORDER BY created_at_utc";
    protected virtual string SelectOneSql => "SELECT record_id, bank_id, algorithm, cipher_text, metadata_json, created_at_utc FROM encrypted_accounts WHERE record_id=@id";
    protected virtual string InsertSql => "INSERT INTO encrypted_accounts(record_id,bank_id,algorithm,cipher_text,metadata_json,created_at_utc) VALUES(@id,@bank,@alg,@cipher,@meta,@created)";
    public async Task InitializeAsync(){await using var c=await OpenAsync(); await using var cmd=c.CreateCommand(); cmd.CommandText=CreateTableSql; await cmd.ExecuteNonQueryAsync();}
    public async Task<IReadOnlyList<EncryptedAccountRecord>> GetAllAsync(){var list=new List<EncryptedAccountRecord>(); await using var c=await OpenAsync(); await using var cmd=c.CreateCommand(); cmd.CommandText=SelectAllSql; await using var r=await cmd.ExecuteReaderAsync(); while(await r.ReadAsync()) list.Add(Read(r)); return list;}
    public async Task<EncryptedAccountRecord?> GetByIdAsync(string id){await using var c=await OpenAsync(); await using var cmd=c.CreateCommand(); cmd.CommandText=SelectOneSql; Add(cmd,"@id",id); await using var r=await cmd.ExecuteReaderAsync(); return await r.ReadAsync()?Read(r):null;}
    public async Task InsertAsync(EncryptedAccountRecord rec){await using var c=await OpenAsync(); await using var cmd=c.CreateCommand(); cmd.CommandText=InsertSql; Add(cmd,"@id",rec.RecordId);Add(cmd,"@bank",rec.BancoId);Add(cmd,"@alg",rec.Algoritmo);Add(cmd,"@cipher",rec.CipherText);Add(cmd,"@meta",JsonSerializer.Serialize(rec.Metadata));Add(cmd,"@created",rec.CreatedAtUtc); await cmd.ExecuteNonQueryAsync();}
    public async Task InsertBatchAsync(IReadOnlyList<EncryptedAccountRecord> records)
    {
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = InsertSql;
        foreach (var record in records)
        {
            command.Parameters.Clear();
            Add(command, "@id", record.RecordId); Add(command, "@bank", record.BancoId);
            Add(command, "@alg", record.Algoritmo); Add(command, "@cipher", record.CipherText);
            Add(command, "@meta", JsonSerializer.Serialize(record.Metadata)); Add(command, "@created", record.CreatedAtUtc);
            await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }
    protected static void Add(System.Data.Common.DbCommand cmd,string name,object value){var p=cmd.CreateParameter();p.ParameterName=name;p.Value=value;cmd.Parameters.Add(p);}
    private static EncryptedAccountRecord Read(System.Data.Common.DbDataReader r)=>new(){RecordId=r.GetString(0),BancoId=Convert.ToInt32(r.GetValue(1)),Algoritmo=r.GetString(2),CipherText=r.GetString(3),Metadata=JsonSerializer.Deserialize<Dictionary<string,string>>(r.GetString(4))??new(),CreatedAtUtc=Convert.ToDateTime(r.GetValue(5)).ToUniversalTime()};
}

internal sealed class PostgreSqlRepository(string cs):RelationalRepositoryBase(cs)
{
    protected override string CreateTableSql => "CREATE TABLE IF NOT EXISTS encrypted_accounts(record_id VARCHAR(64) PRIMARY KEY, bank_id INTEGER NOT NULL, algorithm VARCHAR(40) NOT NULL, cipher_text TEXT NOT NULL, metadata_json TEXT NOT NULL, created_at_utc TIMESTAMPTZ NOT NULL);";
    protected override async Task<System.Data.Common.DbConnection> OpenAsync(){var c=new NpgsqlConnection(ConnectionString);await c.OpenAsync();return c;}
}
internal sealed class MySqlRepository(string cs):RelationalRepositoryBase(cs)
{
    protected override string CreateTableSql => "CREATE TABLE IF NOT EXISTS encrypted_accounts(record_id VARCHAR(64) PRIMARY KEY, bank_id INT NOT NULL, algorithm VARCHAR(40) NOT NULL, cipher_text LONGTEXT NOT NULL, metadata_json LONGTEXT NOT NULL, created_at_utc DATETIME(6) NOT NULL);";
    protected override async Task<System.Data.Common.DbConnection> OpenAsync(){var c=new MySqlConnection(ConnectionString);await c.OpenAsync();return c;}
}
internal sealed class SqlServerRepository(string cs):RelationalRepositoryBase(cs)
{
    protected override string CreateTableSql => "IF OBJECT_ID('encrypted_accounts','U') IS NULL CREATE TABLE encrypted_accounts(record_id VARCHAR(64) PRIMARY KEY, bank_id INT NOT NULL, algorithm VARCHAR(40) NOT NULL, cipher_text NVARCHAR(MAX) NOT NULL, metadata_json NVARCHAR(MAX) NOT NULL, created_at_utc DATETIME2 NOT NULL);";
    protected override async Task<System.Data.Common.DbConnection> OpenAsync(){var c=new SqlConnection(ConnectionString);await c.OpenAsync();return c;}
}

internal sealed class MongoRepository : IEncryptedAccountRepository
{
    private readonly IMongoCollection<BsonDocument> _col;
    public MongoRepository(string cs,string db){var client=new MongoClient(cs);_col=client.GetDatabase(db).GetCollection<BsonDocument>("encrypted_accounts");}
    public async Task InitializeAsync(){var idx=new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("recordId"),new CreateIndexOptions{Unique=true});await _col.Indexes.CreateOneAsync(idx);}
    public async Task<IReadOnlyList<EncryptedAccountRecord>> GetAllAsync(){var docs=await _col.Find(FilterDefinition<BsonDocument>.Empty).Sort(Builders<BsonDocument>.Sort.Ascending("createdAtUtc")).ToListAsync();return docs.Select(FromDoc).ToList();}
    public async Task<EncryptedAccountRecord?> GetByIdAsync(string id){var d=await _col.Find(Builders<BsonDocument>.Filter.Eq("recordId",id)).FirstOrDefaultAsync();return d is null?null:FromDoc(d);}
    public Task InsertAsync(EncryptedAccountRecord r)=>_col.InsertOneAsync(ToDoc(r));
    public Task InsertBatchAsync(IReadOnlyList<EncryptedAccountRecord> records)=>_col.InsertManyAsync(records.Select(ToDoc));
    private static BsonDocument ToDoc(EncryptedAccountRecord r)=>new(){{"recordId",r.RecordId},{"bankId",r.BancoId},{"algorithm",r.Algoritmo},{"cipherText",r.CipherText},{"metadataJson",JsonSerializer.Serialize(r.Metadata)},{"createdAtUtc",r.CreatedAtUtc}};
    private static EncryptedAccountRecord FromDoc(BsonDocument d)=>new(){RecordId=d["recordId"].AsString,BancoId=d["bankId"].AsInt32,Algoritmo=d["algorithm"].AsString,CipherText=d["cipherText"].AsString,Metadata=JsonSerializer.Deserialize<Dictionary<string,string>>(d["metadataJson"].AsString)??new(),CreatedAtUtc=d["createdAtUtc"].AsBsonDateTime.ToUniversalTime()};
}

internal sealed class Neo4jRepository : IEncryptedAccountRepository, IAsyncDisposable
{
    private readonly IDriver _driver; private readonly string _database; private readonly int _bankId;
    public Neo4jRepository(string uri,string user,string password,string database,int bankId){_driver=GraphDatabase.Driver(uri,AuthTokens.Basic(user,password));_database=database;_bankId=bankId;}
    private IAsyncSession Session()=>_driver.AsyncSession(o=>o.WithDatabase(_database));
    public async Task InitializeAsync(){await using var s=Session();await s.RunAsync("CREATE CONSTRAINT encrypted_account_unique IF NOT EXISTS FOR (a:EncryptedAccount) REQUIRE (a.bankId, a.recordId) IS UNIQUE");}
    public async Task<IReadOnlyList<EncryptedAccountRecord>> GetAllAsync(){await using var s=Session();var c=await s.RunAsync("MATCH (a:EncryptedAccount {bankId:$bank}) RETURN a ORDER BY a.createdAtUtc",new{bank=_bankId});var list=new List<EncryptedAccountRecord>();while(await c.FetchAsync()) list.Add(FromNode(c.Current["a"].As<INode>()));return list;}
    public async Task<EncryptedAccountRecord?> GetByIdAsync(string id){await using var s=Session();var c=await s.RunAsync("MATCH (a:EncryptedAccount {bankId:$bank,recordId:$id}) RETURN a LIMIT 1",new{bank=_bankId,id});if(await c.FetchAsync()) return FromNode(c.Current["a"].As<INode>());return null;}
    public async Task InsertAsync(EncryptedAccountRecord r){await using var s=Session();await s.RunAsync("CREATE (a:EncryptedAccount {recordId:$id,bankId:$bank,algorithm:$alg,cipherText:$cipher,metadataJson:$meta,createdAtUtc:$created})",new{id=r.RecordId,bank=r.BancoId,alg=r.Algoritmo,cipher=r.CipherText,meta=JsonSerializer.Serialize(r.Metadata),created=r.CreatedAtUtc.ToString("O")});}
    public async Task InsertBatchAsync(IReadOnlyList<EncryptedAccountRecord> records)
    {
        if (records.Any(r => r.BancoId != _bankId)) throw new InvalidOperationException("El lote pertenece a otro banco.");
        var rows = records.Select(r => new Dictionary<string, object>
        {
            ["recordId"] = r.RecordId, ["bankId"] = r.BancoId, ["algorithm"] = r.Algoritmo,
            ["cipherText"] = r.CipherText, ["metadataJson"] = JsonSerializer.Serialize(r.Metadata),
            ["createdAtUtc"] = r.CreatedAtUtc.ToString("O")
        }).ToArray();
        await using var session = Session();
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync("UNWIND $rows AS row CREATE (a:EncryptedAccount) SET a = row", new { rows });
            await cursor.ConsumeAsync();
        });
    }
    private static EncryptedAccountRecord FromNode(INode n)=>new(){RecordId=n.Properties["recordId"].As<string>(),BancoId=Convert.ToInt32(n.Properties["bankId"]),Algoritmo=n.Properties["algorithm"].As<string>(),CipherText=n.Properties["cipherText"].As<string>(),Metadata=JsonSerializer.Deserialize<Dictionary<string,string>>(n.Properties["metadataJson"].As<string>())??new(),CreatedAtUtc=DateTime.Parse(n.Properties["createdAtUtc"].ToString()!).ToUniversalTime()};
    public ValueTask DisposeAsync()=>_driver.DisposeAsync();
}

public static class RepositoryFactory
{
    public static IEncryptedAccountRepository Create(BankSettings s)=>s.DatabaseEngine.ToUpperInvariant() switch
    {
        "POSTGRESQL"=>new PostgreSqlRepository(s.ConnectionString),
        "MYSQL"=>new MySqlRepository(s.ConnectionString),
        "SQLSERVER"=>new SqlServerRepository(s.ConnectionString),
        "MONGODB"=>new MongoRepository(s.ConnectionString,s.DatabaseName),
        "NEO4J"=>new Neo4jRepository(s.ConnectionString,s.DatabaseUser,s.DatabasePassword,string.IsNullOrWhiteSpace(s.DatabaseName)?"neo4j":s.DatabaseName,s.BancoId),
        _=>throw new NotSupportedException($"Motor no soportado: {s.DatabaseEngine}")
    };
}
