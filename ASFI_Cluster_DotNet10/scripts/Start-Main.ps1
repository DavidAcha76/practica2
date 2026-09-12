param(
  [Parameter(Mandatory=$false)][string]$Worker1Ip = "",
  [Parameter(Mandatory=$false)][string]$Worker2Ip = "",
  [ValidateRange(1,65535)][int]$WorkerPort = 5201,
  [ValidateRange(0,65535)][int]$Worker2Port = 0,
  [Parameter(Mandatory=$false)][string]$SqlConnection = "",
  [string]$BanksHost = "",
  [string]$BcbHost = "",
  [ValidateRange(1,65535)][int]$BcbPort = 5050,
  [string]$ListenAddress = "localhost",
  [ValidateRange(1,65535)][int]$Port = 5000,
  [switch]$InitializeDatabase
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent
$listenUrl = [UriBuilder]::new('http', $ListenAddress, $Port).Uri.GetLeftPart([UriPartial]::Authority)
$appArguments = @('--urls', $listenUrl)
if ($Worker2Port -eq 0) { $Worker2Port = $WorkerPort }

# Así puedes arrancar sin editar appsettings.json:
# .\scripts\Start-Main.ps1 -Worker1Ip 100.87.131.69 -Worker2Ip 100.76.119.96
if (-not [string]::IsNullOrWhiteSpace($Worker1Ip)) {
  $workerUrl = [UriBuilder]::new('http', $Worker1Ip, $WorkerPort).Uri.GetLeftPart([UriPartial]::Authority)
  $appArguments += @('--Workers:0:BaseUrl', $workerUrl, '--Workers:0:Enabled', 'true')
}
if (-not [string]::IsNullOrWhiteSpace($Worker2Ip)) {
  $workerUrl = [UriBuilder]::new('http', $Worker2Ip, $Worker2Port).Uri.GetLeftPart([UriPartial]::Authority)
  $appArguments += @('--Workers:1:BaseUrl', $workerUrl, '--Workers:1:Enabled', 'true')
}
if (-not [string]::IsNullOrWhiteSpace($SqlConnection)) {
  $appArguments += @('--ConnectionStrings:Asfi', $SqlConnection)
}
if ($InitializeDatabase) { $appArguments += @('--Asfi:InitializeDatabaseOnStartup', 'true') }
if ($BcbHost) {
  $bcbUrl = [UriBuilder]::new('http', $BcbHost, $BcbPort).Uri.GetLeftPart([UriPartial]::Authority)
  $appArguments += @('--BCB:BaseUrl', $bcbUrl)
}
if ($BanksHost) {
  $config = Get-Content (Join-Path $projectRoot 'Asfi.Main.Api/appsettings.json') -Raw -Encoding UTF8 | ConvertFrom-Json
  for ($index = 0; $index -lt $config.Banks.Count; $index++) {
    $bankUrl = [UriBuilder]::new([uri]$config.Banks[$index].BaseUrl)
    $bankUrl.Host = $BanksHost
    $appArguments += @("--Banks:${index}:BaseUrl", $bankUrl.Uri.GetLeftPart([UriPartial]::Authority))
  }
}

Write-Host "Iniciando ASFI MAIN en $listenUrl" -ForegroundColor Cyan
if ($Worker1Ip) { Write-Host "Worker 1: http://${Worker1Ip}:$WorkerPort" -ForegroundColor Green }
if ($Worker2Ip) { Write-Host "Worker 2: http://${Worker2Ip}:$Worker2Port" -ForegroundColor Green }
Write-Host "Hilos lógicos detectados en MAIN: $([Environment]::ProcessorCount)" -ForegroundColor Cyan

dotnet run --no-launch-profile --project (Join-Path $projectRoot 'Asfi.Main.Api/Asfi.Main.Api.csproj') -- @appArguments
if ($LASTEXITCODE -ne 0) { throw 'ASFI Main no pudo ejecutarse.' }
