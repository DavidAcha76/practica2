# Conectividad

El arranque completo y la carga del CSV se explican en [LEEME.md](LEEME.md).

- `INICIAR_TODO.bat`: cinco motores Docker, BCB, 14 APIs bancarias y ASFI principal.
- `CARGAR_CSV.bat`: importacion cifrada, independiente del arranque.
- `VACIAR_BD.bat`: vaciado de los 14 bancos y ASFI, seguido del reinicio de los servicios.

| Servicio | URL |
|---|---|
| ASFI principal | `http://localhost:5000` |
| BCB | `http://localhost:5050/api/cotizacion` |
| Bancos 1 a 14 | `http://localhost:5101` a `http://localhost:5114` |
| Worker 1 | `http://100.87.131.69:5201` |
| Worker 2 | `http://100.76.119.96:5201` |

Los workers escuchan en `0.0.0.0:5201` en sus PCs. Las conexiones usan las IP Tailscale reales. Ambos pueden usar 5201 por estar en equipos diferentes.

La inicializacion de bases ya esta habilitada y las cadenas de conexion corresponden a `BD/.env`. SQL Server se consulta por TCP en `127.0.0.1,1433` con la autenticacion configurada en Docker.

`GET /api/asfi/dependencies` comprueba BCB, los 14 bancos y los workers; `GET /api/asfi/config-summary` muestra las URLs efectivas. El arranque espera los motores y prepara las bases, y comprueba las 15 conexiones antes de anunciar que los servicios estan listos. La importacion tambien verifica las 15 bases antes de escribir.

La prueba `scripts/Test-Launchers.ps1 -WithApis` verifica los BAT en una copia aislada y las conexiones HTTP locales con las 16 APIs reales. La prueba completa con escritura, vaciado y recarga sigue pendiente de instalar Docker Desktop con WSL 2; consulta el flujo de prueba en `LEEME.md`.
