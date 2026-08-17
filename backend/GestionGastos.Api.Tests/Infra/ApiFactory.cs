using System.Collections.Concurrent;
using GestionGastos.Api.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GestionGastos.Api.Tests.Infra;

/// <summary>
/// Levanta la API en memoria con la cadena de conexión de tests. Corre en el entorno
/// <c>Testing</c> a propósito: en Development la página de excepciones del desarrollador
/// devolvería HTML con stack trace y taparía el <c>ProblemDetails</c> que hay que verificar.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly RegistroDeExcepciones _registro = new();

    /// <summary>
    /// Excepciones que el pipeline registró en el log. Permite afirmar cuál llegó al manejador, que
    /// es lo único que distingue "el endpoint de prueba explotó" de "explotó cualquier otra cosa
    /// antes".
    /// </summary>
    public IReadOnlyCollection<Exception> ExcepcionesRegistradas => _registro.Excepciones;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting y no ConfigureAppConfiguration: la cadena se lee antes de construir el host,
        // y las fuentes de configuración diferidas todavía no están cargadas en ese punto.
        builder.UseSetting("ConnectionStrings:Default", BaseDeDatosFixture.CadenaDeConexion);
        builder.ConfigureLogging(registro => registro.AddProvider(_registro));
        builder.ConfigureServices(servicios =>
            servicios.AddSingleton<IStartupFilter, EndpointsDePrueba>());
    }
}

/// <summary>
/// Agrega, solo para los tests, dos caminos: uno que lanza una excepción no controlada y otro que
/// cuenta los movimientos visibles. Se registran después del pipeline de la aplicación para quedar
/// dentro del <c>UseExceptionHandler</c> y después de la resolución del usuario actual: así se
/// verifica el comportamiento global sin ensuciar el código de producción con endpoints de prueba.
/// </summary>
internal sealed class EndpointsDePrueba : IStartupFilter
{
    public const string RutaExcepcion = "/_pruebas/excepcion";
    public const string RutaMovimientosVisibles = "/_pruebas/movimientos-visibles";
    public const string MensajeInterno = "EXPLOSION_DE_PRUEBA en detalle interno";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> siguiente) => app =>
    {
        siguiente(app);
        app.Use(async (contexto, continuar) =>
        {
            if (contexto.Request.Path == RutaExcepcion)
            {
                throw new InvalidOperationException(MensajeInterno);
            }

            if (contexto.Request.Path == RutaMovimientosVisibles)
            {
                var datos = contexto.RequestServices.GetRequiredService<AppDbContext>();
                var visibles = await datos.Movimientos.CountAsync(contexto.RequestAborted);
                await contexto.Response.WriteAsync(
                    visibles.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    contexto.RequestAborted);
                return;
            }

            await continuar();
        });
    };
}

/// <summary>Captura las excepciones que el pipeline registra en el log.</summary>
internal sealed class RegistroDeExcepciones : ILoggerProvider
{
    private readonly ConcurrentQueue<Exception> _excepciones = new();

    public IReadOnlyCollection<Exception> Excepciones => _excepciones;

    public ILogger CreateLogger(string categoryName) => new Registrador(_excepciones);

    public void Dispose()
    {
    }

    private sealed class Registrador(ConcurrentQueue<Exception> excepciones) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (exception is not null)
            {
                excepciones.Enqueue(exception);
            }
        }
    }
}
