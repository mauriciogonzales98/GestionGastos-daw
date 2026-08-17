using System.Globalization;
using MySqlConnector;

namespace GestionGastos.Api.Tests.Infra;

/// <summary>
/// Lecturas de la tabla <c>movimientos</c> por SQL crudo, es decir SIN el filtro global de
/// propietario: "no se creó ningún movimiento" es una afirmación sobre la tabla entera, no sobre lo
/// que el usuario actual alcanza a ver. Todas las sentencias son constantes, sin interpolar nada que
/// venga de afuera (mitigación R-05).
/// </summary>
public static class MovimientosEnLaBase
{
    public static async Task<int> CantidadAsync()
    {
        await using var conexion = new MySqlConnection(BaseDeDatosFixture.CadenaDeConexion);
        await conexion.OpenAsync();
        await using var comando = new MySqlCommand("SELECT COUNT(*) FROM movimientos", conexion);
        return Convert.ToInt32(await comando.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    /// <summary>El <c>usuario_id</c> de cada fila, para verificar a quién quedó atribuido el alta.</summary>
    public static async Task<IReadOnlyList<int>> PropietariosAsync()
    {
        await using var conexion = new MySqlConnection(BaseDeDatosFixture.CadenaDeConexion);
        await conexion.OpenAsync();
        await using var comando = new MySqlCommand("SELECT usuario_id FROM movimientos", conexion);

        var propietarios = new List<int>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            propietarios.Add(lector.GetInt32(0));
        }

        return propietarios;
    }
}
