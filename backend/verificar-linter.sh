#!/usr/bin/env bash
#
# Verificación de regresión del linter del backend (FIX-001).
#
# POR QUÉ EXISTE ESTE ARCHIVO
# ---------------------------
# El defecto que FIX-001 arregla era la ausencia de señal: el backend compilaba en verde sin que
# nadie mirara convenciones. La barrera que lo arregla es configuración —Directory.Build.props y
# .editorconfig—, y una barrera de configuración se puede desarmar en silencio: alguien pone
# EnforceCodeStyleInBuild en false, o borra un archivo, y el build sigue verde. Sin este script,
# nada se pondría en rojo, que es exactamente el defecto original volviendo.
#
# Verificar la barrera UNA VEZ a mano no alcanza. Esto es lo que la verifica cada vez.
#
# QUÉ COMPRUEBA — las dos direcciones, porque una sola no dice nada:
#   1. Una violación deliberada en código escrito a mano ROMPE el build, indicando la regla.
#   2. La MISMA violación dentro de Migrations/ NO lo rompe  (contra-prueba de la mitigación R-05
#      del threat model: la exclusión de código generado no se ensanchó sobre código real).
#
# Uso:  ./backend/verificar-linter.sh
# Sale 0 si la barrera está en pie, distinto de 0 si no.

set -euo pipefail

raiz="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
solucion="$raiz/backend/GestionGastos.sln"
enProduccion="$raiz/backend/GestionGastos.Api/Common/_PruebaDelLinter.cs"
enMigraciones="$raiz/backend/GestionGastos.Api/Migrations/_PruebaDelLinter.cs"

# El archivo temporal entra en la compilación por el globbing del SDK. Si el script se interrumpe,
# quedaría rompiendo el build de todos, así que se borra pase lo que pase.
limpiar() { rm -f "$enProduccion" "$enMigraciones"; }
trap limpiar EXIT

escribirViolacion() {
    cat > "$1" <<'CS'
namespace GestionGastos.Api.Common;

// Archivo temporal de backend/verificar-linter.sh. Si lo encontrás commiteado, el script se
// interrumpió: borralo. Viola CA1822 a propósito — el miembro no toca estado de instancia.
internal sealed class _PruebaDelLinter
{
    private readonly int valor = 7;

    internal int Constante() => 42;

    internal int Valor() => valor;
}
CS
}

compila() {
    dotnet build "$solucion" --no-incremental --nologo -v quiet > /dev/null 2>&1
}

fallo=0

echo "1/3  La solución compila limpia antes de empezar..."
if compila; then
    echo "     ok"
else
    echo "     FALLA: la solución ya no compila sin tocar nada. Arreglá eso antes de leer el resto."
    exit 1
fi

echo "2/3  Una violación de CA1822 en Common/ tiene que ROMPER el build..."
escribirViolacion "$enProduccion"
if compila; then
    echo "     FALLA: el build pasó con una violación deliberada de CA1822."
    echo "            La barrera está desarmada. Revisá backend/Directory.Build.props"
    echo "            (EnforceCodeStyleInBuild, AnalysisMode) y backend/.editorconfig."
    fallo=1
else
    echo "     ok: el build falló, como corresponde"
fi
rm -f "$enProduccion"

echo "3/3  La MISMA violación dentro de Migrations/ NO tiene que romperlo..."
escribirViolacion "$enMigraciones"
if compila; then
    echo "     ok: el código generado queda fuera del análisis"
else
    echo "     FALLA: la exclusión de Migrations/ no está funcionando."
    echo "            Con esto, cada migración nueva de EF va a romper el build y alguien va a"
    echo "            terminar apagando el linter entero. Revisá la sección"
    echo "            [GestionGastos.Api/Migrations/*.cs] de backend/.editorconfig."
    fallo=1
fi
rm -f "$enMigraciones"

# Dejar los binarios coherentes con el árbol de fuentes real, sin el archivo temporal dentro.
compila || true

if [ "$fallo" -eq 0 ]; then
    echo
    echo "La barrera del linter está en pie."
else
    echo
    echo "La barrera del linter NO está en pie."
fi
exit "$fallo"
