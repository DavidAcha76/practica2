# API académica BCB - Cotización USD/BOB

Proyecto independiente que simula **únicamente el servicio del Banco Central de Bolivia (BCB)** requerido por la práctica.

No contiene bancos, ASFI, bases de datos, cifrado ni lógica de conversión masiva. Su única responsabilidad es publicar una cotización USD/BOB y renovarla periódicamente.

## Tecnología

- ASP.NET Core / .NET 10
- Minimal API
- Sin paquetes NuGet externos
- Sin base de datos
- Sin CORS
- Sin autenticación ni autorización
- Sin HTTPS redirection

## Comportamiento

- Tipo de cambio base inicial: `6.9600 BOB` por `1 USD`.
- Vigencia inicial: `15 segundos`.
- Cada vez que vence la vigencia se genera una nueva cotización.
- La cotización fluctúa entre `-0.9999` y `+0.9999` respecto al tipo de cambio base.
- Se mantiene una precisión máxima de 4 decimales.
- El servicio SSE notifica automáticamente las nuevas cotizaciones a los clientes conectados.

## Ejecutar

Necesitas el SDK de .NET 10.

```powershell
cd BCB.Cotizaciones.Api
dotnet restore
dotnet run
```

Con el perfil incluido, la aplicación queda disponible en:

- Página HTML de prueba: `http://localhost:5050/`
- Cotización actual: `http://localhost:5050/api/cotizacion`
- Stream de cotizaciones: `http://localhost:5050/api/cotizacion/stream`
- Configuración activa: `http://localhost:5050/api/configuracion`

El puerto `5050` también está fijado en `appsettings.json`, por lo que se conserva al ejecutar sin perfil de lanzamiento o al publicar. ASFI consulta `http://localhost:5050/api/cotizacion` desde esta misma PC. Consulta [CONECTIVIDAD.md](../CONECTIVIDAD.md) para el arranque conjunto.

Antes de arrancar, define `BCB__ClaveAdministracion` en esa terminal como indica [ACTUALIZAR_COTIZACION.md](ACTUALIZAR_COTIZACION.md). El servicio requiere esa configuración incluso para inicializar la consulta pública de cotización.

## Respuesta de ejemplo

```json
{
  "fuente": "Banco Central de Bolivia - Simulación académica",
  "monedaBase": "USD",
  "monedaCotizada": "BOB",
  "valorBolivianoPorDolar": 6.9600,
  "vigenciaSegundos": 15,
  "fechaActualizacion": "2026-09-10T22:00:00-04:00",
  "proximaActualizacion": "2026-09-10T22:00:15-04:00",
  "version": 1
}
```

## HTML de prueba

El archivo `wwwroot/index.html` usa la propia API mediante rutas del mismo origen:

- `fetch('/api/cotizacion')` para consulta manual.
- `EventSource('/api/cotizacion/stream')` para recibir las actualizaciones automáticas.

Esto permite probarlo en el navegador **sin configurar CORS**.

Consulta `CAMBIAR_CONFIGURACION.md` para cambiar manualmente los dos valores solicitados.
