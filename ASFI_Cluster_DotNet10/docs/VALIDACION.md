# Validación realizada antes de entregar

Se revisó estáticamente la solución generada:

- JSON de configuración válido.
- XML de ambos `.csproj` válido.
- estructura de solución con exactamente dos APIs ejecutables: `Asfi.Main.Api` y `Asfi.Worker.Api`;
- referencias compartidas de C# incluidas por `Compile Include`;
- balance de llaves/paréntesis/corchetes de los archivos C# revisado;
- rutas BCB y bancos contrastadas con los TXT suministrados;
- scripts PowerShell preparados para una principal y dos workers por Tailscale.

El entorno usado para generar este ZIP no dispone del SDK de .NET, por lo que aquí no fue posible ejecutar `dotnet restore` / `dotnet build`. En tu PC, la primera verificación debe ser:

```powershell
dotnet --version
dotnet restore .\ASFI.Cluster.sln
dotnet build .\ASFI.Cluster.sln -c Release
```

Después ejecuta `scripts\Test-Cluster.ps1` cuando BCB, bancos y workers estén levantados.
