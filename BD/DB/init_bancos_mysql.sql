-- ============================================================
-- MySQL 8.0 — Bancos 6-9
-- Banco 6:  banco_ganadero  (DES)
-- Banco 7:  banco_economico (3DES)
-- Banco 8:  banco_prodem    (Blowfish)
-- Banco 9:  banco_solidario (Twofish)
--
-- Paso 1 — Crear las BDs:
--   mysql -uroot -p'192837465Po.' -e "CREATE DATABASE IF NOT EXISTS banco_ganadero;"
--   mysql -uroot -p'192837465Po.' -e "CREATE DATABASE IF NOT EXISTS banco_economico;"
--   mysql -uroot -p'192837465Po.' -e "CREATE DATABASE IF NOT EXISTS banco_prodem;"
--   mysql -uroot -p'192837465Po.' -e "CREATE DATABASE IF NOT EXISTS banco_solidario;"
--
-- Paso 2 — Crear tabla en cada BD:
--   mysql -uroot -p'192837465Po.' banco_ganadero  < init_bancos_mysql.sql
--   mysql -uroot -p'192837465Po.' banco_economico < init_bancos_mysql.sql
--   mysql -uroot -p'192837465Po.' banco_prodem    < init_bancos_mysql.sql
--   mysql -uroot -p'192837465Po.' banco_solidario < init_bancos_mysql.sql
-- ============================================================

DROP TABLE IF EXISTS cuentas;

CREATE TABLE cuentas (
    nro                  INT            NOT NULL,
    nro_cuenta           VARCHAR(20)    NOT NULL,
    identificacion_enc   TEXT           NOT NULL,
    nombres_enc          TEXT           NOT NULL,
    apellidos_enc        TEXT           NOT NULL,
    saldo_enc            TEXT           NOT NULL,
    saldo_bs             DECIMAL(15,4)  NULL,
    codigo_verificacion  CHAR(8)        NULL,
    algoritmo            VARCHAR(20)    NOT NULL,
    created_at           DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    convertido_at        DATETIME       NULL,
    PRIMARY KEY (nro),
    UNIQUE KEY idx_nro_cuenta (nro_cuenta)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX idx_convertido ON cuentas (convertido_at);

SELECT 'Tabla cuentas creada OK en MySQL' AS status;
