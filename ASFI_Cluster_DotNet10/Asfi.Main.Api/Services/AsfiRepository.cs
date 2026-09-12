using System.Data;
using Asfi.Main.Api.Models;
using Asfi.Shared;
using Microsoft.Data.SqlClient;

namespace Asfi.Main.Api.Services;

public sealed class AsfiRepository(IConfiguration configuration, ILogger<AsfiRepository> logger)
{
    private readonly string _connectionString = configuration.GetConnectionString("Asfi")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Asfi.");

    public async Task InitializeAsync(CancellationToken ct)
    {
        var builder = new SqlConnectionStringBuilder(_connectionString);
        var database = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(database)) throw new InvalidOperationException("La conexión ASFI debe indicar Database/Initial Catalog.");
        builder.InitialCatalog = "master";
        await using (var master = new SqlConnection(builder.ConnectionString))
        {
            await master.OpenAsync(ct);
            await using var cmd = master.CreateCommand();
            cmd.CommandText = $"IF DB_ID(@db) IS NULL BEGIN DECLARE @sql nvarchar(max)=N'CREATE DATABASE ' + QUOTENAME(@db); EXEC sp_executesql @sql; END";
            cmd.Parameters.AddWithValue("@db", database);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var schema = conn.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(ct);
        logger.LogInformation("SQL Server ASFI listo: {Database}", database);
    }

    public async Task CreateRunAsync(RunStatusDto run, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString); await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO dbo.ConversionRuns(RunId,StartedAtUtc,Status) VALUES(@id,@started,@status)";
        cmd.Parameters.AddWithValue("@id", run.RunId); cmd.Parameters.AddWithValue("@started", run.StartedAtUtc); cmd.Parameters.AddWithValue("@status", run.Status);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateRunAsync(RunStatusDto run, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString); await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"UPDATE dbo.ConversionRuns SET Status=@status,FinishedAtUtc=@finished,ExchangeRate=@rate,TotalRecords=@total,SuccessRecords=@ok,FailedRecords=@fail,ErrorMessage=@error,DurationSeconds=@duration WHERE RunId=@id";
        cmd.Parameters.AddWithValue("@id", run.RunId); cmd.Parameters.AddWithValue("@status", run.Status);
        cmd.Parameters.AddWithValue("@finished", (object?)run.FinishedAtUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@rate", (object?)run.ExchangeRate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@total", run.TotalRecords); cmd.Parameters.AddWithValue("@ok", run.SuccessRecords); cmd.Parameters.AddWithValue("@fail", run.FailedRecords);
        cmd.Parameters.AddWithValue("@error", (object?)run.Error ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@duration", (object?)run.DurationSeconds ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task BulkInsertAsync(IReadOnlyCollection<ProcessedAccountDto> results, CancellationToken ct)
    {
        if (results.Count == 0) return;
        var table = new DataTable();
        table.Columns.Add("RunId", typeof(Guid));
        table.Columns.Add("BatchId", typeof(int));
        table.Columns.Add("RecordId", typeof(string));
        table.Columns.Add("CuentaId", typeof(string));
        table.Columns.Add("BancoId", typeof(int));
        table.Columns.Add("Identificacion", typeof(string));
        table.Columns.Add("Nombres", typeof(string));
        table.Columns.Add("Apellidos", typeof(string));
        table.Columns.Add("NroCuenta", typeof(string));
        table.Columns.Add("SaldoUSD", typeof(decimal));
        table.Columns.Add("SaldoBs", typeof(decimal));
        table.Columns.Add("TipoCambio", typeof(decimal));
        table.Columns.Add("FechaConversionUtc", typeof(DateTime));
        table.Columns.Add("CodigoVerificacion", typeof(string));
        table.Columns.Add("WorkerName", typeof(string));
        table.Columns.Add("IntegrityHash", typeof(string));
        table.Columns.Add("Success", typeof(bool));
        table.Columns.Add("Error", typeof(string));
        foreach (var r in results)
        {
            var row = table.NewRow();
            row["RunId"] = r.RunId; row["BatchId"] = r.BatchId; row["RecordId"] = r.RecordId; row["CuentaId"] = r.CuentaId; row["BancoId"] = r.BancoId;
            row["Identificacion"] = Db(r.Identificacion); row["Nombres"] = Db(r.Nombres); row["Apellidos"] = Db(r.Apellidos); row["NroCuenta"] = Db(r.NroCuenta);
            row["SaldoUSD"] = r.SaldoUSD; row["SaldoBs"] = r.SaldoBs; row["TipoCambio"] = r.TipoCambio; row["FechaConversionUtc"] = r.FechaConversionUtc;
            row["CodigoVerificacion"] = r.CodigoVerificacion; row["WorkerName"] = r.WorkerName; row["IntegrityHash"] = r.IntegrityHash; row["Success"] = r.Success; row["Error"] = Db(r.Error);
            table.Rows.Add(row);
        }
        await using var conn = new SqlConnection(_connectionString); await conn.OpenAsync(ct);
        using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.TableLock, null) { DestinationTableName = "dbo.ConversionResultados", BatchSize = Math.Min(5000, results.Count), BulkCopyTimeout = 120 };
        foreach (DataColumn c in table.Columns) bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
        await bulk.WriteToServerAsync(table, ct);
    }

    public async Task ConsolidateAsync(Guid runId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString); await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;
        cmd.CommandText = @"
MERGE dbo.Cuentas AS T
USING (SELECT * FROM dbo.ConversionResultados WHERE RunId=@run AND Success=1) AS S
ON T.BancoId=S.BancoId AND T.CuentaId=S.CuentaId
WHEN MATCHED THEN UPDATE SET T.Identificacion=S.Identificacion,T.Nombres=S.Nombres,T.Apellidos=S.Apellidos,T.NroCuenta=S.NroCuenta,T.SaldoUSD=S.SaldoUSD,T.SaldoBs=S.SaldoBs,T.FechaConversion=S.FechaConversionUtc,T.CodigoVerificacion=S.CodigoVerificacion,T.TipoCambio=S.TipoCambio,T.RunId=S.RunId
WHEN NOT MATCHED THEN INSERT(CuentaId,BancoId,Identificacion,Nombres,Apellidos,NroCuenta,SaldoUSD,SaldoBs,FechaConversion,CodigoVerificacion,TipoCambio,RunId) VALUES(S.CuentaId,S.BancoId,S.Identificacion,S.Nombres,S.Apellidos,S.NroCuenta,S.SaldoUSD,S.SaldoBs,S.FechaConversionUtc,S.CodigoVerificacion,S.TipoCambio,S.RunId);";
        cmd.Parameters.AddWithValue("@run", runId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<object>> GetResultsAsync(Guid runId, int take, CancellationToken ct)
    {
        var list = new List<object>();
        await using var conn = new SqlConnection(_connectionString); await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT TOP (@take) CuentaId,BancoId,Identificacion,Nombres,Apellidos,NroCuenta,SaldoUSD,SaldoBs,TipoCambio,FechaConversionUtc,CodigoVerificacion,WorkerName,Success,Error FROM dbo.ConversionResultados WHERE RunId=@run ORDER BY ResultId";
        cmd.Parameters.AddWithValue("@take", Math.Clamp(take,1,5000)); cmd.Parameters.AddWithValue("@run", runId);
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(new { CuentaId=rd.GetString(0), BancoId=rd.GetInt32(1), Identificacion=Nullable(rd,2), Nombres=Nullable(rd,3), Apellidos=Nullable(rd,4), NroCuenta=Nullable(rd,5), SaldoUSD=rd.GetDecimal(6), SaldoBs=rd.GetDecimal(7), TipoCambio=rd.GetDecimal(8), FechaConversionUtc=rd.GetDateTime(9), CodigoVerificacion=rd.GetString(10), WorkerName=rd.GetString(11), Success=rd.GetBoolean(12), Error=Nullable(rd,13) });
        return list;
    }

    private static object Db(string? v) => string.IsNullOrEmpty(v) ? DBNull.Value : v;
    private static string? Nullable(SqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

    private const string SchemaSql = @"
IF OBJECT_ID('dbo.Bancos','U') IS NULL CREATE TABLE dbo.Bancos(BancoId int NOT NULL PRIMARY KEY,Nombre nvarchar(150) NOT NULL,AlgoritmoEncriptacion nvarchar(50) NOT NULL);
IF OBJECT_ID('dbo.ConversionRuns','U') IS NULL CREATE TABLE dbo.ConversionRuns(RunId uniqueidentifier NOT NULL PRIMARY KEY,StartedAtUtc datetime2 NOT NULL,FinishedAtUtc datetime2 NULL,Status nvarchar(40) NOT NULL,ExchangeRate decimal(18,4) NULL,TotalRecords int NOT NULL DEFAULT 0,SuccessRecords int NOT NULL DEFAULT 0,FailedRecords int NOT NULL DEFAULT 0,ErrorMessage nvarchar(max) NULL);
IF COL_LENGTH('dbo.ConversionRuns','DurationSeconds') IS NULL ALTER TABLE dbo.ConversionRuns ADD DurationSeconds float NULL;
IF OBJECT_ID('dbo.ConversionResultados','U') IS NULL CREATE TABLE dbo.ConversionResultados(ResultId bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,RunId uniqueidentifier NOT NULL,BatchId int NOT NULL,RecordId nvarchar(150) NOT NULL,CuentaId nvarchar(150) NOT NULL,BancoId int NOT NULL,Identificacion nvarchar(100) NULL,Nombres nvarchar(200) NULL,Apellidos nvarchar(200) NULL,NroCuenta nvarchar(100) NULL,SaldoUSD decimal(18,4) NOT NULL,SaldoBs decimal(18,4) NOT NULL,TipoCambio decimal(18,4) NOT NULL,FechaConversionUtc datetime2 NOT NULL,CodigoVerificacion char(8) NOT NULL,WorkerName nvarchar(100) NOT NULL,IntegrityHash char(64) NOT NULL,Success bit NOT NULL,Error nvarchar(2000) NULL);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Resultados_Run') CREATE INDEX IX_Resultados_Run ON dbo.ConversionResultados(RunId,BancoId,CuentaId);
IF OBJECT_ID('dbo.Cuentas','U') IS NULL CREATE TABLE dbo.Cuentas(CuentaId nvarchar(150) NOT NULL,BancoId int NOT NULL,Identificacion nvarchar(100) NULL,Nombres nvarchar(200) NULL,Apellidos nvarchar(200) NULL,NroCuenta nvarchar(100) NULL,SaldoUSD decimal(18,4) NOT NULL,SaldoBs decimal(18,4) NOT NULL,FechaConversion datetime2 NOT NULL,CodigoVerificacion char(8) NOT NULL,TipoCambio decimal(18,4) NOT NULL,RunId uniqueidentifier NOT NULL,CONSTRAINT PK_Cuentas PRIMARY KEY(BancoId,CuentaId));
MERGE dbo.Bancos AS T USING (VALUES
(1,N'Banco Unión S.A.',N'César'),(2,N'Banco Mercantil Santa Cruz S.A.',N'Atbash'),(3,N'Banco Nacional de Bolivia S.A. (BNB)',N'Vigenère'),(4,N'Banco de Crédito de Bolivia S.A. (BCP)',N'Playfair'),(5,N'Banco BISA S.A.',N'Hill'),(6,N'Banco Ganadero S.A.',N'DES'),(7,N'Banco Económico S.A.',N'3DES'),(8,N'Banco Prodem S.A.',N'Blowfish'),(9,N'Banco Solidario S.A.',N'Twofish'),(10,N'Banco Fortaleza S.A.',N'AES'),(11,N'Banco FIE S.A.',N'RSA'),(12,N'Banco PYME de la Comunidad S.A.',N'ElGamal'),(13,N'Banco de Desarrollo Productivo S.A.M.',N'ECC'),(14,N'Banco de la Nación Argentina',N'ChaCha20')) AS S(BancoId,Nombre,AlgoritmoEncriptacion) ON T.BancoId=S.BancoId WHEN MATCHED THEN UPDATE SET Nombre=S.Nombre,AlgoritmoEncriptacion=S.AlgoritmoEncriptacion WHEN NOT MATCHED THEN INSERT(BancoId,Nombre,AlgoritmoEncriptacion) VALUES(S.BancoId,S.Nombre,S.AlgoritmoEncriptacion);";
}
