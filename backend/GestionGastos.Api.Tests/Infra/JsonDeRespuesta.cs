using System.Text.Json;

namespace GestionGastos.Api.Tests.Infra;

/// <summary>
/// Lectura del cuerpo JSON de una respuesta HTTP como <see cref="JsonElement"/> desacoplado del
/// <see cref="JsonDocument"/> que lo produjo.
/// </summary>
public static class JsonDeRespuesta
{
    public static async Task<JsonElement> LeerAsync(HttpResponseMessage respuesta) =>
        Raiz(await respuesta.Content.ReadAsStringAsync());

    /// <summary>
    /// <c>Clone</c>: el <see cref="JsonDocument"/> se descarta al salir y el elemento quedaría
    /// apuntando a memoria devuelta al pool.
    /// </summary>
    public static JsonElement Raiz(string json)
    {
        using var documento = JsonDocument.Parse(json);
        return documento.RootElement.Clone();
    }
}
