# Ejecutar la practica

En la PC principal utiliza estos tres archivos:

1. **`INICIAR_TODO.bat`**: inicia Docker, espera los cinco motores, configura y crea las bases y tablas que necesitan las APIs, compila e inicia BCB, los 14 bancos y ASFI. Comprueba las conexiones. No carga el CSV ni inicia conversiones.
2. **`CARGAR_CSV.bat`**: carga `BD/dataset.csv` en las bases bancarias usando el cifrado real de cada banco. Ejecuta primero el BAT de servicios. También puedes arrastrar otro CSV encima de este BAT.
3. **`VACIAR_BD.bat`**: detiene los servicios, vacía los 14 bancos y ASFI y reinicia los servicios para permitir una nueva carga desde cero.

Las ventanas muestran el resultado y esperan una tecla. Los servicios continúan en segundo plano; sus logs están en `.logs/`. Si vuelves a abrir el BAT de servicios, reutiliza los procesos que ya inició.

El inicio y la consulta de estado comprueban el acceso a las 15 bases, además de las respuestas HTTP. Repetir el inicio también levanta los contenedores que se hayan detenido. Antes de importar se valida el CSV, se comprueban los cifrados y se revisan las 15 bases; un fallo en estas comprobaciones detiene la carga antes de escribir.

## Requisitos de la PC principal

- .NET **SDK 10** (ya disponible en el equipo revisado).
- **Docker Desktop con WSL 2**, configurado para contenedores Linux. La primera instalación de WSL puede requerir permisos de administrador y reiniciar Windows. Consulta la [instalación oficial de Docker para Windows](https://docs.docker.com/desktop/setup/install/windows-install/).
- Internet en el primer arranque para descargar imágenes y paquetes NuGet.

En el equipo revisado **Docker Desktop y WSL aún no están instalados**. El BAT detecta esta situación y muestra el requisito pendiente; no informa un arranque exitoso sin bases. Una vez instalados, abre Docker Desktop y completa su configuración inicial una vez. Después el BAT puede iniciarlo automáticamente.

## Workers en las otras PCs

Copia la carpeta completa **`ASFI_Cluster_DotNet10` actualizada**, incluidas `Shared/` y `config/`, a cada PC de apoyo. Instala .NET SDK 10, conecta Tailscale y abre **`INICIAR_WORKER.bat`** allí.

| PC | Dirección que usa ASFI |
|---|---|
| Worker 1 | `http://100.87.131.69:5201` |
| Worker 2 | `http://100.76.119.96:5201` |

El BAT del worker solicita elevación de Windows una vez si falta la regla de firewall del puerto 5201. Las reglas de Tailscale también deben permitir esa conexión. El BAT de la principal comprueba esos equipos; no puede iniciar procesos en ellos sin un servicio de administración remota. ASFI conserva el procesamiento local y el fallback si un worker no responde.

El inicio principal sincroniza `config/asfi-keys.json` con las claves de las APIs bancarias. Si cambias las claves de un banco, vuelve a copiar ese archivo a los workers y reinícialos.

## Datos y conexiones

`BD/.env` contiene los puertos y las credenciales de los contenedores. El inicio propaga esos valores a los `appsettings.json` y genera `BD/connection_strings.env` como referencia. Los volúmenes de Docker conservan las bases entre arranques. Cambiar una contraseña en `.env` no cambia la contraseña de un volumen ya inicializado.

| Motor | Puerto local | Bancos |
|---|---:|---|
| PostgreSQL | 5432 | 1 Unión, 6 Ganadero, 11 FIE |
| MySQL | 3307 | 2 Mercantil, 7 Económico, 12 PYME |
| SQL Server | 1433 | 3 BNB, 8 Prodem, 13 BDP y ASFI |
| MongoDB | 27017 | 4 BCP, 9 Solidario, 14 Nación Argentina |
| Neo4j | 7687 / 7474 | 5 BISA y 10 Fortaleza, separados por BancoId |

BCB usa `localhost:5050`, ASFI `localhost:5000` y las 14 APIs bancarias `localhost:5101` a `localhost:5114`. El BAT genera una clave administrativa aleatoria para BCB y la conserva en `.runtime/bcb-admin.key`; no pide escribirla en cada arranque.

## Carga del CSV

La cabecera requerida es `Nro,Identificacion,Nombres,Apellidos,NroCuenta,IdBanco,Saldo`. Se admite la línea inicial `Actualizado ...` del archivo incluido. Se conservan los ceros iniciales de las cuentas y los identificadores.

El archivo incluido contiene **123786 cuentas válidas** para los 14 bancos y **2 filas con IdBanco 15 y 19**. Las filas de bancos desconocidos se excluyen con un reporte en `.runtime/csv-rejected.csv`; el CSV original se conserva. Las filas con formato inválido o cuentas repetidas con datos distintos detienen la validación antes de escribir.

La carga cifra el JSON completo con la misma implementación de las APIs y escribe lotes en `encrypted_accounts` / `EncryptedAccount`. ASFI lee esas mismas estructuras. Una cuenta se identifica por banco y número de cuenta: repetir o reanudar la carga omite las ya importadas. No reemplaza saldos existentes ni borra datos. Si una carga falla a mitad, puede reanudarse con el mismo BAT.

## Vaciar los 14 bancos y ASFI

Abre **`VACIAR_BD.bat`** para eliminar los datos de los **15 destinos: bancos 1 al 14 y ASFI**. El BAT ejecuta el vaciado directamente: detiene las APIs, compila todos los proyectos, comprueba el acceso a todos los destinos, elimina las cuentas cifradas y los datos de ASFI, verifica que quedaron vacíos y reinicia los servicios. Si falla la compilación, no empieza a borrar. Después puedes abrir `CARGAR_CSV.bat` para cargar desde cero.

En ASFI vacía `Cuentas`, `ConversionResultados`, `ConversionRuns` y `Bancos`; al reiniciar, la aplicación vuelve a crear únicamente las 14 entradas del catálogo `Bancos`. El reinicio también elimina el estado de corridas que estaba en memoria. Se conservan el CSV original, las claves, la estructura de las bases y sus índices. BISA y Fortaleza comparten la base física `neo4j`: se vacían ambos por sus identificadores 5 y 10.

El vaciado usa el mismo bloqueo que el arranque y la importación, y requiere que las bases ya hayan sido inicializadas. Si falla algún motor durante el borrado, muestra cuántos destinos se completaron y permite repetirlo; no hay una transacción global entre las 15 bases. Los servicios se reinician únicamente después de verificar el vaciado completo.

Para revisar los 15 destinos sin conectarse ni borrar datos:

```powershell
dotnet run --project .\BD\Bootstrap\Bootstrap.csproj -- clear-plan .
```

Los archivos SQL de `BD/DB` corresponden al esquema anterior y se conservan como referencia; el arranque no los ejecuta porque incluyen otra distribución de bancos y operaciones que eliminan tablas. Los auxiliares Python ahora delegan en el inicializador común y no requieren los conectores Python anteriores.

## Iniciar una conversión de ASFI

Después de cargar el CSV, en PowerShell:

```powershell
$run = Invoke-RestMethod -Method Post http://localhost:5000/api/asfi/runs
Invoke-RestMethod "http://localhost:5000/api/asfi/runs/$($run.runId)"
Invoke-RestMethod "http://localhost:5000/api/asfi/runs/$($run.runId)/results?take=100"
```

La conversión se inicia por separado de los BAT. Guarda resultados consolidados en SQL Server `ASFI` y auditoría en `ASFI_Cluster_DotNet10/Asfi.Main.Api/audit`.

## Detener o consultar

Desde la raíz, sin borrar las bases:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Run-Stack.ps1 -Action Status
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Run-Stack.ps1 -Action Stop
```

El script detiene únicamente los procesos que registró, comprobando su ejecutable y hora de inicio. Los workers se detienen en sus respectivas PCs.

## Validación disponible

```powershell
dotnet run --project .\BD\Bootstrap\Bootstrap.csproj -- verify .
```

Comprueba los 14 algoritmos pasando por el adaptador y el procesador reales de ASFI, y valida el CSV completo sin conectarse a las bases. La prueba con los cinco motores requiere Docker en ejecución.

Para repetir las pruebas de los BAT sin tocar las bases:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Launchers.ps1
```

Ejecuta los tres BAT en una carpeta aislada dentro de `.logs/`, con Docker y servicios simulados. Comprueba inicio, segundo inicio, importación, vaciado, nueva importación, rutas con espacios, bloqueo simultáneo y propagación de errores. Con los proyectos compilados, añade `-WithApis` para iniciar también las 16 APIs reales en puertos temporales y comprobar BCB y los 14 bancos desde ASFI. Esta opción deshabilita la inicialización de bases y los workers remotos solo en los procesos de prueba; los detiene al terminar.

Revisión realizada: **45 comprobaciones correctas** con `-WithApis`, compilación sin errores de los tres proyectos y Bootstrap, validación de los 14 cifrados y del CSV, y plan de vaciado con 15 destinos. Las dependencias bancarias mantienen avisos NuGet preexistentes. **No se ha validado todavía la importación, conversión persistida, vaciado y recarga contra los cinco motores**, porque Docker Desktop y WSL no están instalados en este equipo. Tampoco se verificaron los workers remotos en esta prueba.

Cuando Docker esté disponible, el flujo de prueba es:

1. Abrir `INICIAR_TODO.bat` y esperar los 15 mensajes `BD OK`.
2. Abrir `CARGAR_CSV.bat` y esperar el resumen de carga. Repetirlo debe mostrar cero cuentas nuevas.
3. Iniciar la conversión con el comando anterior y consultar el estado hasta `Completado`, con `failedRecords` igual a cero.
4. Abrir `VACIAR_BD.bat`, comprobar `Vaciado verificado: 15/15` y esperar el reinicio. ASFI conserva únicamente las 14 entradas del catálogo `Bancos`.
5. Abrir `CARGAR_CSV.bat` otra vez: el CSV incluido debe cargar nuevamente 123786 cuentas. Una nueva conversión debe finalizar sin errores.
