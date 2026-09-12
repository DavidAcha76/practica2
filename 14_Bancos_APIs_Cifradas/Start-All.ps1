param(
    [string]$ListenAddress = "localhost",
    [switch]$InitializeDatabases
)

$ErrorActionPreference = "Stop"
$projects = @(Get-ChildItem -Path "$PSScriptRoot\src" -Directory | Where-Object { $_.Name -like "Banco*.Api" } | ForEach-Object {
    $config = Get-Content (Join-Path $_.FullName 'appsettings.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    [pscustomobject]@{
        Name = $_.Name
        Directory = $_.FullName
        Project = (Get-ChildItem $_.FullName -Filter *.csproj | Select-Object -First 1).FullName
        BankId = [int]$config.Bank.BancoId
        Port = ([uri]$config.Urls).Port
    }
} | Sort-Object BankId)

if ($projects.Count -ne 14 -or @($projects.Port | Select-Object -Unique).Count -ne 14) {
    throw 'Se requieren 14 APIs con puertos diferentes. Revisa los appsettings.json.'
}
$busyPorts = @([System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() | ForEach-Object { $_.Port })
foreach ($project in $projects) {
    if ($project.Port -in $busyPorts) { throw "El puerto $($project.Port) de $($project.Name) ya esta ocupado." }
}

# Compilar una sola vez evita que 14 compilaciones escriban al mismo tiempo en BankApi.Shared.
dotnet build (Join-Path $PSScriptRoot 'BancosCifrados.sln') --nologo
if ($LASTEXITCODE -ne 0) { throw 'No se pudieron compilar las APIs bancarias.' }

$logDirectory = Join-Path $PSScriptRoot '.logs'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
foreach ($project in $projects) {
    $listenUrl = [UriBuilder]::new('http', $ListenAddress, $project.Port).Uri.GetLeftPart([UriPartial]::Authority)
    $arguments = @('run', '--no-build', '--no-launch-profile', '--project', ('"{0}"' -f $project.Project), '--', '--urls', $listenUrl)
    if ($InitializeDatabases) { $arguments += @('--Database:InitializeOnStartup', 'true') }
    $process = Start-Process dotnet -ArgumentList $arguments -WorkingDirectory $project.Directory -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $logDirectory "$($project.Name).out.log") `
        -RedirectStandardError (Join-Path $logDirectory "$($project.Name).err.log")
    Write-Host "$($project.Name): $listenUrl (PID $($process.Id))" -ForegroundColor Green
}
Write-Host "Logs: $logDirectory. Comprueba las APIs con .\Test-All.ps1."
