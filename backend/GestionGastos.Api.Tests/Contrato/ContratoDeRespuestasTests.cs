using System.Net;
using System.Text.Json;
using GestionGastos.Api.Common;
using GestionGastos.Api.Data.Entidades;
using GestionGastos.Api.Tests.Infra;

namespace GestionGastos.Api.Tests.Contrato;

/// <summary>
/// Verifica que lo que el backend emite coincide con lo que el frontend declara en
/// <c>frontend/src/api/tipos.ts</c>.
/// </summary>
/// <remarks>
/// Antes de este ticket había dos definiciones del contrato —los <c>record</c> de C# y las
/// interfaces de TypeScript— y nada las comparaba. Un rename coherente del lado del backend dejaba
/// el build, los 142 tests, <c>tsc</c>, los 105 de Vitest y ESLint en verde, y llegaba
/// <c>undefined</c> a la pantalla. Estos tests son el punto donde las dos definiciones se miran.
/// </remarks>
[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class ContratoDeRespuestasTests(BaseDeDatosFixture baseDeDatos)
{
    private const int CategoriaComidaId = 1;
    private const int CategoriaSueldoId = 8;

    /// <summary>Endpoint → tipo del frontend que declara su cuerpo de respuesta.</summary>
    private static readonly (string Endpoint, string Tipo)[] Respuestas =
    [
        ("/api/categorias", "CategoriaDto"),
        ("/api/movimientos", "ListadoMovimientosResponse"),
        ("/api/movimientos/{id}", "MovimientoDto"),
        ("/api/resumen", "ResumenMensual"),
    ];

    // ---------------------------------------------------------------- los cuatro endpoints

    [Fact]
    public async Task Contrato_Categorias_CoincideConElTipoDelFrontend()
    {
        var cuerpo = await ObtenerAsync("/api/categorias");

        Assert.Equal(JsonValueKind.Array, cuerpo.ValueKind);
        var elementos = cuerpo.EnumerateArray().ToList();
        Assert.NotEmpty(elementos);

        AfirmarQueCoincide("CategoriaDto", elementos[0], "/api/categorias");
    }

    [Fact]
    public async Task Contrato_Movimientos_CoincideConElTipoDelFrontend()
    {
        await SembrarUnMovimientoAsync();

        var cuerpo = await ObtenerAsync("/api/movimientos");

        AfirmarQueCoincide("ListadoMovimientosResponse", cuerpo, "/api/movimientos");

        // El anidado se recorre solo, pero se afirma que había algo que recorrer: un `items` vacío
        // dejaría MovimientoDto sin verificar y el test seguiría verde.
        Assert.NotEmpty(cuerpo.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Contrato_MovimientoPorId_CoincideConElTipoDelFrontend()
    {
        var id = await SembrarUnMovimientoAsync();

        var cuerpo = await ObtenerAsync($"/api/movimientos/{id}");

        AfirmarQueCoincide("MovimientoDto", cuerpo, "/api/movimientos/{id}");
    }

    [Fact]
    public async Task Contrato_Resumen_CoincideConElTipoDelFrontend()
    {
        await SembrarUnMovimientoAsync();

        var cuerpo = await ObtenerAsync("/api/resumen");

        AfirmarQueCoincide("ResumenMensual", cuerpo, "/api/resumen");
        Assert.NotEmpty(cuerpo.GetProperty("desglose").EnumerateArray());
    }

    // ---------------------------------------------------------------- las cuatro direcciones

    [Fact]
    public void Comparador_CampoEnElJsonQueElFrontendNoDeclara_LoReporta()
    {
        var contrato = LectorDeTiposDelFrontend.LeerElContratoCompleto();
        var json = JsonDeRespuesta.Raiz(
            """{"categoriaId":1,"categoriaNombre":"Comida","total":10,"promedio":5}""");

        var diferencias = ComparadorDeFormas.Comparar(
            contrato, "CategoriaConTotal", json, "/api/resumen");

        var diferencia = Assert.Single(diferencias);
        Assert.Contains("promedio", diferencia.Ruta, StringComparison.Ordinal);
        Assert.Contains("no lo declara", diferencia.Detalle, StringComparison.Ordinal);
    }

    [Fact]
    public void Comparador_CampoDeclaradoQueElJsonNoTrae_LoReporta()
    {
        var contrato = LectorDeTiposDelFrontend.LeerElContratoCompleto();
        var json = JsonDeRespuesta.Raiz("""{"categoriaId":1,"categoriaNombre":"Comida"}""");

        var diferencias = ComparadorDeFormas.Comparar(
            contrato, "CategoriaConTotal", json, "/api/resumen");

        var diferencia = Assert.Single(diferencias);
        Assert.Contains("total", diferencia.Ruta, StringComparison.Ordinal);
        Assert.Contains("undefined", diferencia.Detalle, StringComparison.Ordinal);
    }

    [Fact]
    public void Comparador_MismoCampoConTipoIncompatible_LoReporta()
    {
        var contrato = LectorDeTiposDelFrontend.LeerElContratoCompleto();
        var json = JsonDeRespuesta.Raiz(
            """{"categoriaId":1,"categoriaNombre":"Comida","total":"10"}""");

        var diferencias = ComparadorDeFormas.Comparar(
            contrato, "CategoriaConTotal", json, "/api/resumen");

        var diferencia = Assert.Single(diferencias);
        Assert.Contains("total", diferencia.Ruta, StringComparison.Ordinal);
        Assert.Contains("number", diferencia.Detalle, StringComparison.Ordinal);
        Assert.Contains("String", diferencia.Detalle, StringComparison.Ordinal);
    }

    [Fact]
    public void Comparador_ValorFueraDeLaUnionDeLiterales_LoReporta()
    {
        var contrato = LectorDeTiposDelFrontend.LeerElContratoCompleto();
        var json = JsonDeRespuesta.Raiz("""{"id":1,"nombre":"Comida","tipo":"Gasto"}""");

        var diferencias = ComparadorDeFormas.Comparar(
            contrato, "CategoriaDto", json, "/api/categorias");

        // El tipo JSON coincide —es String— pero el valor no está en la unión. El frontend compara
        // contra los literales en minúscula, así que "Gasto" rompe el contrato igual.
        var diferencia = Assert.Single(diferencias);
        Assert.Contains("Gasto", diferencia.Detalle, StringComparison.Ordinal);
        Assert.Contains("gasto, ingreso", diferencia.Detalle, StringComparison.Ordinal);
    }

    [Fact]
    public void Comparador_AlReportar_NombraCampoEndpointYDiferencia()
    {
        var contrato = LectorDeTiposDelFrontend.LeerElContratoCompleto();
        var json = JsonDeRespuesta.Raiz(
            """{"categoriaId":1,"categoriaNombre":"Comida","total":"10"}""");

        var diferencia = Assert.Single(ComparadorDeFormas.Comparar(
            contrato, "CategoriaConTotal", json, "/api/resumen"));

        // Los tres, porque AC-06 pide los tres: sin el endpoint, un campo repetido entre tipos no
        // dice dónde mirar.
        Assert.Contains("/api/resumen", diferencia.Ruta, StringComparison.Ordinal);
        Assert.Contains("total", diferencia.Ruta, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(diferencia.Detalle));
    }

    // ---------------------------------------------------------------- sad paths

    [Fact]
    public async Task Contrato_SiElEndpointNoDevuelve2xx_FallaSinReportarVerificado()
    {
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        using var respuesta = await cliente.GetAsync("/api/movimientos/999999");

        Assert.NotEqual(HttpStatusCode.OK, respuesta.StatusCode);

        // Sin cuerpo comparable no hay contrato verificado: se distingue de "el contrato coincide",
        // que es lo que AC-15 pide y lo que evita que un endpoint caído se lea como éxito.
        var error = Assert.Throws<InvalidOperationException>(
            () => ExigirCuerpoComparable(respuesta.StatusCode, "/api/movimientos/{id}"));

        Assert.Contains("no se pudo obtener el cuerpo", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Contrato_SiLaColeccionVieneVacia_FallaEnVezDeDarPorVerificado()
    {
        await baseDeDatos.LimpiarAsync();

        var cuerpo = await ObtenerAsync("/api/movimientos");
        var items = cuerpo.GetProperty("items");

        Assert.Empty(items.EnumerateArray());

        // Recorrer una colección vacía no compara nada, y "0 diferencias" se leería como éxito.
        Assert.Empty(ComparadorDeFormas.Comparar(
            LectorDeTiposDelFrontend.LeerElContratoCompleto(),
            "ListadoMovimientosResponse",
            cuerpo,
            "/api/movimientos"));
        Assert.Throws<InvalidOperationException>(
            () => ExigirColeccionNoVacia(items, "/api/movimientos", "items"));
    }

    [Fact]
    public void Comparador_ConTiposMutuamenteReferenciados_LanzaNombrandoElCiclo()
    {
        // Mitigación R-04: hoy el grafo de tipos.ts es acíclico, pero un test colgado se ve igual
        // que uno que todavía corre, así que el recorrido tiene que cortar.
        var contrato = LectorDeTiposDelFrontend.Parsear(
            """
            export interface Ida {
              vuelta: Vuelta;
            }

            export interface Vuelta {
              ida: Ida;
            }
            """,
            "fragmento-ciclico.ts");

        var json = JsonDeRespuesta.Raiz("""{"vuelta":{"ida":{"vuelta":{}}}}""");

        var error = Assert.Throws<InvalidOperationException>(
            () => ComparadorDeFormas.Comparar(contrato, "Ida", json, "/api/prueba"));

        Assert.Contains("ciclo", error.Message, StringComparison.Ordinal);
        Assert.Contains("Ida", error.Message, StringComparison.Ordinal);
        Assert.Contains("Vuelta", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Contrato_TodoTipoDeRespuesta_EstaVerificadoOExcluidoConMotivo()
    {
        var contrato = LectorDeTiposDelFrontend.LeerElContratoCompleto();

        // Los tipos que se alcanzan recorriendo desde las raíces de los cuatro endpoints.
        var alcanzados = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, raiz) in Respuestas)
        {
            Alcanzar(contrato, raiz, alcanzados);
        }

        var peticiones = new[] { "CrearMovimientoRequest", "ModificarMovimientoRequest" };

        foreach (var tipo in contrato.Tipos)
        {
            var cubierto = alcanzados.Contains(tipo.Nombre)
                || peticiones.Contains(tipo.Nombre, StringComparer.Ordinal)
                || TiposExcluidos.PorNombre.ContainsKey(tipo.Nombre);

            // Sin esta afirmación, un tipo nuevo en tipos.ts quedaría sin verificar y sin que nadie
            // se entere: parecería verificado, que es el defecto que este ticket arregla (NFR-05).
            Assert.True(
                cubierto,
                $"«{tipo.Nombre}» no está verificado por ningún endpoint ni excluido con motivo en " +
                "TiposExcluidos. Agregalo a la verificación o declaralo como excluido.");
        }
    }

    // ---------------------------------------------------------------- helpers

    private static void Alcanzar(
        ContratoDelFrontend contrato,
        string nombre,
        HashSet<string> alcanzados)
    {
        if (!alcanzados.Add(nombre))
        {
            return;
        }

        var tipo = contrato.Tipos.SingleOrDefault(t =>
            string.Equals(t.Nombre, nombre, StringComparison.Ordinal));

        foreach (var campo in tipo?.Campos ?? [])
        {
            if (contrato.Tipos.Any(t => string.Equals(t.Nombre, campo.TipoBase, StringComparison.Ordinal)))
            {
                Alcanzar(contrato, campo.TipoBase, alcanzados);
            }
        }
    }

    private static void AfirmarQueCoincide(string tipo, JsonElement cuerpo, string endpoint)
    {
        var diferencias = ComparadorDeFormas.Comparar(
            LectorDeTiposDelFrontend.LeerElContratoCompleto(), tipo, cuerpo, endpoint);

        Assert.True(
            diferencias.Count == 0,
            $"El contrato de {endpoint} no coincide con «{tipo}» tal como lo declara el frontend:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, diferencias.Select(d => "  - " + d)));
    }

    private static void ExigirCuerpoComparable(HttpStatusCode estado, string endpoint)
    {
        if (estado is not (HttpStatusCode.OK or HttpStatusCode.Created))
        {
            throw new InvalidOperationException(
                $"no se pudo obtener el cuerpo de {endpoint}: el backend respondió {(int)estado}. " +
                "El contrato queda SIN verificar; esto no es «coincide».");
        }
    }

    private static void ExigirColeccionNoVacia(JsonElement coleccion, string endpoint, string campo)
    {
        if (!coleccion.EnumerateArray().Any())
        {
            throw new InvalidOperationException(
                $"«{campo}» de {endpoint} vino vacío: sin al menos un elemento no hay nada que " +
                "comparar, y «0 diferencias» no significa que el contrato coincida.");
        }
    }

    private static async Task<JsonElement> ObtenerAsync(string ruta)
    {
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        using var respuesta = await cliente.GetAsync(ruta);

        ExigirCuerpoComparable(respuesta.StatusCode, ruta);
        return await JsonDeRespuesta.LeerAsync(respuesta);
    }

    private async Task<int> SembrarUnMovimientoAsync()
    {
        await baseDeDatos.LimpiarAsync();

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        await using var contexto = baseDeDatos.CrearContexto();
        var usuarioId = await new UsuarioSemillaActual(contexto).ObtenerIdAsync();
        contexto.UsuarioActualId = usuarioId;

        var gasto = new Movimiento
        {
            UsuarioId = usuarioId,
            Tipo = TipoMovimiento.Gasto,
            CategoriaId = CategoriaComidaId,
            Monto = 123.45m,
            Fecha = new DateOnly(hoy.Year, hoy.Month, 1),
            Nota = "verificación de contrato",
        };

        var ingreso = new Movimiento
        {
            UsuarioId = usuarioId,
            Tipo = TipoMovimiento.Ingreso,
            CategoriaId = CategoriaSueldoId,
            Monto = 1000m,
            Fecha = new DateOnly(hoy.Year, hoy.Month, 1),
            Nota = null,
        };

        contexto.Movimientos.AddRange(gasto, ingreso);
        await contexto.SaveChangesAsync();
        return gasto.Id;
    }
}
