using System.Globalization;
using GestionGastos.Api.Data;
using GestionGastos.Api.Data.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MySqlConnector;

namespace GestionGastos.Api.Tests.Infra;

/// <summary>
/// Crea y migra la base <c>gestiongastos_test</c> una vez por corrida y la deja limpia entre tests.
/// Los tests corren contra MySQL real por ADR-002: el tipo de columna y las restricciones del
/// esquema son justamente lo que hay que verificar, y un proveedor en memoria es ciego a eso.
/// </summary>
public sealed class BaseDeDatosFixture : IAsyncLifetime
{
    /// <summary>
    /// La cadena llega por variable de entorno, nunca por user-secrets: con <c>dotnet test</c> los
    /// user-secrets se resuelven contra el assembly de entrada, que es este proyecto y no la API
    /// (ADR-002, mitigación R-11).
    /// </summary>
    public const string VariableDeEntorno = "ConnectionStrings__Default";

    public static string CadenaDeConexion { get; } =
        ResolverCadena(Environment.GetEnvironmentVariable(VariableDeEntorno));

    /// <summary>
    /// Sin la variable no hay valor por defecto (ADR-002): apuntar a una base adivinada convierte
    /// la falta de configuración en un error de autenticación tardío contra una base ajena, que es
    /// un diagnóstico peor que no arrancar.
    /// </summary>
    public static string ResolverCadena(string? valorDeLaVariable) =>
        string.IsNullOrWhiteSpace(valorDeLaVariable)
            ? throw new InvalidOperationException(
                $"Falta la variable de entorno {VariableDeEntorno} con la cadena de conexión de la " +
                "base de tests. Exportala antes de correr dotnet test; nunca la escribas en un " +
                "appsettings ni en el repo.")
            : valorDeLaVariable;

    public string NombreDeLaBase => new MySqlConnectionStringBuilder(CadenaDeConexion).Database;

    public async Task InitializeAsync()
    {
        await using var contexto = CrearContexto();
        await contexto.Database.MigrateAsync();
        await LimpiarAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public AppDbContext CrearContexto()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(CadenaDeConexion, ServerVersion.Parse("8.4.0-mysql"))
            .Options;
        return new AppDbContext(opciones);
    }

    /// <summary>
    /// Deja la base en el estado que dejó la migración: sin movimientos, sin usuarios agregados y
    /// con la semilla (usuario y categorías) restaurada. Restaurar y no solo borrar es lo que evita
    /// que un test que falla —por ejemplo, uno que espera que un UNIQUE rechace una fila— envenene
    /// todas las corridas siguientes. SQL constante y parametrizado (mitigación R-05); se usa SQL
    /// directo en vez de <c>IgnoreQueryFilters</c> porque ADR-003 lo prohíbe.
    /// </summary>
    public async Task LimpiarAsync()
    {
        await using var conexion = new MySqlConnection(CadenaDeConexion);
        await conexion.OpenAsync();
        await using (var borrarMovimientos = new MySqlCommand("DELETE FROM movimientos", conexion))
        {
            await borrarMovimientos.ExecuteNonQueryAsync();
        }

        await using (var borrarUsuarios = new MySqlCommand(
            "DELETE FROM usuarios WHERE email <> @emailSemilla", conexion))
        {
            borrarUsuarios.Parameters.AddWithValue("@emailSemilla", EmailSemilla);
            await borrarUsuarios.ExecuteNonQueryAsync();
        }

        await RestaurarUsuarioSemillaAsync(conexion);
        await RestaurarCategoriasAsync(conexion);
    }

    private const string EmailSemilla = GestionGastos.Api.Common.UsuarioSemillaActual.EmailSemilla;

    private async Task RestaurarUsuarioSemillaAsync(MySqlConnection conexion)
    {
        var semilla = SemillaDe<Usuario>().Single();

        await using var comando = new MySqlCommand(
            """
            INSERT INTO usuarios (id, email, creado_en) VALUES (@id, @email, @creadoEn) AS nuevo
            ON DUPLICATE KEY UPDATE email = nuevo.email
            """, conexion);
        comando.Parameters.AddWithValue("@id", semilla["Id"]);
        comando.Parameters.AddWithValue("@email", semilla["Email"]);
        comando.Parameters.AddWithValue("@creadoEn", semilla["CreadoEn"]);
        await comando.ExecuteNonQueryAsync();
    }

    private async Task RestaurarCategoriasAsync(MySqlConnection conexion)
    {
        var semilla = SemillaDe<Categoria>().ToList();
        var indices = Enumerable.Range(0, semilla.Count).ToList();

        // El SQL se arma con la CANTIDAD de filas sembradas, nunca con sus valores: todo lo que
        // viene del modelo viaja como parámetro.
        await using (var borrarIntrusas = new MySqlCommand(
            $"DELETE FROM categorias WHERE id NOT IN ({string.Join(", ", indices.Select(i => $"@id{i}"))})",
            conexion))
        {
            foreach (var i in indices)
            {
                borrarIntrusas.Parameters.AddWithValue($"@id{i}", semilla[i]["Id"]);
            }

            await borrarIntrusas.ExecuteNonQueryAsync();
        }

        await using var restaurar = new MySqlCommand(
            $"""
             INSERT INTO categorias (id, nombre, tipo)
             VALUES {string.Join(", ", indices.Select(i => $"(@id{i}, @nombre{i}, @tipo{i})"))} AS nuevo
             ON DUPLICATE KEY UPDATE nombre = nuevo.nombre, tipo = nuevo.tipo
             """, conexion);
        foreach (var i in indices)
        {
            restaurar.Parameters.AddWithValue($"@id{i}", semilla[i]["Id"]);
            restaurar.Parameters.AddWithValue($"@nombre{i}", semilla[i]["Nombre"]);
            restaurar.Parameters.AddWithValue($"@tipo{i}", Convert.ToByte(semilla[i]["Tipo"], CultureInfo.InvariantCulture));
        }

        await restaurar.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// La semilla se lee del modelo de EF y no se duplica acá: si la migración cambia el catálogo,
    /// la limpieza lo sigue sin que nadie tenga que acordarse.
    /// </summary>
    private IEnumerable<IDictionary<string, object?>> SemillaDe<TEntidad>()
    {
        using var contexto = CrearContexto();
        // El modelo de runtime está optimizado para consultar y no conserva los datos sembrados.
        var modelo = contexto.GetService<IDesignTimeModel>().Model;
        return modelo.FindEntityType(typeof(TEntidad))!.GetSeedData().ToList();
    }

    /// <summary>Solo para el test del camino sin usuario semilla: lo borra para que no se resuelva.</summary>
    public async Task BorrarUsuarioSemillaAsync()
    {
        await using var conexion = new MySqlConnection(CadenaDeConexion);
        await conexion.OpenAsync();
        await using var comando = new MySqlCommand(
            "DELETE FROM usuarios WHERE email = @emailSemilla", conexion);
        comando.Parameters.AddWithValue("@emailSemilla", GestionGastos.Api.Common.UsuarioSemillaActual.EmailSemilla);
        await comando.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<string>> TablasAsync()
    {
        await using var conexion = new MySqlConnection(CadenaDeConexion);
        await conexion.OpenAsync();
        await using var comando = new MySqlCommand(
            "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = @esquema", conexion);
        comando.Parameters.AddWithValue("@esquema", NombreDeLaBase);

        var tablas = new List<string>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            tablas.Add(lector.GetString(0));
        }

        return tablas;
    }

    public async Task<string?> TipoDeColumnaAsync(string tabla, string columna)
    {
        await using var conexion = new MySqlConnection(CadenaDeConexion);
        await conexion.OpenAsync();
        await using var comando = new MySqlCommand(
            """
            SELECT COLUMN_TYPE
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = @esquema AND TABLE_NAME = @tabla AND COLUMN_NAME = @columna
            """, conexion);
        comando.Parameters.AddWithValue("@esquema", NombreDeLaBase);
        comando.Parameters.AddWithValue("@tabla", tabla);
        comando.Parameters.AddWithValue("@columna", columna);

        return await comando.ExecuteScalarAsync() as string;
    }
}

[CollectionDefinition(NombreDeLaColeccion)]
public sealed class BaseDeDatosCollection : ICollectionFixture<BaseDeDatosFixture>
{
    public const string NombreDeLaColeccion = "base-de-datos";
}
