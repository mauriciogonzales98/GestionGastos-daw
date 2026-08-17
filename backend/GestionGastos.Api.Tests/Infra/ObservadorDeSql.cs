using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestionGastos.Api.Tests.Infra;

/// <summary>
/// Permite afirmar sobre el SQL que la API emite, y no solo sobre la respuesta HTTP.
/// </summary>
/// <remarks>
/// Hace falta cuando el motor puede producir el resultado correcto por su cuenta: un índice que ya
/// viene ordenado devuelve las filas como se esperan aunque la consulta no lo pida, y entonces un
/// test conductual no distingue "el contrato está escrito" de "la base lo acertó". Vive en
/// <c>Infra</c> y no dentro de una carpeta de feature porque la técnica no es de ningún endpoint en
/// particular: es gemela de <see cref="RegistroDeExcepciones"/>, con la que comparte forma y lugar.
/// </remarks>
public static class ObservadorDeSql
{
    /// <summary>
    /// Hace un GET a <paramref name="ruta"/> con un proveedor de log agregado, y deja en
    /// <paramref name="sentencias"/> el SQL que EF ejecutó durante ese request.
    /// </summary>
    public static async Task<JsonElement> GetObservandoElSqlAsync(
        string ruta,
        RegistroDeSentencias sentencias)
    {
        // WithWebHostBuilder es la única forma soportada de sumarle configuración a una factory ya
        // construida: devuelve una derivada, y la base no llega a atender ninguna petición ni a
        // levantar un host. Disponer la base alcanza — arrastra a las derivadas.
        await using var fabricaBase = new ApiFactory();
        var conRegistro = fabricaBase.WithWebHostBuilder(constructor =>
            constructor.ConfigureLogging(registro =>
            {
                registro.AddProvider(sentencias);
                registro.AddFilter(DbLoggerCategory.Database.Command.Name, LogLevel.Information);
                registro.SetMinimumLevel(LogLevel.Information);
            }));

        using var cliente = conRegistro.CreateClient();
        using var respuesta = await cliente.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await JsonDeRespuesta.LeerAsync(respuesta);
    }

    /// <summary>
    /// Devuelve la cláusula <c>ORDER BY</c> de la consulta que leyó <paramref name="tabla"/>. Falla
    /// con un mensaje útil si no hay ninguna: sin eso, un cambio que dejara la consulta sin orden
    /// pasaría como "no encontré nada que revisar".
    /// </summary>
    public static string OrderByDe(RegistroDeSentencias sentencias, string tabla)
    {
        var selects = sentencias.Sentencias
            .Where(s => s.Contains($"FROM `{tabla}`", StringComparison.Ordinal))
            .ToList();
        Assert.True(
            selects.Count > 0,
            $"Ninguna consulta observada leyó la tabla '{tabla}'.");

        // Puede haber más de una consulta sobre la tabla (por ejemplo, el COUNT del total, que no
        // lleva ORDER BY): interesa la que ordena.
        var conOrden = selects.Where(s => s.Contains("ORDER BY", StringComparison.Ordinal)).ToList();
        Assert.True(
            conOrden.Count > 0,
            $"Ninguna consulta a '{tabla}' emitió ORDER BY. SQL observado:\n{string.Join("\n---\n", selects)}");

        var sentencia = conOrden[^1];
        var orden = Regex.Match(
            sentencia,
            @"ORDER BY(?<clausula>.*?)(?:\s+LIMIT\b|$)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        Assert.True(orden.Success, $"No se pudo aislar el ORDER BY de:\n{sentencia}");
        return orden.Groups["clausula"].Value;
    }
}

/// <summary>
/// Junta el texto de los comandos SQL que EF Core registra. Se pasa a
/// <see cref="ObservadorDeSql.GetObservandoElSqlAsync"/> y se consulta después de la llamada.
/// </summary>
public sealed class RegistroDeSentencias : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _sentencias = new();

    public IReadOnlyCollection<string> Sentencias => _sentencias;

    public ILogger CreateLogger(string categoryName) => new Registrador(_sentencias, categoryName);

    public void Dispose()
    {
    }

    private sealed class Registrador(ConcurrentQueue<string> sentencias, string categoria) : ILogger
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
            if (categoria.StartsWith(DbLoggerCategory.Database.Command.Name, StringComparison.Ordinal))
            {
                sentencias.Enqueue(formatter(state, exception));
            }
        }
    }
}
