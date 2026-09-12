@echo off
setlocal
cd /d "%~dp0"
echo Se vaciaran los datos de los 14 bancos y ASFI: 15 destinos.
echo Las APIs se detendran durante el vaciado y se reiniciaran al terminar.
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Run-Stack.ps1" -Action Reset
set "RESULTADO=%ERRORLEVEL%"
echo.
if not "%RESULTADO%"=="0" echo El proceso no se completo. Revisa el mensaje anterior y los logs antes de cargar el CSV.
pause
exit /b %RESULTADO%
