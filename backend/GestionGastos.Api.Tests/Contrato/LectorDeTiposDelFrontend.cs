using System.Globalization;
using System.Text.RegularExpressions;

namespace GestionGastos.Api.Tests.Contrato;

/// <summary>
/// Lee <c>frontend/src/api/tipos.ts</c> y extrae las interfaces del contrato con sus campos.
/// </summary>
/// <remarks>
/// <para>
/// <b>Estricto por diseño, y es lo más importante de esta clase.</b> Cualquier construcción que no
/// reconozca lanza nombrando la interfaz, la línea y el texto que no pudo interpretar. Nunca saltea
/// y nunca devuelve una lista parcial. Saltear en silencio convertiría un tipo no entendido en un
/// tipo aparentemente verificado, que es exactamente el defecto que este ticket arregla, un nivel
/// más arriba (R-02 del threat model).
/// </para>
/// <para>
/// El parseo va línea por línea y ningún patrón lleva cuantificadores anidados (R-01): el archivo
/// de entrada sólo va a crecer, y una expresión regular escrita sin cuidado sobre un archivo entero
/// es un problema real — la lección quedó registrada en FIX-003.
/// </para>
/// </remarks>
public static class LectorDeTiposDelFrontend
{
    /// <summary>Ruta del contrato, relativa a la raíz del repositorio.</summary>
    public const string RutaRelativa = "frontend/src/api/tipos.ts";

    private static readonly Regex DeclaracionDeInterfaz = new(
        @"^export interface ([A-Za-z0-9]+) \{$",
        RegexOptions.CultureInvariant);

    private static readonly Regex DeclaracionDeAlias = new(
        @"^export type ([A-Za-z0-9]+) = (.+);$",
        RegexOptions.CultureInvariant);

    private static readonly Regex DeclaracionDeCampo = new(
        @"^([a-zA-Z0-9]+)(\?)?: (.+);$",
        RegexOptions.CultureInvariant);

    private static readonly Regex LiteralDeCadena = new(
        @"^'([a-z]+)'$",
        RegexOptions.CultureInvariant);

    private static readonly string[] TiposPrimitivos = ["number", "string", "boolean"];

    /// <summary>Lee el contrato del repositorio y devuelve sus interfaces.</summary>
    public static IReadOnlyList<TipoDelFrontend> LeerElContratoDelFrontend() =>
        LeerElContratoCompleto().Tipos;

    /// <summary>
    /// Lee el contrato del repositorio con sus alias de unión, que la comparación necesita para no
    /// degradar <c>TipoMovimiento</c> a "una cadena cualquiera".
    /// </summary>
    public static ContratoDelFrontend LeerElContratoCompleto() =>
        LeerDe(Path.Combine(RaizDelRepositorio(), RutaRelativa.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>Lee el contrato desde una ruta concreta.</summary>
    /// <exception cref="InvalidOperationException">
    /// Si el archivo no existe. Lleva la ruta buscada entera: un "no encontrado" sin la ruta no dice
    /// dónde miró.
    /// </exception>
    public static ContratoDelFrontend LeerDe(string ruta)
    {
        if (!File.Exists(ruta))
        {
            throw new InvalidOperationException(
                $"No se encontró el contrato del frontend en «{ruta}». Sin ese archivo no hay nada " +
                "contra qué comparar, y dar el contrato por verificado sería peor que no verificarlo.");
        }

        return Parsear(File.ReadAllText(ruta), ruta);
    }

    /// <summary>Parsea el contenido de un archivo de tipos.</summary>
    /// <param name="contenido">El texto completo del archivo.</param>
    /// <param name="origen">Ruta o nombre que aparece en los mensajes de error.</param>
    /// <exception cref="InvalidOperationException">
    /// Ante cualquier construcción no reconocida, una interfaz sin cerrar o una interfaz sin campos.
    /// </exception>
    public static ContratoDelFrontend Parsear(string contenido, string origen)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        var lineas = contenido.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var aliases = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var tipos = new List<TipoDelFrontend>();

        string? interfazAbierta = null;
        var lineaDeApertura = 0;
        var campos = new List<CampoDelFrontend>();

        for (var indice = 0; indice < lineas.Length; indice++)
        {
            var numeroDeLinea = indice + 1;
            var linea = lineas[indice].Trim();

            if (EsIgnorable(linea))
            {
                continue;
            }

            if (interfazAbierta is null)
            {
                var alias = DeclaracionDeAlias.Match(linea);
                if (alias.Success)
                {
                    aliases[alias.Groups[1].Value] =
                        LiteralesDe(alias.Groups[2].Value, alias.Groups[1].Value, numeroDeLinea, origen);
                    continue;
                }

                var interfaz = DeclaracionDeInterfaz.Match(linea);
                if (interfaz.Success)
                {
                    interfazAbierta = interfaz.Groups[1].Value;
                    lineaDeApertura = numeroDeLinea;
                    campos = [];
                    continue;
                }

                throw NoReconocido(linea, null, numeroDeLinea, origen);
            }

            if (linea == "}")
            {
                if (campos.Count == 0)
                {
                    throw new InvalidOperationException(
                        Encabezado(interfazAbierta, lineaDeApertura, origen) +
                        "la interfaz no declara ningún campo. Una interfaz vacía es un error de " +
                        "parseo, no un tipo sin campos.");
                }

                tipos.Add(new TipoDelFrontend(interfazAbierta, campos));
                interfazAbierta = null;
                continue;
            }

            campos.Add(CampoDe(linea, interfazAbierta, numeroDeLinea, origen));
        }

        if (interfazAbierta is not null)
        {
            throw new InvalidOperationException(
                Encabezado(interfazAbierta, lineaDeApertura, origen) +
                "la interfaz nunca se cierra. El archivo termina sin su «}».");
        }

        VerificarQueLosTiposReferenciadosExisten(tipos, aliases, origen);
        return new ContratoDelFrontend(tipos, aliases);
    }

    /// <summary>Comentarios, directivas y líneas en blanco: lo único que se saltea a propósito.</summary>
    private static bool EsIgnorable(string linea) =>
        linea.Length == 0
        || linea.StartsWith("//", StringComparison.Ordinal)
        || linea.StartsWith("/*", StringComparison.Ordinal)
        || linea.StartsWith('*');

    private static CampoDelFrontend CampoDe(
        string linea,
        string interfaz,
        int numeroDeLinea,
        string origen)
    {
        var campo = DeclaracionDeCampo.Match(linea);
        if (!campo.Success)
        {
            throw NoReconocido(linea, interfaz, numeroDeLinea, origen);
        }

        var declarado = campo.Groups[3].Value.Trim();
        var admiteNull = false;

        // Se acepta exactamente una unión: «| null». Cualquier otra —«string | number»— cae en el
        // camino de abajo y lanza, porque la comparación no sabría contra cuál verificar.
        foreach (var sufijo in new[] { " | null", " | undefined | null" })
        {
            if (declarado.EndsWith(sufijo, StringComparison.Ordinal))
            {
                declarado = declarado[..^sufijo.Length].Trim();
                admiteNull = true;
                break;
            }
        }

        if (declarado.EndsWith(" | undefined", StringComparison.Ordinal))
        {
            declarado = declarado[..^" | undefined".Length].Trim();
        }

        if (declarado.Contains('|', StringComparison.Ordinal))
        {
            throw NoReconocido(campo.Groups[3].Value.Trim(), interfaz, numeroDeLinea, origen);
        }

        var baseDelTipo = declarado.EndsWith("[]", StringComparison.Ordinal)
            ? declarado[..^2]
            : declarado;

        if (!EsIdentificadorSimple(baseDelTipo))
        {
            throw NoReconocido(campo.Groups[3].Value.Trim(), interfaz, numeroDeLinea, origen);
        }

        return new CampoDelFrontend(
            campo.Groups[1].Value,
            declarado,
            admiteNull,
            campo.Groups[2].Success);
    }

    private static bool EsIdentificadorSimple(string texto)
    {
        if (texto.Length == 0)
        {
            return false;
        }

        foreach (var caracter in texto)
        {
            if (!char.IsLetterOrDigit(caracter))
            {
                return false;
            }
        }

        return true;
    }

    private static List<string> LiteralesDe(
        string declaracion,
        string alias,
        int numeroDeLinea,
        string origen)
    {
        var literales = new List<string>();

        foreach (var parte in declaracion.Split('|'))
        {
            var coincidencia = LiteralDeCadena.Match(parte.Trim());
            if (!coincidencia.Success)
            {
                throw NoReconocido(declaracion, alias, numeroDeLinea, origen);
            }

            literales.Add(coincidencia.Groups[1].Value);
        }

        return literales;
    }

    /// <summary>
    /// Un campo que referencia un tipo que no existe es una forma silenciosa de contrato roto: el
    /// recorrido no tendría contra qué comparar y podría darlo por bueno.
    /// </summary>
    private static void VerificarQueLosTiposReferenciadosExisten(
        IReadOnlyList<TipoDelFrontend> tipos,
        Dictionary<string, IReadOnlyList<string>> aliases,
        string origen)
    {
        var conocidos = tipos.Select(t => t.Nombre).ToHashSet(StringComparer.Ordinal);

        foreach (var tipo in tipos)
        {
            foreach (var campo in tipo.Campos)
            {
                var referencia = campo.TipoBase;

                if (TiposPrimitivos.Contains(referencia, StringComparer.Ordinal)
                    || conocidos.Contains(referencia)
                    || aliases.ContainsKey(referencia))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"En «{origen}», la interfaz «{tipo.Nombre}» declara el campo «{campo.Nombre}» " +
                    $"con el tipo «{referencia}», que no es primitivo ni está declarado en el " +
                    "archivo. No hay forma de verificar un campo cuyo tipo no se conoce.");
            }
        }
    }

    private static InvalidOperationException NoReconocido(
        string texto,
        string? interfaz,
        int numeroDeLinea,
        string origen) =>
        new(
            Encabezado(interfaz, numeroDeLinea, origen) +
            $"no se reconoce «{texto}». El lector es deliberadamente estricto: lo que no entiende " +
            "lo rechaza en vez de saltearlo, porque un tipo salteado quedaría pareciendo verificado.");

    private static string Encabezado(string? interfaz, int numeroDeLinea, string origen)
    {
        var donde = interfaz is null ? string.Empty : $", en la interfaz «{interfaz}»";
        var numero = numeroDeLinea.ToString(CultureInfo.InvariantCulture);
        return $"En «{origen}»{donde}, línea {numero}: ";
    }

    /// <summary>
    /// Sube desde el directorio del ensamblado hasta encontrar la raíz del repositorio, igual que
    /// <c>backend/verificar-linter.sh</c> resuelve la suya.
    /// </summary>
    private static string RaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            if (Directory.Exists(Path.Combine(directorio.FullName, ".git")))
            {
                return directorio.FullName;
            }

            directorio = directorio.Parent;
        }

        throw new InvalidOperationException(
            $"No se encontró la raíz del repositorio subiendo desde «{AppContext.BaseDirectory}»: " +
            "ningún directorio ancestro contiene «.git».");
    }
}
