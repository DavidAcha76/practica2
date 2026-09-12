-- ============================================================
-- PostgreSQL — Bancos 1-5
-- Banco 1:  banco_union     (César)
-- Banco 2:  banco_mercantil (Atbash)
-- Banco 3:  banco_bnb       (Vigenère)
-- Banco 4:  banco_bcp       (Playfair)
-- Banco 5:  banco_bisa      (Hill)
--
-- Paso 1 — Crear las BDs (ejecutar como superusuario):
--   psql -U postgres -c "CREATE DATABASE banco_union;"
--   psql -U postgres -c "CREATE DATABASE banco_mercantil;"
--   psql -U postgres -c "CREATE DATABASE banco_bnb;"
--   psql -U postgres -c "CREATE DATABASE banco_bcp;"
--   psql -U postgres -c "CREATE DATABASE banco_bisa;"
--
-- Paso 2 — Crear tabla en cada BD:
--   psql -U postgres -d banco_union     -f init_bancos_postgresql.sql
--   psql -U postgres -d banco_mercantil -f init_bancos_postgresql.sql
--   psql -U postgres -d banco_bnb       -f init_bancos_postgresql.sql
--   psql -U postgres -d banco_bcp       -f init_bancos_postgresql.sql
--   psql -U postgres -d banco_bisa      -f init_bancos_postgresql.sql
-- ============================================================

DROP TABLE IF EXISTS cuentas;

CREATE TABLE cuentas (
    nro                  INTEGER        NOT NULL,
    nro_cuenta           VARCHAR(20)    NOT NULL,
    identificacion_enc   TEXT           NOT NULL,
    nombres_enc          TEXT           NOT NULL,
    apellidos_enc        TEXT           NOT NULL,
    saldo_enc            TEXT           NOT NULL,
    saldo_bs             DECIMAL(15,4)  NULL,
    codigo_verificacion  CHAR(8)        NULL,
    algoritmo            VARCHAR(20)    NOT NULL,
    created_at           TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    convertido_at        TIMESTAMPTZ    NULL,
    CONSTRAINT pk_cuentas PRIMARY KEY (nro)
);

CREATE UNIQUE INDEX idx_nro_cuenta ON cuentas (nro_cuenta);
CREATE INDEX        idx_convertido ON cuentas (convertido_at);

SELECT 'Tabla cuentas creada OK en PostgreSQL' AS status;
