using GestionGastos.Api.Common;
using GestionGastos.Api.Data.Entidades;
using GestionGastos.Api.Tests.Infra;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Datos;

[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class MigracionTests(BaseDeDatosFixture baseDeDatos)
{
    [Fact]
    public async Task Migracion_CreaLasTresTablas()
    {
        var tablas = await baseDeDatos.TablasAsync();

        Assert.Contains("usuarios", tablas);
        Assert.Contains("categorias", tablas);
        Assert.Contains("movimientos", tablas);
    }

    [Fact]
    public async Task Migracion_SiembraDiezCategorias()
    {
        await using var contexto = baseDeDatos.CrearContexto();

        var categorias = await contexto.Categorias.AsNoTracking().ToListAsync();

        Assert.Equal(10, categorias.Count);
        Assert.Equal(7, categorias.Count(c => c.Tipo == TipoMovimiento.Gasto));
        Assert.Equal(3, categorias.Count(c => c.Tipo == TipoMovimiento.Ingreso));
        Assert.Equal(
            ["Comida", "Ocio", "Otros", "Salud", "Servicios", "Transporte", "Vivienda"],
            categorias.Where(c => c.Tipo == TipoMovimiento.Gasto).Select(c => c.Nombre).Order());
        Assert.Equal(
            ["Ingreso extra", "Otros", "Sueldo"],
            categorias.Where(c => c.Tipo == TipoMovimiento.Ingreso).Select(c => c.Nombre).Order());
    }

    [Fact]
    public async Task Migracion_SiembraUsuarioDeDesarrollo()
    {
        await using var contexto = baseDeDatos.CrearContexto();

        var usuario = await contexto.Usuarios
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == UsuarioSemillaActual.EmailSemilla);

        Assert.NotNull(usuario);
        Assert.Equal("dev@gestiongastos.local", usuario.Email);
    }

    [Fact]
    public async Task Monto_UsaDecimalDeQuinceComaDos()
    {
        var tipoDelMonto = await baseDeDatos.TipoDeColumnaAsync("movimientos", "monto");
        var tipoDeLaFecha = await baseDeDatos.TipoDeColumnaAsync("movimientos", "fecha");

        Assert.Equal("decimal(15,2)", tipoDelMonto);
        Assert.Equal("date", tipoDeLaFecha);
    }
}
