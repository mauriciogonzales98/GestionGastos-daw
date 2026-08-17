using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using GestionGastos.Api.Common;
using GestionGastos.Api.Data;
using GestionGastos.Api.Data.Entidades;
using GestionGastos.Api.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace GestionGastos.Api.Tests.Movimientos;

[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class CrearMovimientoTests(BaseDeDatosFixture baseDeDatos)
{
    private const int CategoriaComidaId = 1;
    private const int CategoriaSueldoId = 8;
    private const int CategoriaInexistenteId = 987654;
    private const string FechaValida = "2026-03-01";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---------------------------------------------------------------- camino feliz

    [Fact]
    public async Task CrearGasto_Valido_Devuelve201YPersiste()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.PostAsync(
            "/api/movimientos",
            Cuerpo(new { categoriaId = CategoriaComidaId, monto = 1500.50m, fecha = FechaValida, nota = "Almuerzo" }));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var creado = await LeerJsonAsync(respuesta);
        var id = creado.GetProperty("id").GetInt32();
        Assert.True(id > 0);
        Assert.Equal($"/api/movimientos/{id}", respuesta.Headers.Location?.ToString());
        Assert.Equal("gasto", creado.GetProperty("tipo").GetString());
        Assert.Equal(CategoriaComidaId, creado.GetProperty("categoria").GetProperty("id").GetInt32());
        Assert.Equal("Comida", creado.GetProperty("categoria").GetProperty("nombre").GetString());
        Assert.Equal(1500.50m, creado.GetProperty("monto").GetDecimal());
        Assert.Equal(FechaValida, creado.GetProperty("fecha").GetString());
        Assert.Equal("Almuerzo", creado.GetProperty("nota").GetString());

        var persistido = await UnicoMovimientoAsync();
        Assert.Equal(id, persistido.Id);
        Assert.Equal(TipoMovimiento.Gasto, persistido.Tipo);
        Assert.Equal(CategoriaComidaId, persistido.CategoriaId);
        Assert.Equal(1500.50m, persistido.Monto);
        Assert.Equal(new DateOnly(2026, 3, 1), persistido.Fecha);
        Assert.Equal("Almuerzo", persistido.Nota);
    }

    [Fact]
    public async Task CrearIngreso_Valido_Devuelve201YPersiste()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.PostAsync(
            "/api/movimientos",
            Cuerpo(new { categoriaId = CategoriaSueldoId, monto = 900000m, fecha = FechaValida, nota = (string?)null }));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var creado = await LeerJsonAsync(respuesta);
        Assert.Equal("ingreso", creado.GetProperty("tipo").GetString());
        Assert.Equal("Sueldo", creado.GetProperty("categoria").GetProperty("nombre").GetString());

        var persistido = await UnicoMovimientoAsync();
        Assert.Equal(TipoMovimiento.Ingreso, persistido.Tipo);
        Assert.Equal(CategoriaSueldoId, persistido.CategoriaId);
        Assert.Equal(900000m, persistido.Monto);
    }

    [Fact]
    public async Task Crear_FijaMonedaARS()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.PostAsync("/api/movimientos", CuerpoValido());
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var creado = await LeerJsonAsync(respuesta);
        Assert.Equal("ARS", creado.GetProperty("moneda").GetString());
        Assert.Equal(Moneda.Predeterminada, creado.GetProperty("moneda").GetString());

        var persistido = await UnicoMovimientoAsync();
        Assert.Equal("ARS", persistido.Moneda);
    }

    [Fact]
    public async Task Crear_ConNotaDeCientoVeinte_LaPersiste()
    {
        await baseDeDatos.LimpiarAsync();
        var nota = new string('n', 120);
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.PostAsync(
            "/api/movimientos",
            Cuerpo(new { categoriaId = CategoriaComidaId, monto = 10m, fecha = FechaValida, nota }));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creado = await LeerJsonAsync(respuesta);
        Assert.Equal(nota, creado.GetProperty("nota").GetString());

        var persistido = await UnicoMovimientoAsync();
        Assert.Equal(nota, persistido.Nota);
        Assert.Equal(120, persistido.Nota!.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Crear_ConNotaVacia_PersisteNull(string? nota)
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.PostAsync(
            "/api/movimientos",
            Cuerpo(new { categoriaId = CategoriaComidaId, monto = 10m, fecha = FechaValida, nota }));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creado = await LeerJsonAsync(respuesta);
        Assert.Equal(JsonValueKind.Null, creado.GetProperty("nota").ValueKind);

        var persistido = await UnicoMovimientoAsync();
        Assert.Null(persistido.Nota);
    }

    [Fact]
    public async Task Crear_DerivaElTipoDeLaCategoria()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        // El cuerpo declara "gasto" en cada campo que el cliente podría usar para imponerlo: el
        // servidor tiene que ignorarlos y derivar el tipo de la categoría, que es de ingreso.
        using var respuesta = await cliente.PostAsync(
            "/api/movimientos",
            Cuerpo(new
            {
                categoriaId = CategoriaSueldoId,
                monto = 10m,
                fecha = FechaValida,
                nota = (string?)null,
                tipo = "gasto",
            }));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creado = await LeerJsonAsync(respuesta);
        Assert.Equal("ingreso", creado.GetProperty("tipo").GetString());

        var persistido = await UnicoMovimientoAsync();
        Assert.Equal(TipoMovimiento.Ingreso, persistido.Tipo);
    }

    // ---------------------------------------------------------------- overposting (R-04)

    [Fact]
    public async Task Crear_IgnoraUsuarioIdDelCuerpo()
    {
        await baseDeDatos.LimpiarAsync();
        var otroUsuarioId = await SembrarOtroUsuarioAsync();
        var usuarioSemillaId = await UsuarioSemillaIdAsync();
        Assert.NotEqual(otroUsuarioId, usuarioSemillaId);

        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.PostAsync(
            "/api/movimientos",
            Cuerpo(new
            {
                categoriaId = CategoriaComidaId,
                monto = 10m,
                fecha = FechaValida,
                nota = (string?)null,
                usuarioId = otroUsuarioId,
                id = 4242,
            }));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creado = await LeerJsonAsync(respuesta);
        Assert.NotEqual(4242, creado.GetProperty("id").GetInt32());

        // El propietario sale de IUsuarioActual, nunca del cuerpo: se lee sin el filtro global, con
        // SQL constante, porque justamente lo que se verifica es a quién quedó asignada la fila.
        var propietarios = await PropietariosDeLosMovimientosAsync();
        var propietario = Assert.Single(propietarios);
        Assert.Equal(usuarioSemillaId, propietario);
        Assert.NotEqual(otroUsuarioId, propietario);
    }

    [Fact]
    public async Task Crear_IgnoraMonedaDelCuerpo()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.PostAsync(
            "/api/movimientos",
            Cuerpo(new
            {
                categoriaId = CategoriaComidaId,
                monto = 10m,
                fecha = FechaValida,
                nota = (string?)null,
                moneda = "USD",
            }));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creado = await LeerJsonAsync(respuesta);
        Assert.Equal("ARS", creado.GetProperty("moneda").GetString());

        var persistido = await UnicoMovimientoAsync();
        Assert.Equal("ARS", persistido.Moneda);
    }

    // ---------------------------------------------------------------- caminos tristes

    [Fact]
    public Task Crear_MontoCero_Devuelve400() =>
        RechazaConErrorAsync(
            new { categoriaId = CategoriaComidaId, monto = 0m, fecha = FechaValida, nota = (string?)null },
            "monto",
            "El monto debe ser mayor a cero");

    [Fact]
    public Task Crear_MontoNegativo_Devuelve400() =>
        RechazaConErrorAsync(
            new { categoriaId = CategoriaComidaId, monto = -10.5m, fecha = FechaValida, nota = (string?)null },
            "monto",
            "El monto debe ser mayor a cero");

    [Fact]
    public Task Crear_MontoConTresDecimales_Devuelve400() =>
        RechazaConErrorAsync(
            new { categoriaId = CategoriaComidaId, monto = 10.123m, fecha = FechaValida, nota = (string?)null },
            "monto",
            "El monto admite como máximo 2 decimales");

    [Fact]
    public Task Crear_MontoAusente_Devuelve400() =>
        RechazaConErrorAsync(
            new { categoriaId = CategoriaComidaId, fecha = FechaValida, nota = (string?)null },
            "monto",
            "El monto es obligatorio y debe ser un número");

    [Fact]
    public Task Crear_MontoSuperaElTecho_Devuelve400() =>
        // Un dígito entero más de lo que entra en decimal(15,2): la regla del techo tiene que
        // rechazarlo como validación y no dejar que lo trunque —o lo rompa— la base.
        RechazaConErrorAsync(
            new { categoriaId = CategoriaComidaId, monto = 10000000000000.00m, fecha = FechaValida, nota = (string?)null },
            "monto",
            "El monto no puede superar 9999999999999.99");

    [Fact]
    public async Task Crear_MontoEnElTecho_Devuelve201YPersiste()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        // El límite es inclusivo: el valor exacto del techo es válido y tiene que persistirse sin
        // pérdida de precisión.
        using var respuesta = await cliente.PostAsync(
            "/api/movimientos",
            Cuerpo(new { categoriaId = CategoriaComidaId, monto = 9999999999999.99m, fecha = FechaValida, nota = (string?)null }));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creado = await LeerJsonAsync(respuesta);
        Assert.Equal(9999999999999.99m, creado.GetProperty("monto").GetDecimal());

        var persistido = await UnicoMovimientoAsync();
        Assert.Equal(9999999999999.99m, persistido.Monto);
    }

    // Cuarta forma de AC-08: el monto que no es un número JSON. Se manda el cuerpo crudo porque
    // serializar desde un objeto de C# solo puede producir un decimal válido, que es justo lo que
    // acá no se quiere probar.
    [Theory]
    [InlineData("\"abc\"")]
    [InlineData("\"1,5\"")]
    [InlineData("\"1,2,3\"")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("true")]
    [InlineData("1e400")]
    public async Task Crear_MontoNoNumerico_Devuelve400(string montoCrudo)
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.PostAsync(
            "/api/movimientos",
            CuerpoCrudo($$"""
                {"categoriaId": {{CategoriaComidaId}}, "monto": {{montoCrudo}}, "fecha": "{{FechaValida}}", "nota": null}
                """));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        var errores = await ErroresDeAsync(respuesta);
        Assert.True(
            errores.ContainsKey("monto"),
            $"Se esperaba un error en 'monto'; llegaron: {string.Join(", ", errores.Keys)}");
        Assert.Contains("El monto es obligatorio y debe ser un número", errores["monto"]);

        Assert.Equal(0, await CantidadDeMovimientosAsync());
    }

    // Las tres formas de "sin categoría" que llegan de un formulario: el campo que no viaja, el que
    // viaja nulo y el 0 del selector sin elegir. La regla de la spec nombra las tres.
    [Theory]
    [InlineData("omitida")]
    [InlineData("nula")]
    [InlineData("cero")]
    public Task Crear_SinCategoria_Devuelve400(string variante) =>
        RechazaConErrorAsync(
            variante switch
            {
                "omitida" => new { monto = 10m, fecha = FechaValida, nota = (string?)null },
                "nula" => (object)new { categoriaId = (int?)null, monto = 10m, fecha = FechaValida, nota = (string?)null },
                _ => new { categoriaId = 0, monto = 10m, fecha = FechaValida, nota = (string?)null },
            },
            "categoriaId",
            "La categoría es obligatoria");

    [Fact]
    public async Task Crear_CategoriaInexistente_Devuelve400NoQuinientos()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.PostAsync(
            "/api/movimientos",
            Cuerpo(new
            {
                categoriaId = CategoriaInexistenteId,
                monto = 10m,
                fecha = FechaValida,
                nota = (string?)null,
            }));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        var errores = await ErroresDeAsync(respuesta);
        Assert.Contains("La categoría no existe", errores["categoriaId"]);

        // La validación va ANTES del INSERT: dejar que reviente la FK daría un DbUpdateException
        // convertido en 500 por el manejador global.
        Assert.DoesNotContain(
            fabrica.ExcepcionesRegistradas,
            e => e is DbUpdateException || e.InnerException is MySqlException);
        Assert.Equal(0, await CantidadDeMovimientosAsync());
    }

    [Fact]
    public Task Crear_TipoCruzado_Devuelve400() =>
        // El formulario pide un gasto y manda una categoría de ingreso: el endpoint compara el
        // tipoEsperado explícito con el de la categoría.
        RechazaConErrorAsync(
            new
            {
                categoriaId = CategoriaSueldoId,
                tipoEsperado = "gasto",
                monto = 10m,
                fecha = FechaValida,
                nota = (string?)null,
            },
            "categoriaId",
            "La categoría 'Sueldo' es de tipo ingreso y no puede usarse en un movimiento de tipo gasto");

    [Fact]
    public Task Crear_NotaDeCientoVeintiuno_Devuelve400() =>
        RechazaConErrorAsync(
            new
            {
                categoriaId = CategoriaComidaId,
                monto = 10m,
                fecha = FechaValida,
                nota = new string('n', 121),
            },
            "nota",
            "La nota no puede superar los 120 caracteres");

    [Fact]
    public Task Crear_FechaConFormatoInvalido_Devuelve400() =>
        RechazaConErrorAsync(
            new { categoriaId = CategoriaComidaId, monto = 10m, fecha = "01/03/2026", nota = (string?)null },
            "fecha",
            "La fecha debe tener formato yyyy-MM-dd");

    [Fact]
    public async Task Crear_JsonMalformado_Devuelve400SinDetalleInterno()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var contenido = new StringContent("{ \"categoriaId\": ", Encoding.UTF8, "application/json");
        using var respuesta = await cliente.PostAsync("/api/movimientos", contenido);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        foreach (var detalleInterno in new[]
                 {
                     "JsonReaderException", "JsonException", "System.Text.Json", "LineNumber",
                     "BytePositionInLine", "stackTrace", "   at ",
                 })
        {
            Assert.DoesNotContain(detalleInterno, cuerpo, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(0, await CantidadDeMovimientosAsync());
    }

    [Fact]
    public async Task Crear_Rechazado_NoCreaNingunMovimiento()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        object[] cuerposRechazados =
        [
            new { categoriaId = CategoriaComidaId, monto = 0m, fecha = FechaValida },
            new { categoriaId = CategoriaComidaId, monto = -10m, fecha = FechaValida },
            new { categoriaId = CategoriaComidaId, monto = 10.123m, fecha = FechaValida },
            new { categoriaId = CategoriaComidaId, fecha = FechaValida },
            new { monto = 10m, fecha = FechaValida },
            new { categoriaId = CategoriaInexistenteId, monto = 10m, fecha = FechaValida },
            new { categoriaId = CategoriaSueldoId, tipoEsperado = "gasto", monto = 10m, fecha = FechaValida },
            new { categoriaId = CategoriaComidaId, monto = 10m, fecha = FechaValida, nota = new string('n', 121) },
        ];

        foreach (var cuerpo in cuerposRechazados)
        {
            using var respuesta = await cliente.PostAsync("/api/movimientos", Cuerpo(cuerpo));

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
            var errores = await ErroresDeAsync(respuesta);
            Assert.NotEmpty(errores);
            // La tabla entera, sin filtro de propietario: "no se crea ningún movimiento" es de toda
            // la tabla, no solo de lo que el usuario actual ve.
            Assert.Equal(0, await CantidadDeMovimientosAsync());
        }
    }

    // ---------------------------------------------------------------- GET /{id}

    [Fact]
    public async Task GetPorId_Existente_Devuelve200()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var creacion = await cliente.PostAsync(
            "/api/movimientos",
            Cuerpo(new { categoriaId = CategoriaComidaId, monto = 42.25m, fecha = FechaValida, nota = "Café" }));
        Assert.Equal(HttpStatusCode.Created, creacion.StatusCode);
        var ubicacion = creacion.Headers.Location!.ToString();

        using var respuesta = await cliente.GetAsync(ubicacion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var leido = await LeerJsonAsync(respuesta);
        var creado = await LeerJsonAsync(creacion);
        Assert.Equal(creado.GetProperty("id").GetInt32(), leido.GetProperty("id").GetInt32());
        Assert.Equal("gasto", leido.GetProperty("tipo").GetString());
        Assert.Equal("Comida", leido.GetProperty("categoria").GetProperty("nombre").GetString());
        Assert.Equal(42.25m, leido.GetProperty("monto").GetDecimal());
        Assert.Equal("ARS", leido.GetProperty("moneda").GetString());
        Assert.Equal(FechaValida, leido.GetProperty("fecha").GetString());
        Assert.Equal("Café", leido.GetProperty("nota").GetString());
    }

    [Fact]
    public async Task GetPorId_Inexistente_Devuelve404()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/movimientos/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        // El título es el del endpoint, no el que el ruteo genera cuando no hay ruta: sin esta
        // aserción el test daría verde con el endpoint sin implementar.
        await AssertTituloDeNoEncontradoAsync(respuesta);
    }

    [Fact]
    public async Task GetPorId_DeOtroPropietario_Devuelve404()
    {
        await baseDeDatos.LimpiarAsync();
        var idAjeno = await SembrarMovimientoDeOtroPropietarioAsync();
        Assert.Equal(1, await CantidadDeMovimientosAsync());

        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.GetAsync($"/api/movimientos/{idAjeno}");

        // Indistinguible de "no existe": el filtro global no debe dejar rastro de que la fila está.
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        await AssertTituloDeNoEncontradoAsync(respuesta);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("otro@gestiongastos.local", cuerpo, StringComparison.Ordinal);
        // Sobre la propiedad y no sobre la cadena: buscar el monto ajeno como subcadena chocaría con
        // el traceId hexadecimal del ProblemDetails y haría fallar el test sin que nada esté roto.
        var raiz = await LeerJsonAsync(respuesta);
        Assert.False(
            raiz.TryGetProperty("monto", out _),
            "El ProblemDetails del 404 no debe filtrar el monto del movimiento ajeno.");
    }

    [Fact]
    public async Task Crear_FalloDeBase_DevuelveProblemDetails500SinStackTrace()
    {
        await baseDeDatos.LimpiarAsync();

        // Precondición: la ruta del alta existe. Con la base caída todo termina en 500, así que sin
        // esto el test daría verde incluso sin endpoint. Un cuerpo inválido alcanza y no escribe.
        await using (var sana = new ApiFactory())
        {
            using var cliente = sana.CreateClient();
            using var sinCategoria = await cliente.PostAsync("/api/movimientos", Cuerpo(new { monto = 10m }));
            Assert.Equal(HttpStatusCode.BadRequest, sinCategoria.StatusCode);
        }

        await using var fabrica = new ApiFactory(ApiFactory.CadenaHaciaUnPuertoCerrado);
        using var clienteConBaseCaida = fabrica.CreateClient();

        using var respuesta = await clienteConBaseCaida.PostAsync("/api/movimientos", CuerpoValido());

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        // Correlación: sin esto el test daría verde con cualquier 500 de otra causa.
        Assert.Contains(
            fabrica.ExcepcionesRegistradas,
            e => Desenrollar(e).OfType<MySqlException>().Any());

        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        using var documento = JsonDocument.Parse(cuerpo);
        Assert.True(documento.RootElement.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));

        Assert.DoesNotContain("stackTrace", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("MySql", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- utilidades

    private static StringContent Cuerpo(object carga) =>
        new(JsonSerializer.Serialize(carga, Json), Encoding.UTF8, "application/json");

    /// <summary>
    /// Cuerpo tal cual, sin pasar por el serializador: es la única forma de mandar un <c>monto</c>
    /// que no sea un número JSON válido.
    /// </summary>
    private static StringContent CuerpoCrudo(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static StringContent CuerpoValido() =>
        Cuerpo(new { categoriaId = CategoriaComidaId, monto = 10m, fecha = FechaValida, nota = (string?)null });

    private static async Task<JsonElement> LeerJsonAsync(HttpResponseMessage respuesta)
    {
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        // Clone: el JsonDocument se descarta al salir y el elemento quedaría apuntando a memoria
        // devuelta al pool.
        using var documento = JsonDocument.Parse(cuerpo);
        return documento.RootElement.Clone();
    }

    /// <summary>
    /// El 404 lo produce el endpoint, con su propio título, y no el ruteo por falta de ruta: los dos
    /// devuelven 404 <c>problem+json</c> y sin distinguirlos el test no vigila nada.
    /// </summary>
    private static async Task AssertTituloDeNoEncontradoAsync(HttpResponseMessage respuesta)
    {
        var raiz = await LeerJsonAsync(respuesta);
        Assert.True(raiz.TryGetProperty("title", out var titulo), "El ProblemDetails no trae 'title'.");
        Assert.Equal("Movimiento no encontrado", titulo.GetString());
    }

    private static async Task<Dictionary<string, string[]>> ErroresDeAsync(HttpResponseMessage respuesta)
    {
        var raiz = await LeerJsonAsync(respuesta);
        Assert.True(raiz.TryGetProperty("errors", out var errores), "El ProblemDetails no trae la extensión 'errors'.");
        return errores.EnumerateObject().ToDictionary(
            p => p.Name,
            p => p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray(),
            StringComparer.Ordinal);
    }

    private async Task RechazaConErrorAsync(object carga, string campo, string? mensajeEsperado = null)
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.PostAsync("/api/movimientos", Cuerpo(carga));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        var errores = await ErroresDeAsync(respuesta);
        Assert.True(errores.ContainsKey(campo), $"Se esperaba un error en '{campo}'; llegaron: {string.Join(", ", errores.Keys)}");
        Assert.NotEmpty(errores[campo]);
        Assert.All(errores[campo], m => Assert.False(string.IsNullOrWhiteSpace(m)));
        if (mensajeEsperado is not null)
        {
            Assert.Contains(mensajeEsperado, errores[campo]);
        }

        Assert.Equal(0, await CantidadDeMovimientosAsync());
    }

    private async Task<Movimiento> UnicoMovimientoAsync()
    {
        await using var contexto = baseDeDatos.CrearContexto();
        contexto.UsuarioActualId = await new UsuarioSemillaActual(contexto).ObtenerIdAsync();
        return await contexto.Movimientos.AsNoTracking().SingleAsync();
    }

    private async Task<int> UsuarioSemillaIdAsync()
    {
        await using var contexto = baseDeDatos.CrearContexto();
        return await new UsuarioSemillaActual(contexto).ObtenerIdAsync();
    }

    private async Task<int> SembrarOtroUsuarioAsync()
    {
        await using var contexto = baseDeDatos.CrearContexto();
        var otro = new Usuario { Email = "otro@gestiongastos.local", CreadoEn = DateTime.UtcNow };
        contexto.Usuarios.Add(otro);
        await contexto.SaveChangesAsync();
        return otro.Id;
    }

    private async Task<int> SembrarMovimientoDeOtroPropietarioAsync()
    {
        var otroUsuarioId = await SembrarOtroUsuarioAsync();
        await using var contexto = baseDeDatos.CrearContexto();
        var ajeno = new Movimiento
        {
            UsuarioId = otroUsuarioId,
            CategoriaId = CategoriaComidaId,
            Tipo = TipoMovimiento.Gasto,
            Monto = 999m,
            Moneda = Moneda.Predeterminada,
            Fecha = new DateOnly(2026, 3, 1),
            CreadoEn = DateTime.UtcNow,
        };
        contexto.Movimientos.Add(ajeno);
        await contexto.SaveChangesAsync();
        return ajeno.Id;
    }

    /// <summary>
    /// Cuenta la tabla entera, sin el filtro global de propietario. SQL constante, sin
    /// interpolación de nada que venga de afuera (mitigación R-05).
    /// </summary>
    private static async Task<int> CantidadDeMovimientosAsync()
    {
        await using var conexion = new MySqlConnection(BaseDeDatosFixture.CadenaDeConexion);
        await conexion.OpenAsync();
        await using var comando = new MySqlCommand("SELECT COUNT(*) FROM movimientos", conexion);
        return Convert.ToInt32(await comando.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<int>> PropietariosDeLosMovimientosAsync()
    {
        await using var conexion = new MySqlConnection(BaseDeDatosFixture.CadenaDeConexion);
        await conexion.OpenAsync();
        await using var comando = new MySqlCommand("SELECT usuario_id FROM movimientos", conexion);
        var propietarios = new List<int>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            propietarios.Add(lector.GetInt32(0));
        }

        return propietarios;
    }

    private static IEnumerable<Exception> Desenrollar(Exception excepcion)
    {
        for (var actual = excepcion; actual is not null; actual = actual.InnerException)
        {
            yield return actual;
        }
    }
}
