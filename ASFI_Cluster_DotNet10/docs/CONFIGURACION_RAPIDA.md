# Configuración rápida — 3 PCs con Tailscale

## Reparto recomendado

- **PC principal i7-12700H (20 hilos):** `Asfi.Main.Api` + SQL Server. También procesa lotes localmente.
- **PC apoyo i7-12650H (16 hilos):** una copia de `Asfi.Worker.Api`.
- **PC apoyo i7-10750H (12 hilos):** otra copia idéntica de `Asfi.Worker.Api`.

`MaxParallelism = 0` significa **detectar automáticamente** `Environment.ProcessorCount`; no hay que cambiar 20/16/12 a mano.

## 1. En las tres PCs

1. Instalar .NET SDK 10.
2. Instalar Tailscale e iniciar sesión en la misma tailnet.
3. Copiar toda la carpeta del proyecto.
4. Copiar las **llaves reales de ASFI** a `config/asfi-keys.json` (mismo archivo en las 3 PCs).

## 2. Workers

En PC i7-12650H, abre `INICIAR_WORKERS.bat 1`. Solicita elevacion solo si falta la regla de firewall:

```bat
INICIAR_WORKERS.bat 1
```

En PC i7-10750H, abre `INICIAR_WORKERS.bat 2`:

```bat
INICIAR_WORKERS.bat 2
```

El BAT inicia un worker en la PC actual. Tailscale no permite iniciar automaticamente programas en otra PC, por lo que debe abrirse una vez en cada equipo de apoyo.

Comprueba desde la principal (cambia la IP por Tailscale):

```powershell
Invoke-RestMethod http://100.87.131.69:5201/api/worker/health
```

## 3. Principal

La forma más fácil es **no editar JSON**. Arranca la principal pasando las dos IP Tailscale:

```powershell
.\scripts\Start-Main.ps1 -Worker1Ip 100.87.131.69 -Worker2Ip 100.76.119.96
```

Las dos IP ya están guardadas y habilitadas en `Asfi.Main.Api/appsettings.json`, sección `Workers`. Puedes ejecutar `Start-Main.ps1` sin parámetros. Los parámetros del script permiten sobrescribirlas solo para esa ejecución, sin dejar variables de entorno en la terminal.

No cambies los 14 bancos si sus APIs corren en la PC principal en `5101..5114`. Si las mueves a otra máquina, cambia solo `BaseUrl`.

Ajusta SQL Server si no usas autenticación de Windows:

```json
"Asfi": "Server=localhost;Database=ASFI;User Id=sa;Password=TU_CLAVE;TrustServerCertificate=True"
```

Con las IP guardadas en `appsettings.json`, puedes iniciar simplemente con:

```powershell
.\scripts\Start-Main.ps1
```

Para ejecutar únicamente en la PC principal, cambia `Enabled` a `false` en ambos workers.
BCB y los 14 bancos se consultan por `localhost`; solo los workers usan Tailscale.

La integración con `BD` ya habilita `Asfi:InitializeDatabaseOnStartup` y `Database:InitializeOnStartup`. Ejecuta `INICIAR_TODO.bat` desde la raíz para crear las bases Docker antes de iniciar las APIs. La carga del CSV se realiza por separado con `CARGAR_CSV.bat`. Consulta [LEEME.md](../../LEEME.md).

## 4. Probar antes de lanzar

```powershell
Invoke-RestMethod http://localhost:5000/api/asfi/dependencies
```

Debe mostrar BCB, 14 bancos y los 2 workers.

## 5. Lanzar el procesamiento

Cuando estén instalados y configurados los motores, reinicia los bancos con `Start-All.ps1 -InitializeDatabases` y ASFI con `Start-Main.ps1 -InitializeDatabase`, o activa las opciones de inicialización en los JSON. Ajusta primero las cadenas de conexión a las instancias reales.

```powershell
$run = Invoke-RestMethod -Method Post http://localhost:5000/api/asfi/runs
$run
Invoke-RestMethod http://localhost:5000/api/asfi/runs/$($run.runId)
```

La principal crea lotes de 1000 registros. PC principal y los dos workers compiten por los lotes: el que termina antes recibe otro. Por eso no se divide 1/3 fijo.

## 6. Ajustes de rendimiento

Empieza con:
- `BatchSize`: **1000**.
- `LocalMaxParallelism`: **0**.
- workers `MaxParallelism`: **0**.

Luego prueba `BatchSize` 500, 1000, 2000 y conserva el mejor tiempo. SQL se escribe con `SqlBulkCopy`, no con un INSERT por registro.
