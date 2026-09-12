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
if not "%RESULTADO%"=="0" echo La carga o conversion ASFI no se completo. Revisa el mensaje anterior antes de reintentar.
pause
exit /b %RESULTADO%
