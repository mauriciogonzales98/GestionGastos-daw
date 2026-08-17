using GestionGastos.Api.Data.Entidades;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> opciones) : DbContext(opciones)
{
    /// <summary>
    /// Propietario que aplica el filtro global. Lo publica el pipeline una vez por request, con el
    /// id que devuelve <c>IUsuarioActual</c>.
    /// </summary>
    public int? UsuarioActualId { get; set; }

    /// <summary>
    /// El propietario que usa el filtro global. Sin publicar no devuelve cero filas —eso sería
    /// indistinguible de "no hay movimientos"— sino que falla con un error tipado.
    /// </summary>
    private int PropietarioRequerido => UsuarioActualId ?? throw new UsuarioActualNoPublicadoException();

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Categoria> Categorias => Set<Categoria>();

    public DbSet<Movimiento> Movimientos => Set<Movimiento>();

    protected override void OnModelCreating(ModelBuilder constructor)
    {
        constructor.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // La pertenencia al usuario no queda a criterio de cada consulta (mitigación R-03): acá
        // olvidarla es imposible, no improbable.
        constructor.Entity<Movimiento>().HasQueryFilter(m => m.UsuarioId == PropietarioRequerido);
    }
}

/// <summary>
/// Error tipado: consultar movimientos sin propietario publicado no puede devolver una lista vacía,
/// porque es indistinguible de "este usuario no tiene movimientos". Falla ruidoso.
/// </summary>
public sealed class UsuarioActualNoPublicadoException()
    : InvalidOperationException(
        "No hay usuario actual publicado en el AppDbContext: el pipeline tiene que asignar " +
        "UsuarioActualId con el id que devuelve IUsuarioActual antes de consultar movimientos.");
