@echo off
setlocal
set "CSV_ENTRADA="
if not "%~1"=="" set "CSV_ENTRADA=%~f1"
cd /d "%~dp0"
echo PASO 1/2: Iniciar bases, BCB, 14 bancos y ASFI.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Run-Stack.ps1" -Action Start
if errorlevel 1 goto error
echo.
echo PASO 2/2: Cargar CSV, convertir en ASFI, verificar y exportar saldos en bolivianos.
if not defined CSV_ENTRADA (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Run-Stack.ps1" -Action Import
) else (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Run-Stack.ps1" -Action Import -CsvPath "%CSV_ENTRADA%"
)
if errorlevel 1 goto error
echo.
echo Proceso completo. Revisa el tiempo ASFI y la ruta de resultados indicados arriba.
pause
exit /b 0
:error
echo.
echo No se completo el flujo. Revisa el error anterior. Los datos importados se conservan.
pause
exit /b 1
