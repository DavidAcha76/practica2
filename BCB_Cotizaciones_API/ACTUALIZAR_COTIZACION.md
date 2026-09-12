# Actualizar manualmente la cotización

La cotización ya no se modifica automáticamente. Las APIs consumidoras solo deben llamar a `GET /api/cotizacion` y no reciben permisos de escritura.

## 1. Define la clave administrativa

Antes de iniciar la API, en PowerShell define una clave que solo conocerá quien administre el servicio:

```powershell
$env:BCB__ClaveAdministracion = "cambia-esta-clave-por-una-secreta"
dotnet run --project BCB.Cotizaciones.Api
```

La variable se usa solo en esa terminal. No la agregues a `appsettings.json` ni la compartas con las APIs consumidoras.

## 2. Envía el nuevo valor

Desde otra terminal, ejecuta:

```powershell
$clave = "cambia-esta-clave-por-una-secreta"
Invoke-RestMethod -Method Put `
  -Uri "http://localhost:5050/api/admin/cotizacion" `
  -Headers @{ "X-Api-Key" = $clave } `
  -ContentType "application/json" `
  -Body '{"valorBolivianoPorDolar":7.2500}'
```

El valor debe ser mayor que cero y se redondea a cuatro decimales. La respuesta contiene la cotización nueva y los clientes conectados al stream SSE reciben la actualización inmediatamente.

## Seguridad

Solo `PUT /api/admin/cotizacion` puede cambiar el valor y exige la clave del encabezado `X-Api-Key`. `GET /api/cotizacion`, `GET /api/cotizacion/stream` y `GET /api/configuracion` permanecen disponibles para lectura.

En un despliegue fuera de tu equipo, publica la API con HTTPS y configura `BCB__ClaveAdministracion` en el entorno del servidor.
