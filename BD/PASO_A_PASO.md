# Bases de datos integradas

El arranque de esta carpeta ya esta integrado con los tres proyectos de la raiz.

1. Instala Docker Desktop con WSL 2 y .NET SDK 10.
2. Abre `../INICIAR_TODO.bat` para crear las bases y arrancar todos los servicios locales.
3. Abre `../CARGAR_CSV.bat` para cargar `dataset.csv` cifrado, sin duplicar cuentas.

Consulta [LEEME.md](../LEEME.md) para los workers Tailscale, los puertos y las pruebas.

`BD/.env` es la configuracion de motores. `Bootstrap/` crea las bases y tablas segun los proyectos bancarios actuales y reutiliza su cifrado. No se necesitan Python ni sus conectores para usar los BAT.

Los SQL de `DB/` y los scripts de `docker-init/` son referencias del esquema anterior. Docker Compose no los monta ni los ejecuta. No los apliques a las bases del sistema actual: la distribucion antigua y las tablas `cuentas` son distintas de `encrypted_accounts`.

Los scripts Python auxiliares delegan en los mismos comandos usados por los BAT. Ya no contienen rutas de otra computadora ni cargan cuentas sin el cifrado de las APIs.
