@echo off
setlocal
cd /d "%~dp0"

rem Ejecuta este archivo en cada PC de apoyo. Opcionalmente: 1 o 2.
set "NODO=%~1"
if /I "%NODO%"=="1" set "NODO=WORKER-I7-12650H"
if /I "%NODO%"=="2" set "NODO=WORKER-I7-10750H"
if not defined NODO (
    if /I "%COMPUTERNAME%"=="I7-12650H" set "NODO=WORKER-I7-12650H"
    if /I "%COMPUTERNAME%"=="I7-10750H" set "NODO=WORKER-I7-10750H"
)
if not defined NODO set "NODO=%COMPUTERNAME%"

echo Iniciando %NODO% en esta PC. El firewall puede pedir permiso una vez.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-Worker.ps1" -NodeName "%NODO%" -Port 5201 -ConfigureFirewall
set "RESULTADO=%ERRORLEVEL%"
echo.
if not "%RESULTADO%"=="0" echo El worker no pudo iniciar. Revisa el mensaje anterior.
pause
exit /b %RESULTADO%
