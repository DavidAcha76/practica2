# Arquitectura

```text
Cliente/ASFI
    |
    | HTTP GET /api/cuentas
    v
API de Banco (x14)
    |
    | serializa y cifra antes de persistir
    v
IAccountCipher (algoritmo específico)
    |
    v
IEncryptedAccountRepository
    |
    +--> PostgreSQL
    +--> MySQL
    +--> SQL Server
    +--> MongoDB
    +--> Neo4j
```

La API nunca consulta un dato en claro desde la base porque el registro persistido ya es un `EncryptedAccountRecord`.

Campos persistidos:

- `recordId`: identificador técnico aleatorio del registro cifrado.
- `bankId`: identifica el banco.
- `algorithm`: algoritmo que debe usar ASFI.
- `cipherText`: carga cifrada.
- `metadataJson`: IV, nonce, tag, clave de sesión envuelta o clave pública efímera cuando corresponda.
- `createdAtUtc`: fecha técnica de creación.

El objeto real de cuenta (`CuentaId`, identificación, nombres, apellidos, número de cuenta y saldo USD) queda dentro del payload cifrado.
