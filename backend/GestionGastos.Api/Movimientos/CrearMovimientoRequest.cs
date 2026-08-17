using System.Text.Json;
using System.Text.Json.Serialization;

namespace GestionGastos.Api.Movimientos;

/// <summary>
/// Entrada del alta. **No tiene** <c>id</c>, <c>usuarioId</c>, <c>moneda</c>, <c>tipo</c> ni
/// <c>creadoEn</c> (mitigación R-04): un cuerpo que los incluya los ve ignorados, no aplicados. El
/// propietario sale de <c>IUsuarioActual</c>, la moneda de <c>Moneda.Predeterminada</c> y el tipo se
/// deriva de la categoría elegida.
/// </summary>
/// <param name="CategoriaId">Categoría del catálogo. De ella se deriva el tipo del movimiento.</param>
/// <param name="Monto">Mayor a cero, con hasta dos decimales.</param>
/// <param name="Fecha">Fecha del movimiento en formato <c>yyyy-MM-dd</c>, sin hora ni zona horaria.</param>
/// <param name="Nota">Opcional, hasta 120 caracteres. Vacía o en blanco se normaliza a <c>null</c>.</param>
/// <param name="TipoEsperado">
/// Opcional: el tipo que el cliente cree estar cargando. No se persiste — el tipo siempre sale de la
/// categoría—, solo se compara con el de la categoría para detectar el cruce de AC-10, que de otro
/// modo sería indetectable en el servidor.
/// </param>
public sealed record CrearMovimientoRequest(
    int? CategoriaId,
    [property: JsonConverter(typeof(MontoJsonConverter))] decimal? Monto,
    string? Fecha,
    string? Nota,
    string? TipoEsperado);

/// <summary>
/// Acepta únicamente números JSON que entren en un <c>decimal</c>, tal como declara el contrato, y
/// devuelve <c>null</c> ante cualquier otro token. Ese <c>null</c> es lo que hace que un
/// <c>"monto": "abc"</c> —o <c>"1,5"</c>, <c>{}</c>, <c>true</c>, <c>1e400</c>— termine en un 400 con
/// <c>errors.monto</c> en vez de reventar el deserializador y responder un 400 genérico sin campo.
/// Las cadenas se rechazan a propósito: parsearlas obligaría a elegir una cultura, y con separador de
/// miles <c>"1,5"</c> se guardaría como 15 (un monto equivocado y plausible) en lugar de fallar.
/// </summary>
public sealed class MontoJsonConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader lector, Type tipo, JsonSerializerOptions opciones)
    {
        switch (lector.TokenType)
        {
            case JsonTokenType.Number when lector.TryGetDecimal(out var numero):
                return numero;
            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                // Hay que consumir el valor entero: dejar el lector en el token de apertura
                // descolocaría la lectura del resto del cuerpo. Se usa ParseValue y no Skip porque
                // el cuerpo llega por segmentos desde la red y Skip revienta cuando el valor todavía
                // no entró completo en el buffer, convirtiendo el error de campo en un 400 genérico.
                using (JsonDocument.ParseValue(ref lector))
                {
                }

                return null;
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter escritor, decimal? valor, JsonSerializerOptions opciones)
    {
        if (valor is { } monto)
        {
            escritor.WriteNumberValue(monto);
        }
        else
        {
            escritor.WriteNullValue();
        }
    }
}
