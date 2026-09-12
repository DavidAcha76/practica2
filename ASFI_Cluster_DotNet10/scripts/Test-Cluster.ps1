param(
    [string]$MainUrl="http://localhost:5000",
    [switch]$Run
)
$ErrorActionPreference = "Stop"
$MainUrl = $MainUrl.TrimEnd('/')
Write-Host "Dependencias:" -ForegroundColor Cyan
$dependencies = Invoke-RestMethod "$MainUrl/api/asfi/dependencies" -TimeoutSec 15
$dependencies | ConvertTo-Json -Depth 6
if (@($dependencies | Where-Object { -not $_.ok }).Count -gt 0) {
    throw 'Hay dependencias sin conexion. Revisa las URLs y los servicios indicados.'
}
if (-not $Run) {
    Write-Host 'Conectividad OK. Para procesar datos con las bases listas, usa -Run.' -ForegroundColor Green
    return
}
Write-Host "Creando corrida..." -ForegroundColor Cyan
$run = Invoke-RestMethod -Method Post "$MainUrl/api/asfi/runs"
$run | ConvertTo-Json
Write-Host "Consulta estado: $MainUrl/api/asfi/runs/$($run.runId)"
