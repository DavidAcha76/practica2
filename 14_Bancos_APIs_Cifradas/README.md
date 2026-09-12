# 14 APIs Bancarias con datos cifrados — Práctica 2

Proyecto académico en **ASP.NET Core / .NET 10**. Contiene **14 APIs independientes**, una por banco, y una capa compartida de cifrado + persistencia.

## Qué hace

- Cada API recibe una cuenta en texto claro mediante `POST /api/cuentas` **solo para carga/prueba**.
- Antes de tocar la base de datos, serializa la cuenta completa y la cifra con el algoritmo asignado al banco.
- La base de datos guarda **solo el payload cifrado**, metadatos criptográficos (IV/nonce/tag/clave envuelta cuando aplica), `BancoId`, algoritmo, `RecordId` técnico y timestamp.
- `GET /api/cuentas` y `GET /api/cuentas/{recordId}` devuelven únicamente el registro cifrado.
- No existe endpoint de descifrado.
- No se implementó CORS, autenticación, autorización, HSTS ni redirección HTTPS, por pedido del entorno académico.

## Bancos

| ID | Banco | Algoritmo | Motor | Puerto |
|---:|---|---|---|---:|
| 1 | Banco Unión S.A. | Caesar | PostgreSQL | 5101 |
| 2 | Banco Mercantil Santa Cruz S.A. | Atbash | MySQL | 5102 |
| 3 | Banco Nacional de Bolivia S.A. (BNB) | Vigenere | SQLServer | 5103 |
| 4 | Banco de Crédito de Bolivia S.A. (BCP) | Playfair | MongoDB | 5104 |
| 5 | Banco BISA S.A. | Hill | Neo4j | 5105 |
| 6 | Banco Ganadero S.A. | DES | PostgreSQL | 5106 |
| 7 | Banco Económico S.A. | 3DES | MySQL | 5107 |
| 8 | Banco Prodem S.A. | Blowfish | SQLServer | 5108 |
| 9 | Banco Solidario S.A. | Twofish | MongoDB | 5109 |
| 10 | Banco Fortaleza S.A. | AES | Neo4j | 5110 |
| 11 | Banco FIE S.A. | RSA | PostgreSQL | 5111 |
| 12 | Banco PYME de la Comunidad S.A. | ElGamal | MySQL | 5112 |
| 13 | Banco de Desarrollo Productivo S.A.M. | ECC | SQLServer | 5113 |
| 14 | Banco de la Nación Argentina | ChaCha20 | MongoDB | 5114 |

## Motores usados

- PostgreSQL: bancos 1, 6 y 11.
- MySQL: bancos 2, 7 y 12.
- SQL Server: bancos 3, 8 y 13.
- MongoDB: bancos 4, 9 y 14.
- Neo4j: bancos 5 y 10.

Esto cumple la idea de mantener la **capa de cifrado independiente del motor**: el algoritmo se ejecuta en C# antes de persistir. Por ello la misma estructura de API funciona con motores relacionales y no relacionales.

## Requisitos

- .NET 10 SDK.
- Los motores que quiera ejecutar: PostgreSQL, MySQL, SQL Server, MongoDB y Neo4j.
- Ejecute los 5 scripts de `databases/` y ajuste credenciales en cada `appsettings.json` si sus usuarios/contraseñas difieren.

## Restaurar

```powershell
dotnet restore .\BancosCifrados.sln
```

## Ejecutar una API

Ejemplo Banco Unión:

```powershell
dotnet run --project .\src\BancoUnion.Api\BancoUnion.Api.csproj
```

Abra `http://localhost:5101/api/banco/info`.

La configuración actual tiene `Database:InitializeOnStartup=true` y apunta a los motores Docker de `BD`. Usa `INICIAR_TODO.bat` desde la raíz para crear las bases antes de arrancar las APIs, y `CARGAR_CSV.bat` para importar las cuentas cifradas. Consulta [LEEME.md](../LEEME.md). Para una prueba exclusivamente de conectividad sin bases, puedes sobrescribir temporalmente `Database:InitializeOnStartup=false`.

## Prueba rápida

```http
POST http://localhost:5101/api/cuentas/seed-demo
GET  http://localhost:5101/api/cuentas
```

Cada proyecto incluye un archivo `.http` listo para Visual Studio / VS Code REST Client.

## Ejecutar las 14

```powershell
.\Start-All.ps1
```

Cada API queda en su puerto 5101..5114. El script compila la solución una vez, comprueba que los puertos estén libres y lanza las 14 APIs en segundo plano; muestra sus PID y guarda logs en `.logs/`. Comprueba todas con `.\Test-All.ps1`.

## Cifrado clásico y datos arbitrarios

César, Atbash, Vigenère, Playfair y Hill tradicionalmente trabajan con alfabetos limitados. Para poder cifrar **todo el JSON** (números, decimales, tildes, signos, etc.) sin perder información, el proyecto primero transforma los bytes UTF-8 a un alfabeto académico de 16 símbolos `A..P` (dos símbolos por byte). Luego aplica el algoritmo clásico sobre ese alfabeto. El proceso es reversible y evita dejar números o símbolos sensibles sin cifrar.

## Cifrados modernos

- DES y 3DES: CBC + PKCS7.
- Blowfish y Twofish: CBC + PKCS7 mediante BouncyCastle.
- AES: AES-256-GCM.
- RSA: cifrado híbrido RSA-OAEP-SHA256 + AES-256-GCM.
- ElGamal: cifrado híbrido ElGamal + AES-256-GCM.
- ECC: ECDH P-256 + SHA-256 KDF + AES-256-GCM.
- ChaCha20: ChaCha20-Poly1305.

Los cifrados híbridos son necesarios porque RSA/ElGamal/ECC no deben cifrar directamente documentos JSON arbitrariamente grandes.

## Claves para ASFI

Revise `asfi-keys/` y `docs/CLAVES_ASFI.md`. Las claves están incluidas únicamente porque esta práctica es académica. En un sistema real no deberían viajar dentro del ZIP ni almacenarse en texto claro.

## Importante sobre Neo4j

La configuración por defecto usa una sola base local `neo4j`, diferenciando bancos con `bankId`. Neo4j Community normalmente trabaja con una sola base; si usa Enterprise/Aura con múltiples bases, puede poner un `DatabaseName` independiente en los `appsettings.json` de BISA y Fortaleza.
