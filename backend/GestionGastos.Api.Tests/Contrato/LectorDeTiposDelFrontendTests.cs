using System.Globalization;

namespace GestionGastos.Api.Tests.Contrato;

/// <summary>
/// El lector es la fuente de verdad de toda la verificación del contrato, así que sus sad paths
/// importan más que su happy path: un tipo que el parser no entiende y saltea queda pareciendo
/// verificado, que es exactamente el defecto que este ticket arregla (R-02 del threat model).
/// </summary>
public sealed class LectorDeTiposDelFrontendTests
{
    private const string Origen = "fragmento-de-prueba.ts";

    [Fact]
    public void Lector_SobreElArchivoReal_ExtraeLasNueveInterfaces()
    {
        var tipos = LectorDeTiposDelFrontend.LeerElContratoDelFrontend();

        Assert.Equal(9, tipos.Count);
        Assert.Equal(
            new[]
            {
                "CategoriaConTotal",
                "CategoriaDeMovimiento",
                "CategoriaDto",
                "CrearMovimientoRequest",
                "FiltrosDeMovimientos",
                "ListadoMovimientosResponse",
                "ModificarMovimientoRequest",
                "MovimientoDto",
                "ResumenMensual",
            },
            tipos.Select(t => t.Nombre).Order(StringComparer.Ordinal));

        // Ninguna interfaz sin campos: una lista vacía sería una forma silenciosa de "no entendí".
        Assert.All(tipos, tipo => Assert.NotEmpty(tipo.Campos));
    }

    [Fact]
    public void Lector_ExtraeNombreTipoYNulabilidadDeCadaCampo()
    {
        var movimiento = LectorDeTiposDelFrontend
            .LeerElContratoDelFrontend()
            .Single(t => t.Nombre == "MovimientoDto");

        Assert.Equal(7, movimiento.Campos.Count);
        Assert.Equal(
            new[] { "id", "tipo", "categoria", "monto", "moneda", "fecha", "nota" },
            movimiento.Campos.Select(c => c.Nombre));

        var id = movimiento.Campos.Single(c => c.Nombre == "id");
        Assert.Equal("number", id.Tipo);
        Assert.False(id.AdmiteNull);
        Assert.False(id.EsOpcional);

        // `nota` es el único que admite null, y el contrato dice null y no cadena vacía.
        var nota = movimiento.Campos.Single(c => c.Nombre == "nota");
        Assert.Equal("string", nota.Tipo);
        Assert.True(nota.AdmiteNull);
        Assert.Single(movimiento.Campos, c => c.AdmiteNull);

        // `categoria` referencia otra interfaz del archivo: el nombre se conserva tal cual para
        // que la comparación pueda recorrerla.
        var categoria = movimiento.Campos.Single(c => c.Nombre == "categoria");
        Assert.Equal("CategoriaDeMovimiento", categoria.Tipo);
        Assert.False(categoria.AdmiteNull);

        // El alias de unión de literales declarado en el mismo archivo no se degrada a `string`.
        Assert.Equal("TipoMovimiento", movimiento.Campos.Single(c => c.Nombre == "tipo").Tipo);

        // Y el sufijo [] se conserva donde aparece.
        var listado = LectorDeTiposDelFrontend
            .LeerElContratoDelFrontend()
            .Single(t => t.Nombre == "ListadoMovimientosResponse");
        var items = listado.Campos.Single(c => c.Nombre == "items");
        Assert.Equal("MovimientoDto[]", items.Tipo);
        Assert.True(items.EsArreglo);
        Assert.Equal("MovimientoDto", items.TipoBase);
    }

    [Fact]
    public void Lector_ConUnTipoNoReconocido_Lanza()
    {
        // El campo válido de arriba es deliberado: si el lector devolviera lo que sí entendió,
        // `Cosa` aparecería como una interfaz de un solo campo y pasaría por verificada.
        const string fragmento = """
            export interface Cosa {
              id: number;
              datos: Record<string, unknown>;
            }
            """;

        var error = Assert.Throws<InvalidOperationException>(
            () => LectorDeTiposDelFrontend.Parsear(fragmento, Origen));

        Assert.Contains("Cosa", error.Message, StringComparison.Ordinal);
        Assert.Contains(Linea(3), error.Message, StringComparison.Ordinal);
        Assert.Contains("Record<string, unknown>", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Lector_ConUnaInterfazSinCerrar_Lanza()
    {
        const string fragmento = """
            export interface Cosa {
              id: number;
            """;

        var error = Assert.Throws<InvalidOperationException>(
            () => LectorDeTiposDelFrontend.Parsear(fragmento, Origen));

        Assert.Contains("Cosa", error.Message, StringComparison.Ordinal);
        Assert.Contains(Linea(1), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Lector_ConUnCampoDeUnTipoQueNoExiste_Lanza()
    {
        // Un campo que referencia un tipo inexistente es una forma silenciosa de contrato roto: el
        // recorrido del comparador no tendría contra qué compararlo y podría darlo por bueno. Es la
        // misma familia que R-02 del threat model, en una variante que la spec no había previsto.
        const string fragmento = """
            export interface Cosa {
              id: number;
              hija: NoExiste;
            }
            """;

        var error = Assert.Throws<InvalidOperationException>(
            () => LectorDeTiposDelFrontend.Parsear(fragmento, Origen));

        Assert.Contains("Cosa", error.Message, StringComparison.Ordinal);
        Assert.Contains("hija", error.Message, StringComparison.Ordinal);
        Assert.Contains("NoExiste", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Lector_ConUnaConstruccionNoReconocidaFueraDeUnaInterfaz_Lanza()
    {
        // Fuera de una interfaz el parser tampoco saltea: un `import` o un `export const` que hoy
        // ignorara en silencio sería, mañana, una declaración de contrato que nadie verifica.
        const string fragmento = """
            import { algo } from './otro';

            export interface Cosa {
              id: number;
            }
            """;

        var error = Assert.Throws<InvalidOperationException>(
            () => LectorDeTiposDelFrontend.Parsear(fragmento, Origen));

        Assert.Contains(Linea(1), error.Message, StringComparison.Ordinal);
        Assert.Contains("import", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Lector_ConUnAliasQueNoEsUnionDeLiterales_Lanza()
    {
        // El comparador usa los literales del alias para verificar el VALOR, no sólo el tipo JSON.
        // Un alias que no sea unión de literales lo dejaría sin nada contra qué comparar.
        const string fragmento = """
            export type Identificador = number;

            export interface Cosa {
              id: number;
            }
            """;

        var error = Assert.Throws<InvalidOperationException>(
            () => LectorDeTiposDelFrontend.Parsear(fragmento, Origen));

        Assert.Contains("Identificador", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Lector_SinElArchivo_LanzaNombrandoLaRuta()
    {
        var ruta = Path.Combine(Path.GetTempPath(), "contrato-que-no-existe", "tipos.ts");

        var error = Assert.Throws<InvalidOperationException>(
            () => LectorDeTiposDelFrontend.LeerDe(ruta));

        // La ruta buscada, entera: un "archivo no encontrado" sin la ruta no dice dónde miró.
        Assert.Contains(ruta, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TiposExcluidos_CadaUno_TieneMotivoNoVacio()
    {
        // Sin esta primera afirmación el test sería vacío si alguien dejara la lista sin entradas,
        // y "ninguna exclusión sin motivo" pasaría sin haber mirado nada (NFR-05).
        Assert.NotEmpty(TiposExcluidos.PorNombre);
        Assert.Contains("FiltrosDeMovimientos", TiposExcluidos.PorNombre.Keys);

        var declarados = LectorDeTiposDelFrontend
            .LeerElContratoDelFrontend()
            .Select(t => t.Nombre)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (nombre, motivo) in TiposExcluidos.PorNombre)
        {
            Assert.False(string.IsNullOrWhiteSpace(motivo), $"«{nombre}» está excluido sin motivo.");

            // Una exclusión de un tipo que ya no existe deja de excluir algo y nadie se entera.
            Assert.Contains(nombre, declarados);
        }
    }

    private static string Linea(int numero) =>
        "línea " + numero.ToString(CultureInfo.InvariantCulture);
}
