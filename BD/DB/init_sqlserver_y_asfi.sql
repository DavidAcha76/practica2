-- ============================================================
-- SQL Server 2022 — Bancos 10-11 + BD Central ASFI
-- Banco 10: banco_fortaleza (AES-256)
-- Banco 11: banco_fie       (RSA-2048)
-- ASFI:     asfi_central    (BD central de conversión)
--
-- Ejecutar completo en SSMS conectado como pablo o sa
-- ============================================================

-- ── Crear BDs si no existen ──────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'banco_fortaleza')
    CREATE DATABASE banco_fortaleza;
GO
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'banco_fie')
    CREATE DATABASE banco_fie;
GO
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'asfi_central')
    CREATE DATABASE asfi_central;
GO

-- ════════════════════════════════════════════════════════════
-- BANCO FORTALEZA (Banco 10 — AES-256)
-- ════════════════════════════════════════════════════════════
USE banco_fortaleza;
GO

IF OBJECT_ID('cuentas', 'U') IS NOT NULL DROP TABLE cuentas;
GO

CREATE TABLE cuentas (
    nro                  INT            NOT NULL,
    nro_cuenta           NVARCHAR(20)   NOT NULL,
    identificacion_enc   NVARCHAR(MAX)  NOT NULL,
    nombres_enc          NVARCHAR(MAX)  NOT NULL,
    apellidos_enc        NVARCHAR(MAX)  NOT NULL,
    saldo_enc            NVARCHAR(MAX)  NOT NULL,
    saldo_bs             DECIMAL(15,4)  NULL,
    codigo_verificacion  CHAR(8)        NULL,
    algoritmo            NVARCHAR(20)   NOT NULL,
    created_at           DATETIME2      NOT NULL DEFAULT GETDATE(),
    convertido_at        DATETIME2      NULL,
    CONSTRAINT pk_cuentas_fortaleza PRIMARY KEY (nro)
);
GO

CREATE UNIQUE INDEX idx_nro_cuenta_fortaleza ON cuentas (nro_cuenta);
CREATE INDEX        idx_convertido_fortaleza ON cuentas (convertido_at);
GO

PRINT 'banco_fortaleza: OK';
GO

-- ════════════════════════════════════════════════════════════
-- BANCO FIE (Banco 11 — RSA-2048)
-- ════════════════════════════════════════════════════════════
USE banco_fie;
GO

IF OBJECT_ID('cuentas', 'U') IS NOT NULL DROP TABLE cuentas;
GO

CREATE TABLE cuentas (
    nro                  INT            NOT NULL,
    nro_cuenta           NVARCHAR(20)   NOT NULL,
    identificacion_enc   NVARCHAR(MAX)  NOT NULL,
    nombres_enc          NVARCHAR(MAX)  NOT NULL,
    apellidos_enc        NVARCHAR(MAX)  NOT NULL,
    saldo_enc            NVARCHAR(MAX)  NOT NULL,
    saldo_bs             DECIMAL(15,4)  NULL,
    codigo_verificacion  CHAR(8)        NULL,
    algoritmo            NVARCHAR(20)   NOT NULL,
    created_at           DATETIME2      NOT NULL DEFAULT GETDATE(),
    convertido_at        DATETIME2      NULL,
    CONSTRAINT pk_cuentas_fie PRIMARY KEY (nro)
);
GO

CREATE UNIQUE INDEX idx_nro_cuenta_fie ON cuentas (nro_cuenta);
CREATE INDEX        idx_convertido_fie ON cuentas (convertido_at);
GO

PRINT 'banco_fie: OK';
GO

-- ════════════════════════════════════════════════════════════
-- ASFI CENTRAL — BD de conversión monetaria
-- ════════════════════════════════════════════════════════════
USE asfi_central;
GO

-- ── Tabla bancos (catálogo fijo — nunca se trunca) ───────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'bancos')
BEGIN
    CREATE TABLE bancos (
        id_banco   SMALLINT      NOT NULL,
        nombre     NVARCHAR(80)  NOT NULL,
        algoritmo  NVARCHAR(20)  NOT NULL,
        motor_bd   NVARCHAR(20)  NOT NULL,
        api_url    NVARCHAR(100) NOT NULL,
        CONSTRAINT pk_bancos PRIMARY KEY (id_banco)
    );

    INSERT INTO bancos VALUES
        (1,  N'Banco Unión S.A.',                      N'César',    N'PostgreSQL', N'http://localhost:8001'),
        (2,  N'Banco Mercantil Santa Cruz S.A.',        N'Atbash',   N'PostgreSQL', N'http://localhost:8002'),
        (3,  N'Banco Nacional de Bolivia S.A.',         N'Vigenère', N'PostgreSQL', N'http://localhost:8003'),
        (4,  N'Banco de Crédito de Bolivia S.A.',       N'Playfair', N'PostgreSQL', N'http://localhost:8004'),
        (5,  N'Banco BISA S.A.',                        N'Hill',     N'PostgreSQL', N'http://localhost:8005'),
        (6,  N'Banco Ganadero S.A.',                    N'DES',      N'MySQL',      N'http://localhost:8006'),
        (7,  N'Banco Económico S.A.',                   N'3DES',     N'MySQL',      N'http://localhost:8007'),
        (8,  N'Banco Prodem S.A.',                      N'Blowfish', N'MySQL',      N'http://localhost:8008'),
        (9,  N'Banco Solidario S.A.',                   N'Twofish',  N'MySQL',      N'http://localhost:8009'),
        (10, N'Banco Fortaleza S.A.',                   N'AES',      N'SQLServer',  N'http://localhost:8010'),
        (11, N'Banco FIE S.A.',                         N'RSA',      N'SQLServer',  N'http://localhost:8011'),
        (12, N'Banco PYME de la Comunidad S.A.',        N'ElGamal',  N'MongoDB',    N'http://localhost:8012'),
        (13, N'Banco de Desarrollo Productivo S.A.M.',  N'ECC',      N'MongoDB',    N'http://localhost:8013'),
        (14, N'Banco de la Nación Argentina',           N'ChaCha20', N'Neo4j',      N'http://localhost:8014');

    PRINT 'Tabla bancos creada y poblada OK';
END
ELSE
    PRINT 'Tabla bancos ya existe — no se modificó';
GO

-- ── Tabla cuentas_convertidas ────────────────────────────────
IF OBJECT_ID('cuentas_convertidas', 'U') IS NOT NULL
    DROP TABLE cuentas_convertidas;
GO

CREATE TABLE cuentas_convertidas (
    id                   BIGINT          NOT NULL IDENTITY(1,1),
    nro                  INT             NOT NULL,
    nro_cuenta           NVARCHAR(20)    NOT NULL,
    ci                   NVARCHAR(20)    NOT NULL,
    nombres              NVARCHAR(100)   NOT NULL,
    apellidos            NVARCHAR(100)   NOT NULL,
    id_banco             SMALLINT        NOT NULL,
    saldo_usd_original   DECIMAL(15,4)   NOT NULL,
    saldo_bs             DECIMAL(15,4)   NOT NULL,
    tipo_cambio_aplicado DECIMAL(10,4)   NOT NULL,
    codigo_verificacion  CHAR(8)         NOT NULL,
    fecha_creacion       DATETIME2       NOT NULL DEFAULT GETDATE(),
    fecha_modificacion   DATETIME2       NOT NULL DEFAULT GETDATE(),
    worker_id            NVARCHAR(50)    NOT NULL,
    hash_integridad      CHAR(64)        NOT NULL,
    hmac_tc              NVARCHAR(100)   NOT NULL,
    origen               NVARCHAR(20)    NOT NULL DEFAULT 'SEEDER',
    CONSTRAINT pk_cuentas_convertidas PRIMARY KEY (id),
    CONSTRAINT fk_banco FOREIGN KEY (id_banco) REFERENCES bancos (id_banco)
);
GO

CREATE UNIQUE INDEX idx_cc_nro_cuenta_banco ON cuentas_convertidas (nro_cuenta, id_banco);
CREATE INDEX        idx_cc_id_banco         ON cuentas_convertidas (id_banco);
CREATE INDEX        idx_cc_ci               ON cuentas_convertidas (ci);
CREATE INDEX        idx_cc_fecha_mod        ON cuentas_convertidas (fecha_modificacion);
GO

PRINT 'asfi_central: todas las tablas creadas OK';
GO
