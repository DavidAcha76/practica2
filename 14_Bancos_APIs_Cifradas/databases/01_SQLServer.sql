-- Ejecutar con SSMS o sqlcmd sobre localhost\SQLEXPRESS
IF DB_ID('BancoBNB') IS NULL CREATE DATABASE BancoBNB;
GO
USE BancoBNB;
IF OBJECT_ID('encrypted_accounts','U') IS NULL CREATE TABLE encrypted_accounts(record_id VARCHAR(64) PRIMARY KEY, bank_id INT NOT NULL, algorithm VARCHAR(40) NOT NULL, cipher_text NVARCHAR(MAX) NOT NULL, metadata_json NVARCHAR(MAX) NOT NULL, created_at_utc DATETIME2 NOT NULL);
GO
IF DB_ID('BancoProdem') IS NULL CREATE DATABASE BancoProdem;
GO
USE BancoProdem;
IF OBJECT_ID('encrypted_accounts','U') IS NULL CREATE TABLE encrypted_accounts(record_id VARCHAR(64) PRIMARY KEY, bank_id INT NOT NULL, algorithm VARCHAR(40) NOT NULL, cipher_text NVARCHAR(MAX) NOT NULL, metadata_json NVARCHAR(MAX) NOT NULL, created_at_utc DATETIME2 NOT NULL);
GO
IF DB_ID('BancoBDP') IS NULL CREATE DATABASE BancoBDP;
GO
USE BancoBDP;
IF OBJECT_ID('encrypted_accounts','U') IS NULL CREATE TABLE encrypted_accounts(record_id VARCHAR(64) PRIMARY KEY, bank_id INT NOT NULL, algorithm VARCHAR(40) NOT NULL, cipher_text NVARCHAR(MAX) NOT NULL, metadata_json NVARCHAR(MAX) NOT NULL, created_at_utc DATETIME2 NOT NULL);
GO
