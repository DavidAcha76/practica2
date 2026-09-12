@echo off
setlocal
set "CSV_ENTRADA="
if not "%~1"=="" set "CSV_ENTRADA=%~f1"
cd /d "%~dp0"
if not defined CSV_ENTRADA (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Run-Stack.ps1" -Action Import
) else (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Run-Stack.ps1" -Action Import -CsvPath "%CSV_ENTRADA%"
)
set "RESULTADO=%ERRORLEVEL%"
echo.
if not "%RESULTADO%"=="0" echo La carga no se completo. Puedes corregir el problema y volver a ejecutar este BAT.
pause
exit /b %RESULTADO%
