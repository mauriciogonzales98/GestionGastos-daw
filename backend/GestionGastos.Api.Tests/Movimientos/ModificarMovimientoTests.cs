using System.Net;
using System.Text;
using System.Text.Json;
using GestionGastos.Api.Common;
using GestionGastos.Api.Data.Entidades;
using GestionGastos.Api.Tests.Infra;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Movimientos;

/// <summary>
/// <c>PUT /api/movimientos/{id}</c>. La modificación comparte la validación del alta —no una copia
/// (mitigación R-16)— y localiza la fila con una lectura filtrada por propietario, de modo que un
/// movimiento ajeno sea indistinguible de uno inexistente (mitigaciones R-15 y R-17).
/// </summary>
[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class ModificarMovimientoTests(BaseDeDatosFixture baseDeDatos)
{
    private const int CategoriaComidaId = 1;
    private const int CategoriaTransporteId = 2;
    private const int CategoriaSueldoId = 8;
    private const int CategoriaInexistenteId = 987654;

    private const decimal MontoOriginal = 1234.56m;
    private const string FechaOriginal = "2026-03-05";
    private const string NotaOriginal = "Nota original";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---------------------------------------------------------------- camino feliz

    [Fact]
    public async Task Modificar_ElMonto_LoRefleja()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        var id = await SembrarGastoAsync(cliente);

        using var respuesta = await cliente.PutAsync(
            $"/api/movimientos/{id}",
            Cuerpo(new
            {
                categoriaId = CategoriaComidaId,
                monto = 2500.75m,
                fecha = FechaOriginal,
                nota = NotaOriginal,
            }));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var modificado = await JsonDeRespuesta.LeerAsync(respuesta);
        Assert.Equal(id, modificado.GetProperty("id").GetInt32());
        Assert.Equal(2500.75m, modificado.GetProperty("monto").GetDecimal());

        var persistido = await MovimientoPropioAsync(id);
        Assert.Equal(2500.75m, persistido.Monto);

        // AC-01 habla del listado: el monto nuevo tiene que ser el que se ve, y no el anterior.
        using var listado = await cliente.GetAsync("/api/movimientos");
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        var items = (await JsonDeRespuesta.LeerAsync(listado)).GetProperty("items");
        var fila = items.EnumerateArray().Single(m => m.GetProperty("id").GetInt32() == id);
        Assert.Equal(2500.75m, fila.GetProperty("monto").GetDecimal());
        Assert.NotEqual(MontoOriginal, fila.GetProperty("monto").GetDecimal());
    }

    [Fact]
    public async Task Modificar_CategoriaYFecha_PersisteAmbas()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        var id = await SembrarGastoAsync(cliente);

        using var respuesta = await cliente.PutAsync(
            $"/api/movimientos/{id}",
            Cuerpo(new
            {
                categoriaId = CategoriaTransporteId,
                monto = MontoOriginal,
                fecha = "2026-04-20",
                nota = NotaOriginal,
            }));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var modificado = await JsonDeRespuesta.LeerAsync(respuesta);
        Assert.Equal(CategoriaTransporteId, modificado.GetProperty("categoria").GetProperty("id").GetInt32());
        Assert.Equal("Transporte", modificado.GetProperty("categoria").GetProperty("nombre").GetString());
        Assert.Equal("2026-04-20", modificado.GetProperty("fecha").GetString());
        // El tipo no cambia: sigue derivando del movimiento, no de un campo del cuerpo.
        Assert.Equal("gasto", modificado.GetProperty("tipo").GetString());

        var persistido = await MovimientoPropioAsync(id);
        Assert.Equal(CategoriaTransporteId, persistido.CategoriaId);
        Assert.Equal(new DateOnly(2026, 4, 20), persistido.Fecha);
        Assert.Equal(TipoMovimiento.Gasto, persistido.Tipo);
    }

    [Fact]
    public async Task Modificar_LaNota_LaRefleja()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        var id = await SembrarGastoAsync(cliente);

        using var respuesta = await cliente.PutAsync(
            $"/api/movimientos/{id}",
            Cuerpo(new
            {
                categoriaId = CategoriaComidaId,
                monto = MontoOriginal,
                fecha = FechaOriginal,
                nota = "Nota corregida",
            }));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var modificado = await JsonDeRespuesta.LeerAsync(respuesta);
        Assert.Equal("Nota corregida", modificado.GetProperty("nota").GetString());

        Assert.Equal("Nota corregida", (await MovimientoPropioAsync(id)).Nota);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Modificar_BorrandoLaNota_GuardaNull(string? nota)
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        var id = await SembrarGastoAsync(cliente);
        Assert.Equal(NotaOriginal, (await MovimientoPropioAsync(id)).Nota);

        using var respuesta = await cliente.PutAsync(
            $"/api/movimientos/{id}",
            Cuerpo(new
            {
                categoriaId = CategoriaComidaId,
                monto = MontoOriginal,
                fecha = FechaOriginal,
                nota,
            }));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var modificado = await JsonDeRespuesta.LeerAsync(respuesta);
        Assert.Equal(JsonValueKind.Null, modificado.GetProperty("nota").ValueKind);

        // En blanco es ausencia de nota, igual que en el alta: no una nota de espacios.
        Assert.Null((await MovimientoPropioAsync(id)).Nota);
    }

    // ---------------------------------------------------------------- caminos tristes (AC-04)

    [Theory]
    [InlineData(0)]
    [InlineData(-10.5)]
    public Task Modificar_ConMontoInvalido_Devuelve400YNoAltera(double monto) =>
        RechazaSinAlterarAsync(
            new
            {
                categoriaId = CategoriaComidaId,
                monto = (decimal)monto,
                fecha = FechaOriginal,
                nota = NotaOriginal,
            },
            "monto",
            "El monto debe ser mayor a cero");

    [Fact]
    public Task Modificar_ConMontoDeTresDecimales_Devuelve400YNoAltera() =>
        RechazaSinAlterarAsync(
            new
            {
                categoriaId = CategoriaComidaId,
                monto = 10.123m,
                fecha = FechaOriginal,
                nota = NotaOriginal,
            },
            "monto",
            "El monto admite como máximo 2 decimales");

    [Theory]
    [InlineData("omitida")]
    [InlineData("nula")]
    [InlineData("cero")]
    public Task Modificar_SinCategoria_Devuelve400YNoAltera(string variante) =>
        RechazaSinAlterarAsync(
            variante switch
            {
                "omitida" => new { monto = 99m, fecha = FechaOriginal, nota = NotaOriginal },
                "nula" => (object)new
                {
                    categoriaId = (int?)null,
                    monto = 99m,
                    fecha = FechaOriginal,
                    nota = NotaOriginal,
                },
                _ => new
                {
                    categoriaId = 0,
                    monto = 99m,
                    fecha = FechaOriginal,
                    nota = NotaOriginal,
                },
            },
            "categoriaId",
            "La categoría es obligatoria");

    [Fact]
    public Task Modificar_ConCategoriaDeOtroTipo_Devuelve400YNoAltera() =>
        // El movimiento es un gasto y la categoría es de ingreso: cambiar el tipo está fuera de
        // alcance por PRD, y el mensaje tiene que nombrar los dos tipos para que se entienda.
        RechazaSinAlterarAsync(
            new
            {
                categoriaId = CategoriaSueldoId,
                monto = 99m,
                fecha = FechaOriginal,
                nota = NotaOriginal,
            },
            "categoriaId",
            "La categoría 'Sueldo' es de tipo ingreso y no puede usarse en un movimiento de tipo gasto");

    [Fact]
    public Task Modificar_ConCategoriaInexistente_Devuelve400YNoAltera() =>
        RechazaSinAlterarAsync(
            new
            {
                categoriaId = CategoriaInexistenteId,
                monto = 99m,
                fecha = FechaOriginal,
                nota = NotaOriginal,
            },
            "categoriaId",
            "La categoría no existe");

    [Fact]
    public Task Modificar_ConNotaDemasiadoLarga_Devuelve400YNoAltera() =>
        RechazaSinAlterarAsync(
            new
            {
                categoriaId = CategoriaComidaId,
                monto = 99m,
                fecha = FechaOriginal,
                nota = new string('n', 121),
            },
            "nota",
            "La nota no puede superar los 120 caracteres");

    [Fact]
    public async Task Modificar_ConMontoNoNumerico_Devuelve400ConElCampo()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        var id = await SembrarGastoAsync(cliente);

        // Cuerpo crudo: serializar desde C# solo puede producir un decimal válido. Sin repetir el
        // JsonConverter del monto en el request del PUT —los atributos no se heredan de la
        // interfaz—, esto se degrada a un 400 genérico sin 'errors.monto'.
        using var contenido = new StringContent(
            $$"""
              {"categoriaId": {{CategoriaComidaId}}, "monto": "abc", "fecha": "{{FechaOriginal}}", "nota": null}
              """,
            Encoding.UTF8,
            "application/json");
        using var respuesta = await cliente.PutAsync($"/api/movimientos/{id}", contenido);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        var errores = await ErroresDeAsync(respuesta);
        Assert.True(
            errores.ContainsKey("monto"),
            $"Se esperaba un error en 'monto'; llegaron: {string.Join(", ", errores.Keys)}");
        Assert.Contains("El monto es obligatorio y debe ser un número", errores["monto"]);

        await AssertIntactoAsync(id);
    }

    // ---------------------------------------------------------------- 404 indistinguible (AC-06)

    [Fact]
    public async Task Modificar_Inexistente_Devuelve404()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.PutAsync("/api/movimientos/999999", CuerpoValido());

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        // El título es el del endpoint y no el que produce el ruteo por falta de ruta: sin esta
        // aserción el test daría verde con el PUT sin implementar.
        await AssertTituloDeNoEncontradoAsync(respuesta);
    }

    [Fact]
    public async Task Modificar_DeOtroPropietario_Devuelve404()
    {
        await baseDeDatos.LimpiarAsync();
        var (otroUsuarioId, idAjeno) = await SembrarMovimientoDeOtroPropietarioAsync();

        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.PutAsync(
            $"/api/movimientos/{idAjeno}",
            Cuerpo(new
            {
                categoriaId = CategoriaTransporteId,
                monto = 1m,
                fecha = "2026-01-01",
                nota = "Intervenido",
            }));

        // Indistinguible de "no existe": mismo estado y mismo título que el inexistente.
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        await AssertTituloDeNoEncontradoAsync(respuesta);

        // Y la fila ajena sigue exactamente como estaba (mitigación R-15).
        var ajeno = await MovimientoDeAsync(otroUsuarioId, idAjeno);
        Assert.Equal(999m, ajeno.Monto);
        Assert.Equal(CategoriaComidaId, ajeno.CategoriaId);
        Assert.Equal(new DateOnly(2026, 3, 1), ajeno.Fecha);
        Assert.Null(ajeno.Nota);
        Assert.Equal(otroUsuarioId, ajeno.UsuarioId);
    }

    // ---------------------------------------------------------------- overposting (R-15)

    [Fact]
    public async Task Modificar_ConCuerpoQueTraeUsuarioId_LoIgnora()
    {
        await baseDeDatos.LimpiarAsync();
        var otroUsuarioId = await SembrarOtroUsuarioAsync();
        var usuarioSemillaId = await UsuarioSemillaIdAsync();
        Assert.NotEqual(otroUsuarioId, usuarioSemillaId);

        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        var id = await SembrarGastoAsync(cliente);

        using var respuesta = await cliente.PutAsync(
            $"/api/movimientos/{id}",
            Cuerpo(new
            {
                categoriaId = CategoriaComidaId,
                monto = 55m,
                fecha = FechaOriginal,
                nota = NotaOriginal,
                usuarioId = otroUsuarioId,
                id = 4242,
                tipo = "ingreso",
                moneda = "USD",
            }));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var modificado = await JsonDeRespuesta.LeerAsync(respuesta);
        Assert.Equal(id, modificado.GetProperty("id").GetInt32());
        Assert.Equal("gasto", modificado.GetProperty("tipo").GetString());
        Assert.Equal("ARS", modificado.GetProperty("moneda").GetString());

        // El propietario no se reasigna: la única fila de la tabla sigue siendo del usuario actual.
        var propietarios = await MovimientosEnLaBase.PropietariosAsync();
        var propietario = Assert.Single(propietarios);
        Assert.Equal(usuarioSemillaId, propietario);
        Assert.NotEqual(otroUsuarioId, propietario);

        var persistido = await MovimientoPropioAsync(id);
        Assert.Equal(55m, persistido.Monto);
        Assert.Equal(TipoMovimiento.Gasto, persistido.Tipo);
        Assert.Equal(Moneda.Predeterminada, persistido.Moneda);
    }

    // ---------------------------------------------------------------- utilidades

    private static StringContent Cuerpo(object carga) =>
        new(JsonSerializer.Serialize(carga, Json), Encoding.UTF8, "application/json");

    private static StringContent CuerpoValido() =>
        Cuerpo(new
        {
            categoriaId = CategoriaComidaId,
            monto = 10m,
            fecha = FechaOriginal,
            nota = (string?)null,
        });

    /// <summary>
    /// Alta por el endpoint del alta, para que el movimiento a modificar sea uno de verdad y no una
    /// fila armada a mano que podría no parecerse a lo que la API produce.
    /// </summary>
    private static async Task<int> SembrarGastoAsync(HttpClient cliente)
    {
        using var creacion = await cliente.PostAsync(
            "/api/movimientos",
            Cuerpo(new
            {
                categoriaId = CategoriaComidaId,
                monto = MontoOriginal,
                fecha = FechaOriginal,
                nota = NotaOriginal,
            }));

        Assert.Equal(HttpStatusCode.Created, creacion.StatusCode);
        return (await JsonDeRespuesta.LeerAsync(creacion)).GetProperty("id").GetInt32();
    }

    private static async Task<Dictionary<string, string[]>> ErroresDeAsync(HttpResponseMessage respuesta)
    {
        var raiz = await JsonDeRespuesta.LeerAsync(respuesta);
        Assert.True(raiz.TryGetProperty("errors", out var errores), "El ProblemDetails no trae la extensión 'errors'.");
        return errores.EnumerateObject().ToDictionary(
            p => p.Name,
            p => p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray(),
            StringComparer.Ordinal);
    }

    private static async Task AssertTituloDeNoEncontradoAsync(HttpResponseMessage respuesta)
    {
        var raiz = await JsonDeRespuesta.LeerAsync(respuesta);
        Assert.True(raiz.TryGetProperty("title", out var titulo), "El ProblemDetails no trae 'title'.");
        Assert.Equal("Movimiento no encontrado", titulo.GetString());
    }

    /// <summary>
    /// Manda una modificación inválida sobre un movimiento existente y verifica el 400, el campo, el
    /// mensaje exacto y —lo que AC-04 pide de verdad— que la fila quedó con TODOS sus valores
    /// anteriores.
    /// </summary>
    private async Task RechazaSinAlterarAsync(object carga, string campo, string mensajeEsperado)
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        var id = await SembrarGastoAsync(cliente);

        using var respuesta = await cliente.PutAsync($"/api/movimientos/{id}", Cuerpo(carga));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        var errores = await ErroresDeAsync(respuesta);
        Assert.True(errores.ContainsKey(campo), $"Se esperaba un error en '{campo}'; llegaron: {string.Join(", ", errores.Keys)}");
        Assert.All(errores[campo], m => Assert.False(string.IsNullOrWhiteSpace(m)));
        Assert.Contains(mensajeEsperado, errores[campo]);

        await AssertIntactoAsync(id);
    }

    private async Task AssertIntactoAsync(int id)
    {
        var persistido = await MovimientoPropioAsync(id);
        Assert.Equal(MontoOriginal, persistido.Monto);
        Assert.Equal(CategoriaComidaId, persistido.CategoriaId);
        Assert.Equal(DateOnly.Parse(FechaOriginal, System.Globalization.CultureInfo.InvariantCulture), persistido.Fecha);
        Assert.Equal(NotaOriginal, persistido.Nota);
        Assert.Equal(TipoMovimiento.Gasto, persistido.Tipo);
    }

    private async Task<Movimiento> MovimientoPropioAsync(int id) =>
        await MovimientoDeAsync(await UsuarioSemillaIdAsync(), id);

    /// <summary>
    /// Lee la fila con el filtro global publicado para ese propietario, que es la única forma de
    /// mirar un movimiento ajeno sin <c>IgnoreQueryFilters</c> (ADR-003).
    /// </summary>
    private async Task<Movimiento> MovimientoDeAsync(int usuarioId, int id)
    {
        await using var contexto = baseDeDatos.CrearContexto();
        contexto.UsuarioActualId = usuarioId;
        return await contexto.Movimientos.AsNoTracking().SingleAsync(m => m.Id == id);
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

    /// <summary>
    /// No publica <c>UsuarioActualId</c> para insertar: el filtro global se aplica a las consultas,
    /// no a los INSERT, y el propietario se fija a mano acá.
    /// </summary>
    private async Task<(int UsuarioId, int MovimientoId)> SembrarMovimientoDeOtroPropietarioAsync()
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
        return (otroUsuarioId, ajeno.Id);
    }
}
