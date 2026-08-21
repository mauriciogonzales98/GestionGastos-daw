#!/usr/bin/env bash
#
# Verificación de regresión de la barrera del contrato (FEAT-003).
#
# POR QUÉ EXISTE ESTE ARCHIVO
# ---------------------------
# El defecto que FEAT-003 arregla es que el contrato HTTP está escrito dos veces —en los record de
# C# y en las interfaces de frontend/src/api/tipos.ts— y nada comparaba las dos copias. Medido: un
# rename coherente del lado del backend dejaba en verde el build, los 142 tests del backend, tsc,
# los 105 de Vitest, ESLint y la barrera del linter, y llegaba `undefined` a la pantalla.
#
# La barrera que lo arregla son tests. Y unos tests que pasan sobre un repositorio correcto no
# prueban nada: un comparador demasiado permisivo, un parser que saltea lo que no entiende, o una
# colección vacía leída como "todo bien" dan exactamente el mismo verde. Una barrera que nunca falla
# es indistinguible de no tener barrera, salvo por la confianza que genera — que la hace peor.
#
# Este script es lo que comprueba que la barrera SE PONE EN ROJO cuando tiene que ponerse.
# Es la lección de FIX-001, cuya verificación ronda 1 salió BLOCKED precisamente porque el test de
# regresión sólo existía como la narración de una comprobación manual ya borrada.
#
# QUÉ COMPRUEBA
#   1. La verificación de contrato pasa sobre el árbol sin tocar.
#   2. Con un campo renombrado en un DTO de producción, FALLA.
#   3. Revertido el rename, vuelve a pasar.
#
# CUIDADO — este script MODIFICA un archivo de producción y lo revierte (R-06 del threat model).
# Si se interrumpe, el `trap` de abajo restaura el original. El archivo original se guarda antes de
# tocarlo y se restaura tal cual: la reversión no reconstruye nada a mano.
#
# Uso:  ./backend/verificar-contrato.sh
# Sale 0 si la barrera está en pie, distinto de 0 si no.

set -euo pipefail

raiz="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
solucion="$raiz/backend/GestionGastos.sln"
dto="$raiz/backend/GestionGastos.Api/Resumen/ResumenMensualDto.cs"
respaldo="$(mktemp)"

cp "$dto" "$respaldo"

restaurar() {
    cp "$respaldo" "$dto"
    rm -f "$respaldo"
}

# --no-build no sirve acá: el punto del script es recompilar con el DTO cambiado.
#
# La salida se guarda en vez de descartarse, y se vuelca con `mostrarLaSalida` cuando el paso que la
# produjo no dio lo esperado. Descartarla dejaba el log del CI diciendo "el contrato ya no verifica"
# SIN decir que campo ni que endpoint, cuando el comparador ya tiene ese mensaje y esta testeado:
# una barrera que detecta el problema pero no sabe explicarlo, que es la falla que este ticket
# combate. Detectado como W-2 en la verificacion ronda 2.
#
# La verbosidad es `normal` y no `quiet` a proposito: con `quiet`, dotnet test nombra el test
# que fallo pero NO el mensaje de la asercion, que es justamente donde estan el campo y el
# endpoint. Comprobado rompiendo el contrato y leyendo el volcado.
salida="$(mktemp)"
trap 'restaurar; rm -f "$salida"' EXIT

verificacionDeContrato() {
    dotnet test "$solucion" --filter "FullyQualifiedName~Contrato" --nologo -v normal > "$salida" 2>&1
}

mostrarLaSalida() {
    echo
    echo "     ----- salida de dotnet test -----"
    sed 's/^/     /' "$salida"
    echo "     ---------------------------------"
}

fallo=0

echo "1/3  La verificación de contrato pasa sobre el árbol sin tocar..."
if verificacionDeContrato; then
    echo "     ok"
else
    echo "     FALLA: el contrato ya no verifica sin haber tocado nada."
    echo "            Arreglá eso antes de leer el resto: los pasos 2 y 3 no dirían nada."
    mostrarLaSalida
    exit 1
fi

echo "2/3  Con un campo del DTO renombrado, la verificación tiene que FALLAR..."
# El mismo rename que el PRD usó para medir el defecto: antes de este ticket dejaba todo en verde.
sed -i 's/    decimal TotalIngresado,/    decimal Ingresos,/' "$dto"
if verificacionDeContrato; then
    echo "     FALLA: el contrato verificó con un campo renombrado en el backend."
    echo "            La barrera está desarmada: revisá el comparador y el lector en"
    echo "            backend/GestionGastos.Api.Tests/Contrato/."
    fallo=1
else
    echo "     ok: la verificación falló, como corresponde"
fi

cp "$respaldo" "$dto"

echo "3/3  Revertido el rename, tiene que volver a pasar..."
if verificacionDeContrato; then
    echo "     ok"
else
    echo "     FALLA: no volvió a pasar tras revertir. El árbol puede haber quedado sucio."
    mostrarLaSalida
    fallo=1
fi

# El árbol tiene que quedar como estaba: si no, alguien commitea un cambio que nadie escribió, y
# este script habría causado el defecto que el ticket vino a prevenir.
if ! git -C "$raiz" diff --quiet -- "$dto"; then
    echo
    echo "FALLA: «$dto» quedó modificado. Revisalo con git diff antes de commitear nada."
    fallo=1
fi

if [ "$fallo" -eq 0 ]; then
    echo
    echo "La barrera del contrato está en pie."
else
    echo
    echo "La barrera del contrato NO está en pie."
fi
exit "$fallo"
