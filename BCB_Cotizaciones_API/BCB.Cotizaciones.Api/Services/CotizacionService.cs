using BCB.Cotizaciones.Api.Models;
using Microsoft.Extensions.Options;

namespace BCB.Cotizaciones.Api.Services;

public sealed class CotizacionService
{
    private readonly object _lock = new();
    private readonly decimal _tipoCambioBase;
    private readonly string _claveAdministracion;

    private decimal _valorActual;
    private DateTimeOffset _fechaActualizacion;
    private long _version;
    private TaskCompletionSource<long> _senalActualizacion = CrearSenal();

    public CotizacionService(IOptions<BcbOptions> options)
    {
        var config = options.Value;

        if (config.TipoCambioBase <= 0)
        {
            throw new InvalidOperationException(
                "BCB:TipoCambioBase debe ser mayor que 0.");
        }

        if (string.IsNullOrWhiteSpace(config.ClaveAdministracion))
        {
            throw new InvalidOperationException(
                "BCB:ClaveAdministracion debe configurarse mediante una variable de entorno.");
        }

        _tipoCambioBase = Math.Round(config.TipoCambioBase, 4, MidpointRounding.AwayFromZero);
        _claveAdministracion = config.ClaveAdministracion;

        _valorActual = _tipoCambioBase;
        _fechaActualizacion = DateTimeOffset.Now;
        _version = 1;
    }

    public decimal TipoCambioBase => _tipoCambioBase;
    public bool EsClaveAdministracionValida(string? clave) =>
        !string.IsNullOrEmpty(clave) &&
        string.Equals(clave, _claveAdministracion, StringComparison.Ordinal);

    public CotizacionResponse ObtenerCotizacion()
    {
        lock (_lock)
        {
            return CrearRespuesta();
        }
    }

    public void ActualizarCotizacion(decimal nuevoValor)
    {
        TaskCompletionSource<long> senalAnterior;
        long nuevaVersion;

        lock (_lock)
        {
            if (nuevoValor <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nuevoValor),
                    "El valor debe ser mayor que 0.");
            }

            _valorActual = Math.Round(nuevoValor, 4, MidpointRounding.AwayFromZero);
            _fechaActualizacion = DateTimeOffset.Now;
            _version++;
            nuevaVersion = _version;

            senalAnterior = _senalActualizacion;
            _senalActualizacion = CrearSenal();
        }

        // Despierta a todos los clientes SSE que estaban esperando esta actualización.
        senalAnterior.TrySetResult(nuevaVersion);
    }

    public Task EsperarActualizacionAsync(long versionConocida, CancellationToken cancellationToken)
    {
        Task<long> espera;

        lock (_lock)
        {
            if (_version != versionConocida)
            {
                return Task.CompletedTask;
            }

            espera = _senalActualizacion.Task;
        }

        return espera.WaitAsync(cancellationToken);
    }

    private CotizacionResponse CrearRespuesta()
    {
        return new CotizacionResponse(
            Fuente: "Banco Central de Bolivia - Simulación académica",
            MonedaBase: "USD",
            MonedaCotizada: "BOB",
            ValorBolivianoPorDolar: _valorActual,
            FechaActualizacion: _fechaActualizacion,
            Version: _version);
    }

    private static TaskCompletionSource<long> CrearSenal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
