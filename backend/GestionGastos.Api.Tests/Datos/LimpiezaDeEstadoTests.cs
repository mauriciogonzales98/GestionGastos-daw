using GestionGastos.Api.Common;
using GestionGastos.Api.Data.Entidades;
using GestionGastos.Api.Tests.Infra;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Datos;

/// <summary>
/// La base es compartida entre tests (ADR-002): si la limpieza no restaura el estado sembrado, un
/// test que falla contamina todas las corridas siguientes hasta que alguien borre la base a mano.
/// </summary>
[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class LimpiezaDeEstadoTests(BaseDeDatosFixture baseDeDatos)
{
    private const int CategoriaOcioId = 6;

    [Fact]
    public async Task LimpiarAsync_RestauraLasCategoriasSembradas()
    {
        await baseDeDatos.LimpiarAsync();

        await using (var sucio = baseDeDatos.CrearContexto())
        {
            sucio.Categorias.Add(new Categoria { Nombre = "Intrusa", Tipo = TipoMovimiento.Gasto });
            sucio.Categorias.Remove(await sucio.Categorias.SingleAsync(c => c.Id == CategoriaOcioId));
            await sucio.SaveChangesAsync();
        }

        await baseDeDatos.LimpiarAsync();

        await using var contexto = baseDeDatos.CrearContexto();
        var categorias = await contexto.Categorias.AsNoTracking().ToListAsync();

        Assert.Equal(10, categorias.Count);
        Assert.DoesNotContain(categorias, c => c.Nombre == "Intrusa");
        Assert.Contains(categorias, c => c.Id == CategoriaOcioId && c.Nombre == "Ocio");
    }

    [Fact]
    public async Task LimpiarAsync_RestauraElUsuarioSemilla()
    {
        await baseDeDatos.LimpiarAsync();
        await baseDeDatos.BorrarUsuarioSemillaAsync();

        await baseDeDatos.LimpiarAsync();

        await using var contexto = baseDeDatos.CrearContexto();
        var semilla = await contexto.Usuarios.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == UsuarioSemillaActual.EmailSemilla);

        Assert.NotNull(semilla);
    }
}
