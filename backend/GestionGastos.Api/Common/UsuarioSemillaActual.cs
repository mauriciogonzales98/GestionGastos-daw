using GestionGastos.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Common;

/// <summary>
/// Implementación de este ticket: el usuario actual es la fila semilla, resuelta por email contra
/// la base y nunca por un valor que venga del cliente. El id se cachea por el ciclo de vida del
/// scope. Publicarlo en el <see cref="AppDbContext"/> NO es responsabilidad de esta clase: el
/// contrato de <see cref="IUsuarioActual"/> es devolver un id, y la implementación que traiga la
/// autenticación no va a conocer la capa de datos.
/// </summary>
public sealed class UsuarioSemillaActual(AppDbContext contexto) : IUsuarioActual
{
    /// <summary>TLD reservado: el email no puede corresponder a nadie real (mitigación R-10).</summary>
    public const string EmailSemilla = "dev@gestiongastos.local";

    private int? _id;

    public async ValueTask<int> ObtenerIdAsync(CancellationToken cancellationToken = default)
    {
        if (_id is { } cacheado)
        {
            return cacheado;
        }

        var id = await contexto.Usuarios
            .Where(u => u.Email == EmailSemilla)
            .Select(u => u.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (id == 0)
        {
            throw new UsuarioActualNoResueltoException(EmailSemilla);
        }

        _id = id;
        return id;
    }
}

/// <summary>
/// Error tipado: la semilla es parte de la migración inicial, así que su ausencia significa que la
/// base no está migrada. Se falla explícito en vez de seguir con un propietario inventado.
/// </summary>
public sealed class UsuarioActualNoResueltoException(string email)
    : InvalidOperationException($"No existe el usuario semilla '{email}'. Aplicá las migraciones antes de usar la API.")
{
    public string Email { get; } = email;
}
