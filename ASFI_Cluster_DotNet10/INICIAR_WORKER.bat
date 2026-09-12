@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-Worker.ps1" -NodeName "%COMPUTERNAME%" -Port 5201 -ConfigureFirewall
set "RESULTADO=%ERRORLEVEL%"
pause
exit /b %RESULTADO%
