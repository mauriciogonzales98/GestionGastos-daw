using System.Net;
using System.Text.Json;
using GestionGastos.Api.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace GestionGastos.Api.Tests.Infra;

// Levanta la API, que resuelve el usuario semilla contra la base compartida: pertenece a la
// colección aunque no use el fixture, para no correr en paralelo con los tests que la modifican.
[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class ManejoDeErroresTests
{
    [Fact]
    public async Task MysqlNoDisponible_PropagaElErrorDeConexion()
    {
        const string cadenaHaciaUnPuertoCerrado =
            "Server=127.0.0.1;Port=3399;Database=gestiongastos_test;User ID=gestiongastos;Password=;Connection Timeout=3";
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(cadenaHaciaUnPuertoCerrado, ServerVersion.Parse("8.4.0-mysql"))
            .Options;

        await using var contexto = new AppDbContext(opciones);

        var excepcion = await Assert.ThrowsAnyAsync<Exception>(
            () => contexto.Categorias.ToListAsync());

        // El error del proveedor tiene que llegar entero: no se atrapa ni se degrada a un modo sin
        // base. EF lo envuelve para sugerir reintentos, pero la causa sigue siendo la de MySQL.
        var deMySql = Desenrollar(excepcion).OfType<MySqlException>().FirstOrDefault();
        Assert.NotNull(deMySql);
        Assert.NotEqual(MySqlErrorCode.None, deMySql.ErrorCode);
        Assert.Contains("Unable to connect", deMySql.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExcepcionNoControlada_DevuelveProblemDetailsSinStackTrace()
    {
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.GetAsync(EndpointsDePrueba.RutaExcepcion);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        // Correlación: sin esto el test da verde con CUALQUIER excepción previa del pipeline (por
        // ejemplo la de MySQL caído), porque las demás aserciones se cumplen igual.
        Assert.Contains(
            fabrica.ExcepcionesRegistradas,
            e => e is InvalidOperationException && e.Message == EndpointsDePrueba.MensajeInterno);

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        using var documento = JsonDocument.Parse(cuerpo);
        Assert.True(documento.RootElement.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));

        Assert.DoesNotContain(EndpointsDePrueba.MensajeInterno, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("stackTrace", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", cuerpo, StringComparison.Ordinal);
    }

    private static IEnumerable<Exception> Desenrollar(Exception excepcion)
    {
        for (var actual = excepcion; actual is not null; actual = actual.InnerException)
        {
            yield return actual;
        }
    }
}
