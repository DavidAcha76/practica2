-- Ejecutar con psql como usuario con permiso CREATE DATABASE.
SELECT 'CREATE DATABASE banco_union' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname='banco_union')\gexec
\connect banco_union
CREATE TABLE IF NOT EXISTS encrypted_accounts(record_id VARCHAR(64) PRIMARY KEY,bank_id INTEGER NOT NULL,algorithm VARCHAR(40) NOT NULL,cipher_text TEXT NOT NULL,metadata_json TEXT NOT NULL,created_at_utc TIMESTAMPTZ NOT NULL);
SELECT 'CREATE DATABASE banco_ganadero' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname='banco_ganadero')\gexec
\connect banco_ganadero
CREATE TABLE IF NOT EXISTS encrypted_accounts(record_id VARCHAR(64) PRIMARY KEY,bank_id INTEGER NOT NULL,algorithm VARCHAR(40) NOT NULL,cipher_text TEXT NOT NULL,metadata_json TEXT NOT NULL,created_at_utc TIMESTAMPTZ NOT NULL);
SELECT 'CREATE DATABASE banco_fie' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname='banco_fie')\gexec
\connect banco_fie
CREATE TABLE IF NOT EXISTS encrypted_accounts(record_id VARCHAR(64) PRIMARY KEY,bank_id INTEGER NOT NULL,algorithm VARCHAR(40) NOT NULL,cipher_text TEXT NOT NULL,metadata_json TEXT NOT NULL,created_at_utc TIMESTAMPTZ NOT NULL);
