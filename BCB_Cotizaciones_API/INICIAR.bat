@echo off
if not defined BCB__ClaveAdministracion (
    echo Define BCB__ClaveAdministracion antes de iniciar. Ver ACTUALIZAR_COTIZACION.md.
    pause
    exit /b 1
)
cd /d "%~dp0BCB.Cotizaciones.Api"
echo Iniciando API BCB en http://localhost:5050/
dotnet run --no-launch-profile
pause
