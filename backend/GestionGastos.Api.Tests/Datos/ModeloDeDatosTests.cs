using System.Diagnostics.CodeAnalysis;
using GestionGastos.Api.Common;
using GestionGastos.Api.Data;
using GestionGastos.Api.Data.Entidades;
using GestionGastos.Api.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace GestionGastos.Api.Tests.Datos;

[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class ModeloDeDatosTests(BaseDeDatosFixture baseDeDatos)
{
    private const int CategoriaComidaId = 1;

    [Fact]
    public async Task Monto_SumaExactaSinRedondeo()
    {
        await baseDeDatos.LimpiarAsync();
        await using var contexto = baseDeDatos.CrearContexto();
        var usuarioId = await ResolverUsuarioActualAsync(contexto);

        contexto.Movimientos.Add(NuevoMovimiento(usuarioId, 0.10m));
        contexto.Movimientos.Add(NuevoMovimiento(usuarioId, 0.20m));
        await contexto.SaveChangesAsync();

        await using var otroContexto = baseDeDatos.CrearContexto();
        await ResolverUsuarioActualAsync(otroContexto);
        var suma = await otroContexto.Movimientos.SumAsync(m => m.Monto);

        Assert.Equal(0.30m, suma);
    }

    [Fact]
    public async Task Fecha_PersisteSinDesplazamiento()
    {
        await baseDeDatos.LimpiarAsync();
        await using var contexto = baseDeDatos.CrearContexto();
        var usuarioId = await ResolverUsuarioActualAsync(contexto);

        var movimiento = NuevoMovimiento(usuarioId, 100m);
        movimiento.Fecha = new DateOnly(2026, 3, 1);
        contexto.Movimientos.Add(movimiento);
        await contexto.SaveChangesAsync();

        await using var otroContexto = baseDeDatos.CrearContexto();
        await ResolverUsuarioActualAsync(otroContexto);
        var persistido = await otroContexto.Movimientos.AsNoTracking().SingleAsync();

        Assert.Equal(new DateOnly(2026, 3, 1), persistido.Fecha);
    }

    [Fact]
    public async Task Movimiento_PersisteElPropietario()
    {
        await baseDeDatos.LimpiarAsync();
        await using var contexto = baseDeDatos.CrearContexto();
        var usuarioId = await ResolverUsuarioActualAsync(contexto);

        contexto.Movimientos.Add(NuevoMovimiento(usuarioId, 1234.56m));
        await contexto.SaveChangesAsync();

        await using var otroContexto = baseDeDatos.CrearContexto();
        await ResolverUsuarioActualAsync(otroContexto);
        var persistido = await otroContexto.Movimientos.AsNoTracking().SingleAsync();

        var esperado = await otroContexto.Usuarios.AsNoTracking()
            .Where(u => u.Email == UsuarioSemillaActual.EmailSemilla)
            .Select(u => u.Id)
            .SingleAsync();
        Assert.Equal(esperado, persistido.UsuarioId);
        Assert.Equal(Moneda.Predeterminada, persistido.Moneda);
    }

    [Fact]
    public async Task FiltroGlobal_ExcluyeMovimientosDeOtroPropietario()
    {
        await baseDeDatos.LimpiarAsync();

        await using (var preparacion = baseDeDatos.CrearContexto())
        {
            var otroUsuario = new Usuario
            {
                Email = "otro@gestiongastos.local",
                CreadoEn = DateTime.UtcNow,
            };
            preparacion.Usuarios.Add(otroUsuario);
            await preparacion.SaveChangesAsync();

            var propio = NuevoMovimiento(await ResolverUsuarioActualAsync(preparacion), 111m);
            var ajeno = NuevoMovimiento(otroUsuario.Id, 999m);
            preparacion.AddRange(propio, ajeno);
            await preparacion.SaveChangesAsync();
        }

        await using var contexto = baseDeDatos.CrearContexto();
        var usuarioId = await ResolverUsuarioActualAsync(contexto);
        var visibles = await contexto.Movimientos.AsNoTracking().ToListAsync();

        Assert.Single(visibles);
        Assert.All(visibles, m => Assert.Equal(usuarioId, m.UsuarioId));
        Assert.DoesNotContain(visibles, m => m.Monto == 999m);
    }

    [Fact]
    public async Task Categoria_RechazaNombreYTipoDuplicados()
    {
        try
        {
            await using var contexto = baseDeDatos.CrearContexto();
            contexto.Categorias.Add(new Categoria { Nombre = "Comida", Tipo = TipoMovimiento.Gasto });

            var excepcion = await Assert.ThrowsAsync<DbUpdateException>(() => contexto.SaveChangesAsync());

            // 1062 y no "cualquier DbUpdateException": lo que este test vigila es el UNIQUE del par
            // (nombre, tipo), no que la inserción falle por el motivo que sea.
            var deMySql = Assert.IsType<MySqlException>(excepcion.InnerException);
            Assert.Equal(MySqlErrorCode.DuplicateKeyEntry, deMySql.ErrorCode);
        }
        finally
        {
            // Si el UNIQUE se perdiera, la fila duplicada quedaría persistida y envenenaría las
            // corridas siguientes: la limpieza restaura las categorías sembradas.
            await baseDeDatos.LimpiarAsync();
        }
    }

    [Fact]
    public async Task Movimientos_SinUsuarioPublicado_LanzaErrorTipado()
    {
        await using var contexto = baseDeDatos.CrearContexto();

        Assert.Null(contexto.UsuarioActualId);

        // Sin propietario publicado la consulta no puede devolver una lista vacía: eso es
        // indistinguible de "no hay movimientos". Tiene que fallar ruidoso y tipado.
        await Assert.ThrowsAsync<UsuarioActualNoPublicadoException>(
            () => contexto.Movimientos.ToListAsync());
        await Assert.ThrowsAsync<UsuarioActualNoPublicadoException>(
            () => contexto.Movimientos.CountAsync());
    }

    [SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification =
            "La variable se declara como IUsuarioActual a propósito: lo que este helper ejercita es el " +
            "contrato de la abstracción, no la clase que hoy lo implementa. Usar el tipo concreto haría " +
            "que el test pruebe otra cosa (AC-12 de FIX-001).")]
    private static async Task<int> ResolverUsuarioActualAsync(AppDbContext contexto)
    {
        IUsuarioActual usuarioActual = new UsuarioSemillaActual(contexto);
        var id = await usuarioActual.ObtenerIdAsync();
        // La publicación es del llamador (en producción, del middleware): IUsuarioActual solo promete
        // devolver un id.
        contexto.UsuarioActualId = id;
        return id;
    }

    private static Movimiento NuevoMovimiento(int usuarioId, decimal monto) => new()
    {
        UsuarioId = usuarioId,
        CategoriaId = CategoriaComidaId,
        Tipo = TipoMovimiento.Gasto,
        Monto = monto,
        Moneda = Moneda.Predeterminada,
        Fecha = new DateOnly(2026, 3, 1),
        Nota = null,
        CreadoEn = DateTime.UtcNow,
    };
}
