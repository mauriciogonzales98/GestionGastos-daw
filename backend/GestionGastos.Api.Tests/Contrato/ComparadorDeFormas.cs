using System.Text.Json;

namespace GestionGastos.Api.Tests.Contrato;

/// <summary>Una diferencia entre lo que el frontend declara y lo que el backend emite.</summary>
/// <param name="Ruta">Dónde aparece, con el tipo y el camino de campos: <c>ResumenMensual.desglose[].total</c>.</param>
/// <param name="Detalle">En qué consiste la diferencia.</param>
public sealed record DiferenciaDeContrato(string Ruta, string Detalle)
{
    public override string ToString() => $"{Ruta}: {Detalle}";
}

/// <summary>
/// Compara la forma que el frontend declara contra el JSON que el backend emite de verdad.
/// </summary>
/// <remarks>
/// <para>
/// Se compara contra el JSON real y no contra el <c>record</c> de C# a propósito: nadie configura la
/// serialización en este proyecto —no hay <c>JsonNamingPolicy</c> ni <c>ConfigureHttpJsonOptions</c>
/// en ningún lado—, así que el camelCase que el frontend asume es el comportamiento por defecto de
/// ASP.NET Core. Un esquema derivado del <c>record</c> no verificaría esa parte.
/// </para>
/// <para>
/// <b>Compara en las dos direcciones.</b> Un campo que el JSON trae y el frontend no declara es una
/// diferencia, y uno declarado que el JSON no trae también: el primero es una funcionalidad que el
/// frontend no ve, el segundo es un <c>undefined</c> en pantalla.
/// </para>
/// </remarks>
public static class ComparadorDeFormas
{
    /// <summary>
    /// Compara <paramref name="json"/> contra el tipo <paramref name="nombreDelTipo"/> del contrato.
    /// </summary>
    /// <param name="contrato">Lo que el frontend declara.</param>
    /// <param name="nombreDelTipo">El tipo contra el cual comparar.</param>
    /// <param name="json">El cuerpo emitido por el backend.</param>
    /// <param name="endpoint">Ruta del endpoint, para que el mensaje diga dónde mirar (AC-06).</param>
    /// <returns>Las diferencias encontradas; vacío si la forma coincide.</returns>
    public static IReadOnlyList<DiferenciaDeContrato> Comparar(
        ContratoDelFrontend contrato,
        string nombreDelTipo,
        JsonElement json,
        string endpoint)
    {
        ArgumentNullException.ThrowIfNull(contrato);

        var diferencias = new List<DiferenciaDeContrato>();
        Recorrer(contrato, nombreDelTipo, json, $"{endpoint} → {nombreDelTipo}", [], diferencias);
        return diferencias;
    }

    private static void Recorrer(
        ContratoDelFrontend contrato,
        string nombreDelTipo,
        JsonElement json,
        string ruta,
        IReadOnlyCollection<string> enCurso,
        List<DiferenciaDeContrato> diferencias)
    {
        // Mitigación R-04 del threat model: hoy el grafo de tipos.ts es acíclico, pero nada lo
        // garantiza mañana, y un test colgado no da señal — se ve igual que uno que todavía corre.
        if (enCurso.Contains(nombreDelTipo, StringComparer.Ordinal))
        {
            var ciclo = string.Join(" → ", enCurso.Append(nombreDelTipo));
            throw new InvalidOperationException(
                $"Los tipos del contrato se referencian en ciclo: {ciclo}. El recorrido corta acá " +
                "en vez de colgarse.");
        }

        var tipo = contrato.Tipos.SingleOrDefault(t =>
            string.Equals(t.Nombre, nombreDelTipo, StringComparison.Ordinal));

        if (tipo is null)
        {
            throw new InvalidOperationException(
                $"El contrato del frontend no declara ningún tipo llamado «{nombreDelTipo}».");
        }

        if (json.ValueKind != JsonValueKind.Object)
        {
            diferencias.Add(new DiferenciaDeContrato(
                ruta,
                $"el frontend declara un objeto y el backend emitió {json.ValueKind}"));
            return;
        }

        var visitadosAhora = enCurso.Append(nombreDelTipo).ToList();
        var declarados = tipo.Campos.Select(c => c.Nombre).ToHashSet(StringComparer.Ordinal);

        foreach (var propiedad in json.EnumerateObject())
        {
            if (!declarados.Contains(propiedad.Name))
            {
                diferencias.Add(new DiferenciaDeContrato(
                    $"{ruta}.{propiedad.Name}",
                    "el backend emite este campo y el frontend no lo declara: la aplicación no lo ve"));
            }
        }

        foreach (var campo in tipo.Campos)
        {
            if (!json.TryGetProperty(campo.Nombre, out var valor))
            {
                if (!campo.EsOpcional)
                {
                    diferencias.Add(new DiferenciaDeContrato(
                        $"{ruta}.{campo.Nombre}",
                        "el frontend declara este campo y el backend no lo emite: llega undefined"));
                }

                continue;
            }

            VerificarCampo(contrato, campo, valor, $"{ruta}.{campo.Nombre}", visitadosAhora, diferencias);
        }
    }

    private static void VerificarCampo(
        ContratoDelFrontend contrato,
        CampoDelFrontend campo,
        JsonElement valor,
        string ruta,
        IReadOnlyCollection<string> enCurso,
        List<DiferenciaDeContrato> diferencias)
    {
        if (valor.ValueKind == JsonValueKind.Null)
        {
            if (!campo.AdmiteNull)
            {
                diferencias.Add(new DiferenciaDeContrato(
                    ruta,
                    $"el backend emitió null y el frontend declara «{campo.Tipo}» sin «| null»"));
            }

            return;
        }

        if (campo.EsArreglo)
        {
            if (valor.ValueKind != JsonValueKind.Array)
            {
                diferencias.Add(new DiferenciaDeContrato(
                    ruta,
                    $"el frontend declara «{campo.Tipo}» y el backend emitió {valor.ValueKind}"));
                return;
            }

            var indice = 0;
            foreach (var elemento in valor.EnumerateArray())
            {
                VerificarValor(contrato, campo.TipoBase, elemento, $"{ruta}[{indice}]", enCurso, diferencias);
                indice++;
            }

            return;
        }

        VerificarValor(contrato, campo.TipoBase, valor, ruta, enCurso, diferencias);
    }

    private static void VerificarValor(
        ContratoDelFrontend contrato,
        string tipoDeclarado,
        JsonElement valor,
        string ruta,
        IReadOnlyCollection<string> enCurso,
        List<DiferenciaDeContrato> diferencias)
    {
        switch (tipoDeclarado)
        {
            // `number` acepta cualquier número del cable. El backend usa `decimal` para los montos
            // (NFR-03 de FEAT-001a) y TypeScript sólo tiene punto flotante: el criterio de
            // compatibilidad es la forma en el cable, no el tipo de origen.
            case "number":
                Exigir(valor, JsonValueKind.Number, tipoDeclarado, ruta, diferencias);
                return;

            case "string":
                Exigir(valor, JsonValueKind.String, tipoDeclarado, ruta, diferencias);
                return;

            case "boolean":
                if (valor.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    diferencias.Add(new DiferenciaDeContrato(
                        ruta,
                        $"el frontend declara «boolean» y el backend emitió {valor.ValueKind}"));
                }

                return;

            default:
                break;
        }

        if (contrato.AliasesDeLiterales.TryGetValue(tipoDeclarado, out var literales))
        {
            if (!Exigir(valor, JsonValueKind.String, tipoDeclarado, ruta, diferencias))
            {
                return;
            }

            var emitido = valor.GetString();
            if (!literales.Contains(emitido, StringComparer.Ordinal))
            {
                // Un valor fuera de la unión rompe el contrato aunque el tipo JSON coincida: el
                // frontend hace comparaciones exactas contra estos literales.
                diferencias.Add(new DiferenciaDeContrato(
                    ruta,
                    $"el backend emitió «{emitido}» y el frontend declara «{tipoDeclarado}», que " +
                    $"admite sólo: {string.Join(", ", literales)}"));
            }

            return;
        }

        Recorrer(contrato, tipoDeclarado, valor, ruta, enCurso, diferencias);
    }

    private static bool Exigir(
        JsonElement valor,
        JsonValueKind esperado,
        string tipoDeclarado,
        string ruta,
        List<DiferenciaDeContrato> diferencias)
    {
        if (valor.ValueKind == esperado)
        {
            return true;
        }

        diferencias.Add(new DiferenciaDeContrato(
            ruta,
            $"el frontend declara «{tipoDeclarado}» y el backend emitió {valor.ValueKind}"));
        return false;
    }
}
