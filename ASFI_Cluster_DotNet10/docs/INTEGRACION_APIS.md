# Integración con las APIs entregadas

## Banco Central

La configuración usa exactamente:

```text
GET http://localhost:5050/api/cotizacion
```

El parser admite respuestas JSON con nombres habituales como `cotizacion`, `tipoCambio`, `rate`, `valor`, `oficial`, `venta` o `compra`.

## Bancos

Se configuraron exactamente los puertos `5101` a `5114` y estos endpoints existentes:

```text
GET /api/banco/info
GET /api/cuentas
GET /api/cuentas/{id}
POST /api/cuentas
POST /api/cuentas/seed-demo
```

ASFI utiliza **GET /api/cuentas** para leer los registros cifrados. El adaptador acepta un array JSON directo o un array contenido en `cuentas`, `items`, `data`, `results` o `records`.

## Limitación contractual importante

El TXT suministrado de los bancos **no define un endpoint para actualizar `SaldoBs + CodigoVerificacion` ni uno para validarlos**. Por eso este proyecto NO inventa una URL que no existe. La parte central ASFI genera el código, guarda USD/Bs en SQL Server y audita el proceso; cuando el equipo de APIs añada endpoints de actualización/verificación, `BankOptions` ya tiene `UpdateBatchEndpoint` y `VerifyBatchEndpoint` para integrarlos sin cambiar las URLs de lectura.

## Llavero

Las APIs bancarias entregan datos cifrados, pero los TXT no contienen las llaves. Copia al archivo `config/asfi-keys.json` las claves/PEM reales usadas por las APIs bancarias. El archivo incluido solo es plantilla académica.
