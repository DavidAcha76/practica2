param(
    [ValidateSet('Start','Import','Stop','Status','Reset')][string]$Action = 'Start',
    [string]$CsvPath = ''
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$runtimeDirectory = Join-Path $repositoryRoot '.runtime'
$logDirectory = Join-Path $repositoryRoot '.logs'
$statePath = Join-Path $runtimeDirectory 'processes.json'
$bootstrapDll = Join-Path $repositoryRoot 'BD/Bootstrap/bin/Debug/net10.0/Bootstrap.dll'
New-Item -ItemType Directory -Force -Path $runtimeDirectory,$logDirectory | Out-Null
$script:services = @()

function Invoke-Native([string]$FilePath, [string[]]$Arguments, [string]$LogPath = '', [string]$ErrorLogPath = '') {
    # Windows PowerShell 5 trata stderr redirigido como NativeCommandError.
    # El codigo de salida decide si Docker/dotnet fallaron, no el uso de stderr.
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        if ($ErrorLogPath) { & $FilePath @Arguments > $LogPath 2> $ErrorLogPath }
        elseif ($LogPath) { & $FilePath @Arguments *> $LogPath }
        else { & $FilePath @Arguments 2>&1 | Out-Host }
        return $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousPreference }
}

function Save-State {
    ConvertTo-Json -InputObject @($script:services) -Depth 5 | Set-Content -LiteralPath $statePath -Encoding UTF8
}

function Get-OwnedProcess($Service) {
    if (-not $Service.Executable -or -not ([System.IO.Path]::GetFullPath($Service.Executable).StartsWith($repositoryRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase))) { return $null }
    $process = Get-Process -Id $Service.ProcessId -ErrorAction SilentlyContinue
    if ($process -and $process.Path -eq $Service.Executable -and $process.StartTime.ToUniversalTime().Ticks.ToString() -eq $Service.StartTicks) {
        return $process
    }
    return $null
}

function Stop-OwnedServices {
    foreach ($service in $script:services) {
        $process = Get-OwnedProcess $service
        if ($process) {
            Stop-Process -Id $process.Id -Force
            if (-not $process.WaitForExit(10000)) { throw "No se pudo detener $($service.Name)." }
            Write-Host "Detenido: $($service.Name)"
        }
    }
    $script:services = @()
    Save-State
}

function Find-Docker {
    $command = Get-Command docker -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    foreach ($path in @("$env:ProgramFiles\Docker\Docker\resources\bin\docker.exe", "$env:LOCALAPPDATA\Programs\DockerDesktop\resources\bin\docker.exe")) {
        if (Test-Path -LiteralPath $path) { return $path }
    }
    throw 'Falta Docker Desktop. Instala Docker Desktop con WSL 2, inicializalo una vez y vuelve a abrir INICIAR_TODO.bat. Ver LEEME.md.'
}

function Invoke-Compose([string[]]$Arguments) {
    $composeArguments = @('compose','--project-name','practica2','--env-file',(Join-Path $repositoryRoot 'BD/.env'),'-f',(Join-Path $repositoryRoot 'BD/docker-compose.yml')) + $Arguments
    if ((Invoke-Native $script:docker $composeArguments) -ne 0) { throw 'Docker Compose fallo. Revisa Docker Desktop y los mensajes anteriores.' }
}

function Ensure-Docker {
    $script:docker = Find-Docker
    $infoLog = Join-Path $logDirectory 'docker-info.log'
    $errorLog = Join-Path $logDirectory 'docker-info.err.log'
    $dockerExit = Invoke-Native $script:docker @('info','--format','{{.OSType}}') $infoLog $errorLog
    if ($dockerExit -ne 0) {
        $desktop = @("$env:ProgramFiles\Docker\Docker\Docker Desktop.exe", "$env:LOCALAPPDATA\Programs\DockerDesktop\Docker Desktop.exe") | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if ($desktop) { Start-Process -FilePath $desktop -WindowStyle Hidden }
        Write-Host 'Esperando a Docker Desktop...' -ForegroundColor Cyan
        $deadline = [datetime]::UtcNow.AddMinutes(3)
        do {
            Start-Sleep -Seconds 3
            $dockerExit = Invoke-Native $script:docker @('info','--format','{{.OSType}}') $infoLog $errorLog
            if ($dockerExit -eq 0) { break }
        } while ([datetime]::UtcNow -lt $deadline)
        if ($dockerExit -ne 0) { throw "Docker no inicio. Abre Docker Desktop y completa su configuracion de WSL 2. Detalle: $errorLog" }
    }
    $osType = (Get-Content (Join-Path $logDirectory 'docker-info.log') -Raw).Trim()
    if ($osType -ne 'linux') { throw 'Docker debe usar contenedores Linux para estos cinco motores.' }
    Invoke-Compose @('config','--quiet')
}

function Build-Project([string]$RelativePath, [string]$LogName) {
    $path = Join-Path $repositoryRoot $RelativePath
    $log = Join-Path $logDirectory $LogName
    Write-Host "Compilando $RelativePath..." -ForegroundColor Cyan
    if ((Invoke-Native 'dotnet' @('build',$path,'--nologo','--verbosity','minimal') $log) -ne 0) { Get-Content -LiteralPath $log -Tail 25; throw "No se pudo compilar. Log: $log" }
}

function Invoke-Bootstrap([string]$Command, [string]$InputCsv = '') {
    $arguments = @($bootstrapDll, $Command, $repositoryRoot)
    if ($InputCsv) { $arguments += $InputCsv }
    if ((Invoke-Native 'dotnet' $arguments) -ne 0) { throw "Fallo el paso de bases de datos: $Command." }
}

function Wait-Api($Service) {
    $deadline = [datetime]::UtcNow.AddSeconds(90)
    do {
        if (-not (Get-OwnedProcess $Service)) { throw "$($Service.Name) termino. Revisa .logs/$($Service.Name).err.log y .out.log." }
        try {
            $response = Invoke-RestMethod -Uri $Service.HealthUrl -TimeoutSec 3
            if ($Service.BankId -and $response.bancoId -ne $Service.BankId) { throw 'La respuesta pertenece a otro banco.' }
            return
        } catch { Start-Sleep -Milliseconds 500 }
    } while ([datetime]::UtcNow -lt $deadline)
    throw "$($Service.Name) no responde en $($Service.HealthUrl). Revisa sus logs."
}

function Start-Api([string]$RelativeDirectory, [string]$Name, [string]$Url, [string]$HealthPath, [int]$BankId = 0) {
    $directory = Join-Path $repositoryRoot $RelativeDirectory
    $executable = Join-Path $directory "bin/Debug/net10.0/$Name.exe"
    $process = Start-Process -FilePath $executable -ArgumentList @('--urls', $Url) -WorkingDirectory $directory -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $logDirectory "$Name.out.log") `
        -RedirectStandardError (Join-Path $logDirectory "$Name.err.log")
    $service = [pscustomobject]@{
        Name = $Name; ProcessId = $process.Id; StartTicks = $process.StartTime.ToUniversalTime().Ticks.ToString()
        Executable = $executable; HealthUrl = $Url.TrimEnd('/') + $HealthPath; BankId = $BankId
    }
    $script:services += $service
    Save-State
    Write-Host "Iniciando $Name en $Url"
}

function Show-Status([switch]$SkipDatabases) {
    if ($script:services.Count -ne 16 -or @($script:services | Where-Object { -not (Get-OwnedProcess $_) }).Count) { throw 'Ejecuta primero INICIAR_TODO.bat para iniciar las bases y los 16 servicios locales.' }
    foreach ($service in $script:services) {
        Wait-Api $service
        $status = if (Get-OwnedProcess $service) { 'EN EJECUCION' } else { 'DETENIDO' }
        Write-Host "$status - $($service.Name) - $($service.HealthUrl)"
    }
    $dependencies = Invoke-RestMethod 'http://localhost:5000/api/asfi/dependencies' -TimeoutSec 15
    if (@($dependencies | Where-Object { $_.type -eq 'Banco' }).Count -ne 14 -or @($dependencies | Where-Object { $_.type -eq 'BCB' }).Count -ne 1) { throw 'ASFI debe tener habilitados los 14 bancos y BCB.' }
    foreach ($dependency in $dependencies) {
        $status = if ($dependency.ok) { 'OK' } else { 'SIN CONEXION' }
        Write-Host "$status - $($dependency.name) - $($dependency.url)"
    }
    if (@($dependencies | Where-Object { $_.type -ne 'Worker' -and -not $_.ok }).Count) { throw 'Hay servicios locales sin conexion.' }
    if (@($dependencies | Where-Object { $_.type -eq 'Worker' -and -not $_.ok }).Count) {
        Write-Host 'Los workers remotos pendientes deben ejecutar INICIAR_WORKER.bat en sus PCs con Tailscale. ASFI puede procesar localmente.' -ForegroundColor Yellow
    }
    if (-not $SkipDatabases) { Invoke-Bootstrap 'check' }
}

$lock = $null
try {
    try { $lock = [System.IO.File]::Open((Join-Path $runtimeDirectory 'stack.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
    catch { throw 'Ya hay un arranque, una carga o un vaciado en curso. Espera a que termine.' }
    # Leer despues de adquirir el bloqueo: otra ejecucion pudo haber actualizado los PID.
    if (Test-Path -LiteralPath $statePath) {
        # En PowerShell 5, @(... | ConvertFrom-Json) encierra todo el JSON en un
        # solo elemento. Expandir la variable conserva los 16 servicios.
        $savedServices = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
        $script:services = @($savedServices)
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Falta .NET SDK 10. Instala el SDK indicado en LEEME.md.' }

    if ($Action -eq 'Stop') {
        Stop-OwnedServices
        $script:docker = Find-Docker
        Invoke-Compose @('stop')
        Write-Host 'Servicios detenidos. Los datos se conservan en los volumenes de Docker.'
        exit 0
    }
    if ($Action -eq 'Status') { Show-Status; exit 0 }
    if ($Action -eq 'Import') {
        if (-not (Test-Path -LiteralPath $bootstrapDll)) { throw 'Ejecuta primero INICIAR_TODO.bat.' }
        if (-not $CsvPath) { $CsvPath = Join-Path $repositoryRoot 'BD/dataset.csv' }
        if (-not (Test-Path -LiteralPath $CsvPath -PathType Leaf)) { throw "No existe el CSV: $CsvPath" }
        Show-Status -SkipDatabases
        Invoke-Bootstrap 'import' $CsvPath
        Write-Host 'Carga completada. La conversion ASFI se inicia por POST http://localhost:5000/api/asfi/runs.' -ForegroundColor Green
        exit 0
    }

    Ensure-Docker
    if ($Action -ne 'Reset' -and $script:services.Count -eq 16 -and @($script:services | Where-Object { -not (Get-OwnedProcess $_) }).Count -eq 0) {
        Invoke-Compose @('up','-d','--wait','--wait-timeout','300')
        Show-Status
        Write-Host 'Los servicios ya estaban iniciados. No se duplicaron procesos.' -ForegroundColor Green
        exit 0
    }
    Stop-OwnedServices
    $ports = @(5000,5050) + @(5101..5114)
    $busy = @([System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() | Where-Object { $_.Port -in $ports } | ForEach-Object { $_.Port } | Select-Object -Unique)
    if ($busy.Count) { throw "Hay otros procesos usando los puertos $($busy -join ', '). Cierralos antes de iniciar." }
    Build-Project 'BD/Bootstrap/Bootstrap.csproj' 'build-bootstrap.log'
    Build-Project '14_Bancos_APIs_Cifradas/BancosCifrados.sln' 'build-bancos.log'
    Build-Project 'BCB_Cotizaciones_API/BCB.Cotizaciones.Api.sln' 'build-bcb.log'
    Build-Project 'ASFI_Cluster_DotNet10/ASFI.Cluster.sln' 'build-asfi.log'
    if ($Action -eq 'Reset') {
        Write-Host 'Vaciando los 14 bancos y ASFI. Esta operacion elimina sus datos.' -ForegroundColor Yellow
        Invoke-Compose @('up','-d','--wait','--wait-timeout','300')
        Invoke-Bootstrap 'clear'
        Write-Host 'Los 15 destinos quedaron vacios. Reiniciando servicios...' -ForegroundColor Green
    }
    Invoke-Bootstrap 'configure'
    Write-Host 'Iniciando las cinco bases de datos; la primera vez se descargan las imagenes...' -ForegroundColor Cyan
    Invoke-Compose @('up','-d','--wait','--wait-timeout','300')
    Invoke-Bootstrap 'init'

    $keyPath = Join-Path $runtimeDirectory 'bcb-admin.key'
    if (-not (Test-Path -LiteralPath $keyPath)) {
        $bytes = New-Object byte[] 32
        $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try { $random.GetBytes($bytes) } finally { $random.Dispose() }
        [System.IO.File]::WriteAllText($keyPath, [Convert]::ToBase64String($bytes))
    }
    $previousKey = $env:BCB__ClaveAdministracion
    try {
        $env:BCB__ClaveAdministracion = [System.IO.File]::ReadAllText($keyPath).Trim()
        Start-Api 'BCB_Cotizaciones_API/BCB.Cotizaciones.Api' 'BCB.Cotizaciones.Api' 'http://localhost:5050' '/api/cotizacion'
    } finally { $env:BCB__ClaveAdministracion = $previousKey }
    Get-ChildItem (Join-Path $repositoryRoot '14_Bancos_APIs_Cifradas/src') -Directory -Filter Banco*.Api | ForEach-Object {
        $config = Get-Content (Join-Path $_.FullName 'appsettings.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        Start-Api "14_Bancos_APIs_Cifradas/src/$($_.Name)" $_.Name $config.Urls '/api/banco/info' $config.Bank.BancoId
    }
    Start-Api 'ASFI_Cluster_DotNet10/Asfi.Main.Api' 'Asfi.Main.Api' 'http://localhost:5000' '/api/asfi/config-summary'
    Show-Status
    Write-Host 'Servicios locales listos. Usa CARGAR_CSV.bat para cargar BD/dataset.csv.' -ForegroundColor Green
    Write-Host "Logs: $logDirectory"
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    if ($lock) { $lock.Dispose() }
}
