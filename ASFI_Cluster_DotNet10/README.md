# ASFI Cluster — API principal + API Worker (.NET 10)

Solución académica en **C# / ASP.NET Core .NET 10 + SQL Server**, pensada para ejecutar el barrido ASFI con 3 computadoras conectadas por Tailscale.

## Proyectos

### `Asfi.Main.Api`
Corre en el i7-12700H (20 hilos lógicos). Hace de coordinador, consulta BCB + 14 APIs bancarias, procesa también localmente, reparte lotes a workers, escribe por lotes en SQL Server y genera auditoría JSONL.

### `Asfi.Worker.Api`
Es **el mismo proyecto** para las dos PCs de apoyo. Recibe un lote por `POST /api/worker/process`, usa automáticamente los hilos lógicos de esa PC y devuelve resultados a la principal. No necesita SQL Server.

## Flujo

```text
14 APIs bancos (5101..5114)        BCB (5050)
             \                       /
              \                     /
               v                   v
              ASFI MAIN / i7-12700H
          Coordinator + Worker local + SQL
                 |                 |
        Tailscale|                 |Tailscale
                 v                 v
       Worker i7-12650H     Worker i7-10750H
          16 hilos aprox.      12 hilos
                 \                 /
                  ---- resultados --
                         |
                    SqlBulkCopy
                         |
                    SQL Server ASFI
```

## Arranque

Lee primero `docs/CONFIGURACION_RAPIDA.md`. La forma más corta con dos workers Tailscale es:

BCB (`localhost:5050`), bancos (`localhost:5101..5114`) y ASFI principal (`localhost:5000`) se ejecutan en la misma PC. Los workers ya apuntan a `100.87.131.69:5201` y `100.76.119.96:5201` y están habilitados.
La inicialización de SQL está habilitada y apunta al contenedor de `BD`. Usa `INICIAR_TODO.bat` desde la raíz para arrancar las bases y todos los servicios. `CARGAR_CSV.bat` importa los datos de forma separada. Consulta [LEEME.md](../LEEME.md).

```powershell
# En cada PC de apoyo (abre INICIAR_WORKERS.bat 1 o 2)
.\INICIAR_WORKERS.bat 1
# En la segunda PC:
.\INICIAR_WORKERS.bat 2

# En la PC principal (usa aquí las IP Tailscale reales)
.\scripts\Start-Main.ps1 -Worker1Ip 100.87.131.69 -Worker2Ip 100.76.119.96
```

El mismo `Asfi.Worker.Api` se copia en ambas PCs; no son proyectos distintos.

## Endpoints principal

- `POST /api/asfi/runs` — inicia una corrida.
- `GET /api/asfi/runs` — lista corridas de esta ejecución.
- `GET /api/asfi/runs/{id}` — progreso, tiempo, throughput y reparto por nodo.
- `GET /api/asfi/runs/{id}/results?take=100` — muestra resultados SQL.
- `GET /api/asfi/dependencies` — prueba BCB, bancos y workers.
- `GET /api/asfi/config-summary` — configuración efectiva no sensible.

## Endpoints worker

- `GET /api/worker/health`
- `POST /api/worker/process`

## Rendimiento aplicado

- Los 14 bancos se consultan en paralelo.
- BCB se consulta una vez por corrida para usar una cotización consistente.
- Los registros se agrupan en lotes (1000 por defecto).
- Los lotes se reparten dinámicamente entre 3 nodos: no se asigna 33/33/33.
- Cada nodo usa `Parallel.ForEachAsync` con `Environment.ProcessorCount` cuando `MaxParallelism=0`.
- SQL Server recibe lotes mediante `SqlBulkCopy`.
- Si un worker se cae, su lote hace fallback al procesador local.
- La auditoría se genera en `Asfi.Main.Api/audit/run-<id>.jsonl`.

## Antes de procesar datos reales de tus APIs

**Reemplaza las claves de ejemplo** de `config/asfi-keys.json` por las mismas llaves que se usaron al cifrar en los bancos. Sin eso, ASFI puede conectarse a todas las APIs pero no puede descifrar correctamente.

## Nota sobre las APIs bancarias actuales

El contrato TXT suministrado tiene lectura/creación de cuentas, pero no trae endpoints de actualización ni verificación posterior. Ver `docs/INTEGRACION_APIS.md`.
