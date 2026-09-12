@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Run-Stack.ps1" -Action Process
set "RESULTADO=%ERRORLEVEL%"
echo.
if not "%RESULTADO%"=="0" echo La conversion o su verificacion no se completo. Revisa el mensaje y los reportes en .runtime.
pause
exit /b %RESULTADO%
