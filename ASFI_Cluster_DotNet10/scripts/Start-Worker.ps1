param(
  [Parameter(Mandatory=$false)][string]$NodeName = "ASFI-WORKER",
  [ValidateRange(1,65535)][int]$Port = 5201,
  [string]$ListenAddress = "0.0.0.0",
  [switch]$ConfigureFirewall
)
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent
if ($ConfigureFirewall -and -not (Get-NetFirewallRule -DisplayName "ASFI Worker $Port" -ErrorAction SilentlyContinue)) {
  $firewallScript = Join-Path $PSScriptRoot 'Open-Firewall-Worker.ps1'
  $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $firewallScript), '-Port', $Port)
  $setup = Start-Process powershell.exe -Verb RunAs -WindowStyle Hidden -ArgumentList $arguments -Wait -PassThru
  if ($setup.ExitCode -ne 0) { throw 'No se pudo abrir el puerto del worker en el firewall.' }
}
$listenUrl = [UriBuilder]::new('http', $ListenAddress, $Port).Uri.GetLeftPart([UriPartial]::Authority)
Write-Host "Iniciando worker $NodeName en puerto $Port; hilos lógicos detectados: $([Environment]::ProcessorCount)" -ForegroundColor Cyan
dotnet run --no-launch-profile --project (Join-Path $projectRoot 'Asfi.Worker.Api/Asfi.Worker.Api.csproj') -- --urls $listenUrl --Worker:NodeName $NodeName
if ($LASTEXITCODE -ne 0) { throw 'ASFI Worker no pudo ejecutarse.' }
