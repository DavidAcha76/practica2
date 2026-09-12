# Cómo cambiar manualmente la configuración del BCB

Los **dos valores configurables solicitados** están en:

`BCB.Cotizaciones.Api/appsettings.json`

Contenido inicial:

```json
{
  "BCB": {
    "TipoCambioBase": 6.9600,
    "VigenciaSegundos": 15
  }
}
```

## 1. Cambiar el valor del boliviano respecto a 1 dólar

Modifica únicamente:

```json
"TipoCambioBase": 6.9600
```

Ejemplo, para usar `7.2500 BOB` por `1 USD`:

```json
"TipoCambioBase": 7.2500
```

El simulador genera cada nueva cotización alrededor de ese valor base, con una variación máxima de `±0.9999` y 4 decimales.

## 2. Cambiar la vigencia / tiempo de actualización

Modifica únicamente:

```json
"VigenciaSegundos": 15
```

Ejemplo, para actualizar cada 30 segundos:

```json
"VigenciaSegundos": 30
```

El valor debe ser un entero mayor que cero.

## Aplicar los cambios

Después de editar y guardar `appsettings.json`, reinicia la API:

```powershell
Ctrl + C
dotnet run
```

La configuración se carga al iniciar el proyecto. No se proporciona un endpoint para modificarla porque, para este entorno académico, el cambio debe hacerse manualmente en el archivo.

## Importante

No es necesario modificar ningún otro archivo para cambiar estos dos valores.
# Nota: desde esta versión la cotización es manual. `VigenciaSegundos` ya no se usa;
# para cambiar el valor en ejecución sigue las instrucciones de `ACTUALIZAR_COTIZACION.md`.
