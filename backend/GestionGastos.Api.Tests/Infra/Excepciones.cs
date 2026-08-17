namespace GestionGastos.Api.Tests.Infra;

/// <summary>
/// Utilidades para afirmar sobre la causa real de una excepción y no sobre la envoltura que le pone
/// quien la propaga.
/// </summary>
public static class Excepciones
{
    /// <summary>
    /// La cadena de <c>InnerException</c> completa, empezando por la excepción recibida. EF envuelve
    /// los errores del proveedor, así que la causa que interesa casi nunca es la de arriba.
    /// </summary>
    public static IEnumerable<Exception> Desenrollar(Exception excepcion)
    {
        for (var actual = excepcion; actual is not null; actual = actual.InnerException)
        {
            yield return actual;
        }
    }
}
