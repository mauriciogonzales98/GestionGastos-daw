using System.Text.Json;
using GestionGastos.Api.Tests.Infra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace GestionGastos.Api.Tests.Configuracion;

public sealed class ConfiguracionTests
{
    /// <summary>
    /// La resolución de la cadena es una función pura sobre <see cref="IConfiguration"/>: se prueba
    /// como tal, sin levantar el host ni mutar la variable de entorno del proceso — eso serializaba
    /// la suite entera y dejaba el resto de la corrida a merced del orden de ejecución.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Configuracion_SinCadenaDeConexion_NoArranca(string? cadena)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{ConfiguracionDeConexion.Clave}"] = cadena,
            })
            .Build();

        var excepcion = Assert.Throws<InvalidOperationException>(
            () => ConfiguracionDeConexion.ObtenerCadena(configuracion));

        Assert.Contains("ConnectionStrings", excepcion.Message, StringComparison.Ordinal);
        Assert.Contains("user-secrets", excepcion.Message, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings__Default", excepcion.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// El test de arriba cubre la función; este cubre el cableado. Sin él, cambiar la línea de
    /// <c>Program.cs</c> por un valor por defecto silencioso deja la suite entera en verde, que es
    /// justo lo que la spec prohíbe ("nunca un valor por defecto silencioso"). No levanta base:
    /// la excepción ocurre antes de construir el host, así que no entra en la colección compartida
    /// y no cuesta paralelismo.
    /// </summary>
    [Fact]
    public void Arranque_SinCadenaDeConexion_FallaAlConstruirElHost()
    {
        using var fabrica = new FabricaSinCadenaDeConexion();

        var excepcion = Assert.ThrowsAny<Exception>(() => fabrica.CreateClient());

        // WebApplicationFactory invoca el entry point por reflexión, así que la excepción llega
        // envuelta: lo que importa es que la causa sea la nuestra y no un fallo posterior.
        var causa = Excepciones.Desenrollar(excepcion)
            .OfType<InvalidOperationException>()
            .FirstOrDefault(e => e.Message.Contains("ConnectionStrings", StringComparison.Ordinal));

        Assert.NotNull(causa);
        Assert.Contains("user-secrets", causa.Message, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings__Default", causa.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Configuracion_ConCadenaDeConexion_LaDevuelveTalCual()
    {
        const string cadena = "Server=127.0.0.1;Port=3306;Database=gestiongastos;";
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{ConfiguracionDeConexion.Clave}"] = cadena,
            })
            .Build();

        Assert.Equal(cadena, ConfiguracionDeConexion.ObtenerCadena(configuracion));
    }

    /// <summary>
    /// ADR-002: sin la variable de entorno el gate de tests tiene que fallar, y fallar nombrándola.
    /// Un valor por defecto convierte el error en uno de autenticación contra una base ajena, que es
    /// un diagnóstico peor.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CadenaDeTests_SinVariableDeEntorno_FallaNombrandola(string? valorDeLaVariable)
    {
        var excepcion = Assert.Throws<InvalidOperationException>(
            () => BaseDeDatosFixture.ResolverCadena(valorDeLaVariable));

        Assert.Contains("ConnectionStrings__Default", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Configuracion_AppsettingsNoContieneConnectionStrings()
    {
        var archivos = Directory.GetFiles(RutasDelRepo.ProyectoApi, "appsettings*.json");

        Assert.NotEmpty(archivos);
        foreach (var archivo in archivos)
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(archivo));
            Assert.False(
                documento.RootElement.TryGetProperty("ConnectionStrings", out _),
                $"{Path.GetFileName(archivo)} declara una sección ConnectionStrings: la cadena va en user-secrets.");
        }
    }

    [Fact]
    public void Kestrel_EscuchaSoloEnLoopback()
    {
        var appsettings = Path.Combine(RutasDelRepo.ProyectoApi, "appsettings.json");
        using var documento = JsonDocument.Parse(File.ReadAllText(appsettings));

        var url = documento.RootElement
            .GetProperty("Kestrel").GetProperty("Endpoints").GetProperty("Http").GetProperty("Url")
            .GetString();

        Assert.NotNull(url);
        Assert.Equal("127.0.0.1", new Uri(url).Host);

        foreach (var archivo in Directory.GetFiles(RutasDelRepo.ProyectoApi, "appsettings*.json"))
        {
            var contenido = File.ReadAllText(archivo);
            Assert.DoesNotContain("0.0.0.0", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("://*", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("://+", contenido, StringComparison.Ordinal);
        }
    }
}

/// <summary>
/// Igual que <see cref="ApiFactory"/> pero con la cadena de conexión vacía. Usa <c>UseSetting</c>,
/// que pisa la variable de entorno del proceso sin mutarla: los demás tests siguen viendo la suya y
/// pueden correr en paralelo.
/// </summary>
internal sealed class FabricaSinCadenaDeConexion : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{ConfiguracionDeConexion.Clave}", string.Empty);
    }
}

internal static class RutasDelRepo
{
    public static string ProyectoApi { get; } = Localizar();

    private static string Localizar()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            var candidato = Path.Combine(directorio.FullName, "backend", "GestionGastos.Api");
            if (Directory.Exists(candidato))
            {
                return candidato;
            }

            directorio = directorio.Parent;
        }

        throw new DirectoryNotFoundException("No se encontró la carpeta backend/GestionGastos.Api desde " + AppContext.BaseDirectory);
    }
}
