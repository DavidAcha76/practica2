-- Script opcional. Asfi.Main.Api crea este esquema automáticamente al iniciar.
IF DB_ID(N'ASFI') IS NULL CREATE DATABASE ASFI;
GO
USE ASFI;
GO
IF OBJECT_ID('dbo.Bancos','U') IS NULL
CREATE TABLE dbo.Bancos(
    BancoId int NOT NULL PRIMARY KEY,
    Nombre nvarchar(150) NOT NULL,
    AlgoritmoEncriptacion nvarchar(50) NOT NULL
);
IF OBJECT_ID('dbo.ConversionRuns','U') IS NULL
CREATE TABLE dbo.ConversionRuns(
    RunId uniqueidentifier NOT NULL PRIMARY KEY,
    StartedAtUtc datetime2 NOT NULL,
    FinishedAtUtc datetime2 NULL,
    Status nvarchar(40) NOT NULL,
    ExchangeRate decimal(18,4) NULL,
    TotalRecords int NOT NULL DEFAULT 0,
    SuccessRecords int NOT NULL DEFAULT 0,
    FailedRecords int NOT NULL DEFAULT 0,
    ErrorMessage nvarchar(max) NULL
);
IF OBJECT_ID('dbo.ConversionResultados','U') IS NULL
CREATE TABLE dbo.ConversionResultados(
    ResultId bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
    RunId uniqueidentifier NOT NULL,
    BatchId int NOT NULL,
    RecordId nvarchar(150) NOT NULL,
    CuentaId nvarchar(150) NOT NULL,
    BancoId int NOT NULL,
    Identificacion nvarchar(100) NULL,
    Nombres nvarchar(200) NULL,
    Apellidos nvarchar(200) NULL,
    NroCuenta nvarchar(100) NULL,
    SaldoUSD decimal(18,4) NOT NULL,
    SaldoBs decimal(18,4) NOT NULL,
    TipoCambio decimal(18,4) NOT NULL,
    FechaConversionUtc datetime2 NOT NULL,
    CodigoVerificacion char(8) NOT NULL,
    WorkerName nvarchar(100) NOT NULL,
    IntegrityHash char(64) NOT NULL,
    Success bit NOT NULL,
    Error nvarchar(2000) NULL
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Resultados_Run')
CREATE INDEX IX_Resultados_Run ON dbo.ConversionResultados(RunId,BancoId,CuentaId);
IF OBJECT_ID('dbo.Cuentas','U') IS NULL
CREATE TABLE dbo.Cuentas(
    CuentaId nvarchar(150) NOT NULL,
    BancoId int NOT NULL,
    Identificacion nvarchar(100) NULL,
    Nombres nvarchar(200) NULL,
    Apellidos nvarchar(200) NULL,
    NroCuenta nvarchar(100) NULL,
    SaldoUSD decimal(18,4) NOT NULL,
    SaldoBs decimal(18,4) NOT NULL,
    FechaConversion datetime2 NOT NULL,
    CodigoVerificacion char(8) NOT NULL,
    TipoCambio decimal(18,4) NOT NULL,
    RunId uniqueidentifier NOT NULL,
    CONSTRAINT PK_Cuentas PRIMARY KEY(BancoId,CuentaId)
);
GO
MERGE dbo.Bancos AS T
USING (VALUES
(1,N'Banco Unión S.A.',N'César'),
(2,N'Banco Mercantil Santa Cruz S.A.',N'Atbash'),
(3,N'Banco Nacional de Bolivia S.A. (BNB)',N'Vigenère'),
(4,N'Banco de Crédito de Bolivia S.A. (BCP)',N'Playfair'),
(5,N'Banco BISA S.A.',N'Hill'),
(6,N'Banco Ganadero S.A.',N'DES'),
(7,N'Banco Económico S.A.',N'3DES'),
(8,N'Banco Prodem S.A.',N'Blowfish'),
(9,N'Banco Solidario S.A.',N'Twofish'),
(10,N'Banco Fortaleza S.A.',N'AES'),
(11,N'Banco FIE S.A.',N'RSA'),
(12,N'Banco PYME de la Comunidad S.A.',N'ElGamal'),
(13,N'Banco de Desarrollo Productivo S.A.M.',N'ECC'),
(14,N'Banco de la Nación Argentina',N'ChaCha20')
) AS S(BancoId,Nombre,AlgoritmoEncriptacion)
ON T.BancoId=S.BancoId
WHEN MATCHED THEN UPDATE SET Nombre=S.Nombre,AlgoritmoEncriptacion=S.AlgoritmoEncriptacion
WHEN NOT MATCHED THEN INSERT(BancoId,Nombre,AlgoritmoEncriptacion) VALUES(S.BancoId,S.Nombre,S.AlgoritmoEncriptacion);
GO
