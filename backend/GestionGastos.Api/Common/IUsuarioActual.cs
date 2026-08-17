namespace GestionGastos.Api.Common;

/// <summary>
/// Única fuente del usuario propietario de los movimientos (ADR-003). El ticket de autenticación
/// reemplaza la implementación por la que lee la sesión, sin tocar endpoints ni consultas.
/// </summary>
public interface IUsuarioActual
{
    /// <summary>
    /// Devuelve el id del usuario actual. Es asíncrono porque la implementación de este ticket lo
    /// resuelve contra la base: una propiedad síncrona forzaría sync-over-async en el camino
    /// caliente de los tres sub-tickets (ADR-003).
    /// </summary>
    ValueTask<int> ObtenerIdAsync(CancellationToken cancellationToken = default);
}
