using System.Net;
using System.Text.Json;
using GestionGastos.Api.Tests.Infra;

namespace GestionGastos.Api.Tests.Categorias;

/// <summary>
/// El catálogo es global y no depende del propietario, pero la API resuelve el usuario semilla en
/// cada request: pertenece a la colección para no correr en paralelo con quien toca la base.
/// </summary>
[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class CategoriasEndpointsTests(BaseDeDatosFixture baseDeDatos)
{
    [Fact]
    public async Task GetCategorias_DevuelveSieteDeGasto()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/categorias");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var catalogo = await LeerCatalogoAsync(respuesta);
        var gastos = catalogo.Where(c => c.Tipo == "gasto").ToList();

        Assert.Equal(7, gastos.Count);
        Assert.Equal(
            new[] { "Comida", "Ocio", "Otros", "Salud", "Servicios", "Transporte", "Vivienda" },
            gastos.Select(c => c.Nombre).Order(StringComparer.Ordinal).ToArray());
        // El orden del contrato es por tipo y nombre: si el endpoint devolviera el orden de
        // inserción, esta aserción cae.
        Assert.Equal(
            gastos.Select(c => c.Nombre).Order(StringComparer.Ordinal).ToArray(),
            gastos.Select(c => c.Nombre).ToArray());
        Assert.All(gastos, c => Assert.True(c.Id > 0));
    }

    [Fact]
    public async Task GetCategorias_DevuelveTresDeIngreso()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/categorias");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var catalogo = await LeerCatalogoAsync(respuesta);
        var ingresos = catalogo.Where(c => c.Tipo == "ingreso").ToList();

        Assert.Equal(3, ingresos.Count);
        Assert.Equal(
            new[] { "Ingreso extra", "Otros", "Sueldo" },
            ingresos.Select(c => c.Nombre).Order(StringComparer.Ordinal).ToArray());
        // 10 en total: nada fuera de los dos tipos, y ninguna categoría inventada.
        Assert.Equal(10, catalogo.Count);
        Assert.All(catalogo, c => Assert.Contains(c.Tipo, new[] { "gasto", "ingreso" }));
    }

    private static async Task<IReadOnlyList<CategoriaLeida>> LeerCatalogoAsync(HttpResponseMessage respuesta)
    {
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        using var documento = JsonDocument.Parse(cuerpo);
        return documento.RootElement.EnumerateArray()
            .Select(e => new CategoriaLeida(
                e.GetProperty("id").GetInt32(),
                e.GetProperty("nombre").GetString()!,
                e.GetProperty("tipo").GetString()!))
            .ToList();
    }

    private sealed record CategoriaLeida(int Id, string Nombre, string Tipo);
}
