using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using GestionGastos.Api.Movimientos;
using GestionGastos.Api.Tests.Infra;

namespace GestionGastos.Api.Tests.Contrato;

/// <summary>
/// Verifica los cuerpos de petición: lo que el frontend declara que envía contra lo que el backend
/// declara que acepta.
/// </summary>
/// <remarks>
/// Acá no hay JSON del servidor que mirar —el cuerpo lo emite el cliente—, así que la comparación es
/// por reflexión sobre los <c>record</c> de C#, más una prueba de ida y vuelta que arma el cuerpo con
/// los nombres que declara el frontend y comprueba que la API lo acepta. La reflexión sola diría que
/// los nombres coinciden; la ida y vuelta comprueba que además el servidor los entiende.
/// </remarks>
[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class ContratoDePeticionesTests(BaseDeDatosFixture baseDeDatos)
{
    private const int CategoriaComidaId = 1;

    /// <summary>
    /// Valor de ejemplo por campo declarado. Si el frontend agrega un campo, no hay valor para él y
    /// el test falla pidiendo que se agregue: un campo nuevo sin cubrir quedaría sin verificar.
    /// </summary>
    private static readonly Dictionary<string, object?> ValoresDeEjemplo =
        new(StringComparer.Ordinal)
        {
            ["categoriaId"] = CategoriaComidaId,
            ["monto"] = 123.45m,
            ["fecha"] = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["nota"] = "verificación de contrato",
            ["tipoEsperado"] = "gasto",
        };

    [Fact]
    public void Contrato_CrearMovimiento_LosCamposDelFrontendCoincidenConElRecord() =>
        AfirmarQueLosCamposCoinciden<CrearMovimientoRequest>("CrearMovimientoRequest");

    [Fact]
    public void Contrato_ModificarMovimiento_LosCamposDelFrontendCoincidenConElRecord() =>
        AfirmarQueLosCamposCoinciden<ModificarMovimientoRequest>("ModificarMovimientoRequest");

    [Fact]
    public async Task Contrato_CuerpoConLosNombresDelFrontend_EsAceptadoPorElAlta()
    {
        await baseDeDatos.LimpiarAsync();

        // El cuerpo se arma con los nombres que DECLARA el frontend, no con literales escritos acá:
        // si el frontend renombra un campo, este cuerpo cambia y el servidor deja de entenderlo.
        var cuerpo = CuerpoSegunElFrontend(
            "CrearMovimientoRequest",
            new Dictionary<string, string>(StringComparer.Ordinal));

        using var respuesta = await PostearAsync(cuerpo);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    [Fact]
    public async Task Contrato_CuerpoConUnCampoRenombrado_EsRechazado()
    {
        await baseDeDatos.LimpiarAsync();

        // Sin este sad path, la aceptación del test anterior no probaría nada: podría estar pasando
        // porque el servidor ignora lo que no entiende.
        var cuerpo = CuerpoSegunElFrontend(
            "CrearMovimientoRequest",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["categoriaId"] = "idDeCategoria" });

        using var respuesta = await PostearAsync(cuerpo);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var problema = await JsonDeRespuesta.LeerAsync(respuesta);
        Assert.True(
            problema.TryGetProperty("errors", out var errores),
            "El 400 tiene que decir qué campo no se entendió, no ser genérico.");
        Assert.True(errores.TryGetProperty("categoriaId", out _));
    }

    // ---------------------------------------------------------------- helpers

    private static void AfirmarQueLosCamposCoinciden<T>(string nombreEnElFrontend)
    {
        var declaradosPorElFrontend = CamposDelFrontend(nombreEnElFrontend);

        var delRecord = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name))
            .ToList();

        Assert.NotEmpty(delRecord);

        var faltanEnElBackend = declaradosPorElFrontend.Except(delRecord, StringComparer.Ordinal).ToList();
        var faltanEnElFrontend = delRecord.Except(declaradosPorElFrontend, StringComparer.Ordinal).ToList();

        Assert.True(
            faltanEnElBackend.Count == 0 && faltanEnElFrontend.Count == 0,
            $"El cuerpo de petición «{nombreEnElFrontend}» no coincide con {typeof(T).Name}." +
            Environment.NewLine +
            $"  El frontend envía y el backend no acepta: {Enumerar(faltanEnElBackend)}" +
            Environment.NewLine +
            $"  El backend acepta y el frontend no envía: {Enumerar(faltanEnElFrontend)}");
    }

    private static string Enumerar(List<string> campos) =>
        campos.Count == 0 ? "(ninguno)" : string.Join(", ", campos);

    private static List<string> CamposDelFrontend(string nombre)
    {
        var tipo = LectorDeTiposDelFrontend
            .LeerElContratoDelFrontend()
            .Single(t => string.Equals(t.Nombre, nombre, StringComparison.Ordinal));

        return tipo.Campos.Select(c => c.Nombre).ToList();
    }

    /// <summary>
    /// Arma el cuerpo con los nombres declarados por el frontend, aplicando los renombres pedidos.
    /// </summary>
    private static string CuerpoSegunElFrontend(
        string nombreDelTipo,
        IReadOnlyDictionary<string, string> renombres)
    {
        var cuerpo = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var campo in CamposDelFrontend(nombreDelTipo))
        {
            Assert.True(
                ValoresDeEjemplo.TryGetValue(campo, out var valor),
                $"El frontend declara «{campo}» en {nombreDelTipo} y este test no tiene un valor de " +
                "ejemplo para él. Agregalo a ValoresDeEjemplo: sin valor, el campo viajaría ausente " +
                "y quedaría sin verificar.");

            cuerpo[renombres.GetValueOrDefault(campo, campo)] = valor;
        }

        return JsonSerializer.Serialize(cuerpo);
    }

    private static async Task<HttpResponseMessage> PostearAsync(string cuerpo)
    {
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        using var contenido = new StringContent(cuerpo, Encoding.UTF8, "application/json");

        return await cliente.PostAsync("/api/movimientos", contenido);
    }
}
