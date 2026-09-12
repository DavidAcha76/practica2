param([string]$ServerHost = "localhost")

$ErrorActionPreference = "Stop"
$manifest = Get-Content (Join-Path $PSScriptRoot 'project-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$failures = 0
foreach ($bank in $manifest.banks) {
    $url = [UriBuilder]::new('http', $ServerHost, $bank.port, '/api/banco/info').Uri.AbsoluteUri
    try {
        $info = Invoke-RestMethod $url -TimeoutSec 5
        if ($info.bancoId -ne $bank.id) { throw "Se esperaba BancoId $($bank.id), se recibio $($info.bancoId)." }
        Write-Host "OK $url - $($info.nombre) / $($info.algoritmo) / $($info.baseDeDatos)" -ForegroundColor Green
    } catch {
        $failures++
        Write-Host "FALLO $url - $($_.Exception.Message)" -ForegroundColor Red
    }
}
if ($failures -gt 0) { throw "$failures APIs bancarias no superaron la prueba de conectividad." }
