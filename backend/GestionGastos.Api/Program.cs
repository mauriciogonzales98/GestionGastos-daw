using GestionGastos.Api.Categorias;
using GestionGastos.Api.Common;
using GestionGastos.Api.Data;
using GestionGastos.Api.Movimientos;
using GestionGastos.Api.Resumen;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var cadenaDeConexion = ConfiguracionDeConexion.ObtenerCadena(builder.Configuration);

builder.Services.AddDbContext<AppDbContext>(opciones =>
    opciones.UseMySql(cadenaDeConexion, ServerVersion.Parse("8.4.0-mysql")));

builder.Services.AddScoped<IUsuarioActual, UsuarioSemillaActual>();

// Convierte cualquier excepción no controlada en ProblemDetails (RFC 9457) con traceId, en vez de
// un cuerpo HTML o vacío.
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Resuelve el propietario una vez por request y lo publica explícitamente en el DbContext, que es
// de donde lo toma el filtro global (ADR-003). La publicación vive acá y no adentro de
// IUsuarioActual: esa interfaz solo promete devolver un id, y la implementación del ticket de
// autenticación no va a conocer el AppDbContext.
app.Use(async (contexto, siguiente) =>
{
    var usuarioActual = contexto.RequestServices.GetRequiredService<IUsuarioActual>();
    var datos = contexto.RequestServices.GetRequiredService<AppDbContext>();
    datos.UsuarioActualId = await usuarioActual.ObtenerIdAsync(contexto.RequestAborted);
    await siguiente();
});

app.MapCategoriasEndpoints();
app.MapMovimientosEndpoints();
app.MapResumenEndpoints();

app.Run();

/// <summary>
/// Lee la cadena de conexión, que vive en user-secrets o en la variable de entorno y nunca en
/// <c>appsettings</c> (mitigación R-11). Sin ella la aplicación no arranca: un valor por defecto
/// silencioso terminaría en un error de conexión tardío que invita a escribirla donde no va.
/// </summary>
public static class ConfiguracionDeConexion
{
    public const string Clave = "Default";

    public static string ObtenerCadena(IConfiguration configuracion)
    {
        var cadena = configuracion.GetConnectionString(Clave);
        if (string.IsNullOrWhiteSpace(cadena))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexión 'ConnectionStrings:{Clave}'. Cargala con user-secrets " +
                $"(dotnet user-secrets set \"ConnectionStrings:{Clave}\" \"...\") o con la variable de " +
                "entorno ConnectionStrings__Default. Nunca en appsettings.json.");
        }

        return cadena;
    }
}

public partial class Program;
