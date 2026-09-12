CREATE DATABASE IF NOT EXISTS banco_mercantil CHARACTER SET utf8mb4;
CREATE TABLE IF NOT EXISTS banco_mercantil.encrypted_accounts(record_id VARCHAR(64) PRIMARY KEY,bank_id INT NOT NULL,algorithm VARCHAR(40) NOT NULL,cipher_text LONGTEXT NOT NULL,metadata_json LONGTEXT NOT NULL,created_at_utc DATETIME(6) NOT NULL);
CREATE DATABASE IF NOT EXISTS banco_economico CHARACTER SET utf8mb4;
CREATE TABLE IF NOT EXISTS banco_economico.encrypted_accounts(record_id VARCHAR(64) PRIMARY KEY,bank_id INT NOT NULL,algorithm VARCHAR(40) NOT NULL,cipher_text LONGTEXT NOT NULL,metadata_json LONGTEXT NOT NULL,created_at_utc DATETIME(6) NOT NULL);
CREATE DATABASE IF NOT EXISTS banco_pyme CHARACTER SET utf8mb4;
CREATE TABLE IF NOT EXISTS banco_pyme.encrypted_accounts(record_id VARCHAR(64) PRIMARY KEY,bank_id INT NOT NULL,algorithm VARCHAR(40) NOT NULL,cipher_text LONGTEXT NOT NULL,metadata_json LONGTEXT NOT NULL,created_at_utc DATETIME(6) NOT NULL);
