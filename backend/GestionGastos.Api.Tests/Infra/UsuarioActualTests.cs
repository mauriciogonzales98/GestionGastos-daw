using System.Net;
using GestionGastos.Api.Common;
using GestionGastos.Api.Data;
using GestionGastos.Api.Data.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestionGastos.Api.Tests.Infra;

/// <summary>
/// El contrato de <see cref="IUsuarioActual"/> es "devolver un id" y nada más: publicar ese id en el
/// <see cref="AppDbContext"/> es responsabilidad del pipeline. Estos tests lo fijan con una
/// implementación que no conoce la capa de datos, que es la forma que tendrá la del ticket de
/// autenticación.
/// </summary>
[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class UsuarioActualTests(BaseDeDatosFixture baseDeDatos)
{
    [Fact]
    public async Task Middleware_PublicaElIdDeUnaImplementacionQueNoConoceElDbContext()
    {
        await baseDeDatos.LimpiarAsync();

        int usuarioId;
        await using (var preparacion = baseDeDatos.CrearContexto())
        {
            usuarioId = await preparacion.Usuarios
                .Where(u => u.Email == UsuarioSemillaActual.EmailSemilla)
                .Select(u => u.Id)
                .SingleAsync();

            preparacion.UsuarioActualId = usuarioId;
            preparacion.Movimientos.Add(new Movimiento
            {
                UsuarioId = usuarioId,
                CategoriaId = 1,
                Tipo = TipoMovimiento.Gasto,
                Monto = 150m,
                Moneda = Moneda.Predeterminada,
                Fecha = new DateOnly(2026, 3, 1),
                CreadoEn = DateTime.UtcNow,
            });
            await preparacion.SaveChangesAsync();
        }

        await using var fabrica = new ApiFactory();
        using var conUsuarioAjenoALaCapaDeDatos = fabrica.WithWebHostBuilder(builder =>
            builder.ConfigureServices(servicios =>
                servicios.AddScoped<IUsuarioActual>(_ => new UsuarioActualDePrueba(usuarioId))));
        using var cliente = conUsuarioAjenoALaCapaDeDatos.CreateClient();

        using var respuesta = await cliente.GetAsync(EndpointsDePrueba.RutaMovimientosVisibles);
        var visibles = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        // Si el filtro global dependiera del efecto colateral de UsuarioSemillaActual, acá vendría 0.
        Assert.Equal("1", visibles);
    }

    [Fact]
    public async Task UsuarioSemilla_SinLaFilaSembrada_LanzaUsuarioActualNoResuelto()
    {
        await baseDeDatos.LimpiarAsync();
        await baseDeDatos.BorrarUsuarioSemillaAsync();
        try
        {
            await using var contexto = baseDeDatos.CrearContexto();
            IUsuarioActual usuarioActual = new UsuarioSemillaActual(contexto);

            var excepcion = await Assert.ThrowsAsync<UsuarioActualNoResueltoException>(
                async () => await usuarioActual.ObtenerIdAsync());

            Assert.Equal(UsuarioSemillaActual.EmailSemilla, excepcion.Email);
            Assert.Contains("migraciones", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await baseDeDatos.LimpiarAsync();
        }
    }
}

/// <summary>
/// Doble de la implementación que traerá el ticket de autenticación: devuelve un id y no sabe que
/// existe un <c>AppDbContext</c>.
/// </summary>
internal sealed class UsuarioActualDePrueba(int id) : IUsuarioActual
{
    public ValueTask<int> ObtenerIdAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(id);
}
