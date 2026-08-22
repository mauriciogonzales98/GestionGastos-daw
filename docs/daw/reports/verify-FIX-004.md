# Verificación FIX-004

## Ronda 1: PASSED, con un error propio confirmado

| Field | Value |
|-------|-------|
| Ticket | FIX-004 |
| Tier | FIX |
| Ronda | 1 |
| Fecha | 2026-08-22 |
| Resultado | **PASSED** — 0 FAIL, 3 WARN, 11 PASS |
| Verificador | `daw-module-verifier`, sobre código que no escribió |

```
FAILs: 0 | WARNs: 3 | PASSes: 11  →  PASSED
```

El verificador reejecutó la contra-prueba, la suite y los `git diff` en vez de aceptar los números
del cierre de CODE. Los once AC quedan verificados y el gate cumplido.

## Los once AC

| AC | Verificación |
|---|---|
| AC-01 | Piso de 2 filas en el mes, recalculado de forma independiente: mínimo real por día = 2. **Ver W-1**: el peor mes documentado estaba mal |
| AC-02, AC-06, AC-07 | Contra-prueba **reproducida por el verificador**: con el año literal reintroducido, 6 de 8 tests en rojo y 2 en verde —volumen y estabilidad frente al día—, exactamente como el cierre afirmaba. Reversión confirmada |
| AC-03 | Sin literales de año funcionales. El «2026» que queda está dentro del XML-doc, citando el defecto histórico como texto explicativo |
| AC-04 | `ConfirmarQueElMesTieneFilas()` presente y sin tocar. **Ver W-3** |
| AC-05 | `Desde`/`Hasta` derivan de `DateTime.Today.Year`; `esperadas` sigue saliendo de `FechasSembradas` |
| AC-09 | 184/184 backend reejecutado por el verificador; 1000 movimientos y 100 ejecuciones intactos |
| AC-10 | `git diff` sobre producción: vacío, reejecutado |
| AC-11 | 0 dependencias; presupuestos en 1 s y 2 s |

Los 4 pasos del fix-plan implementados, y el **paso 4 confirmado como predicción cumplida**:
`RendimientoResumenTests` no se tocó, y el verificador razonó que es correcto — el oráculo y el
guardarraíl de ese archivo ya eran relativos al reloj, así que el defecto vivía enteramente en el
sembrado.

## W-1 — El «peor mes» estaba mal calculado, en tres documentos

**Es un error propio, y conviene que quede escrito con su corrección.**

El RCA, el fix-plan y el XML-doc de `GenerarFechasSembradas` afirman: *«el peor mes posible —un
febrero común de 28 días— deja 56 filas»*. **Es falso.**

El razonamiento fue "mínimo por día × mes más corto", sin mirar **dónde** caen los días de mínimo.
Con `i % 365` sobre 1000 elementos, los offsets 0–269 llevan 3 filas y los 270–364 llevan 2: los días
de mínimo están en **los últimos 95 del año**, no en febrero. Recalculado:

```
ene 93 · feb 84 · mar 93 · abr 90 · may 93 · jun 90
jul 93 · ago 93 · sep 87 · oct 62 · nov 60 ← mínimo · dic 62
```

**El peor mes es noviembre con 60 filas**, y febrero —lejos de ser el mínimo— tiene 84.

No cambia ningún veredicto: 60 sigue siendo 30 veces el piso de 2 que AC-01 exige. Pero es una
afirmación falsa presentada como el peor caso real, en un comentario de código que alguien podría
citar más adelante.

**Corregido en la ronda 2** en el XML-doc, que es el lugar que se lee. El RCA y el fix-plan no se
pueden modificar desde CODE ni VERIFY, así que su errata queda registrada acá — misma tensión del
método que FIX-001 dejó anotada.

## W-2 — El test «bisiesto vs común» no distingue nada

`Generador_EnFebrero_CubreElMesEnBisiestoYEnComun` aplica **la misma aserción** a 2028 y a 2029:
`NotEmpty` más `Month == 2`. No compara conteos ni verifica el 29 de febrero.

No es un test vacío —puede fallar ante una regresión real del generador— pero **su nombre promete una
distinción que el test no hace**. En un ticket sobre arneses que no miden lo que dicen medir, eso es
particularmente incómodo.

**Corregido en la ronda 2.**

## W-3 — El rol del guardarraíl cambió, y no quedó dicho

Con el sembrado anclado al año completo, el mes en curso tiene siempre ≥60 filas. Si el generador se
rompiera, `GeneradorDeFechasSembradasTests` lo detectaría **antes** de que
`ConfirmarQueElMesTieneFilas()` llegara a activarse.

El guardarraíl pasó de **«última línea de defensa»** —como lo llaman el PRD y el RCA— a **defensa
redundante**. Sigue valiendo conservarlo: FR-02 lo pide, su costo es cero, y puede activarse ante una
rotura que el test unitario no cubra. Pero la descripción era optimista.

**Se registra y no se corrige**: tocar `RendimientoResumenTests` contradiría la predicción del paso 4
que el propio ticket verificó, y el cambio sería sólo de redacción.

## Acción

**Gate `verify` cumplido.** El usuario pidió corregir W-1 y W-2 antes de RELEASE en vez de
arrastrarlos, así que va un bucle a CODE por esos dos. W-3 queda como deuda declarada.

---

## Ronda 2: BLOCKED, por un compromiso del fix-plan que nadie implementó

| Field | Value |
|-------|-------|
| Ticket | FIX-004 |
| Tier | FIX |
| Ronda | 2 |
| Fecha | 2026-08-22 |
| Resultado | **BLOCKED** — 1 FAIL, 1 WARN, 12 PASS |
| Verificador | verificación directa sobre el árbol en `afdff9a`, reejecutando todo |

```
FAILs: 1 | WARNs: 1 | PASSes: 12  →  BLOCKED (intento 2/3)
```

Las dos correcciones del bucle son reales y están comprobadas. Lo que aparece es **otra cosa**, que
la ronda 1 dio por buena: un compromiso explícito del paso 2 del fix-plan que no está en el código.

### Las dos correcciones del bucle — verificadas

**W-1 — recalculado de forma independiente, y ahora el comentario dice la verdad.** Reproduje el
reparto `i % 365` sobre 1000 elementos para un año común (2029) y uno bisiesto (2028):

| Mes | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 2029 (común) | 93 | **84** | 93 | 90 | 93 | 90 | 93 | 93 | 87 | 62 | **60** | 62 |
| 2028 (bisiesto) | 93 | 87 | 93 | 90 | 93 | 90 | 93 | 93 | 86 | 62 | **60** | 60 |

El mínimo es **60 en noviembre** y febrero común tiene **84**, exactamente como el comentario
corregido afirma. La afirmación vieja —«56 en febrero»— era falsa. ✅

**W-2 — la partición distingue de verdad.** `Generador_EnUnAnioBisiesto_SiembraElVeintinueveDeFebrero`
exige `Contains(2028-02-29)` y 29 días distintos; `Generador_EnUnAnioComun_NoIntentaSembrarElVeintinueveDeFebrero`
exige `DoesNotContain` y 28. Son aserciones distintas sobre datos distintos, y el nombre de cada una
describe lo que hace. ✅

**Contra-prueba reejecutada sobre el archivo ya partido** (no sobre el de la ronda 1): reintroduje
`new DateOnly(2026, 1, 1)`, corrí el filtro del generador y salió **6 fallados / 2 pasados, exit 1**.
Los dos Fact nuevos de febrero **fallan los dos**, lo que confirma que la partición no debilitó la
sensibilidad al ancla. Los 2 verdes son los que no dependen del año —volumen y estabilidad frente al
día del mes—, que es lo correcto. Archivo restaurado, árbol limpio, y con el arreglo puesto: 8/8 en
verde, exit 0.

### ❌ FAIL — F-VER-06: el paso 2 prometió una aserción que no está

El fix-plan, en **Error handling**, dice literalmente:

> El test ya afirma contra `esperadas` derivado del sembrado; si el rango no contuviera filas,
> `esperadas` sería 0 y el test compararía 0 con 0. **Por eso el paso 2 agrega una afirmación de que
> el rango contiene al menos una fila (AC-05)** — el mismo modo de falla que
> `ConfirmarQueElMesTieneFilas` cubre del otro lado.

Y la lista de Tests lo repite como compromiso: «`RendimientoListadoTests` consulta un rango que
contiene al menos una fila del sembrado (AC-05)».

**Esa aserción no existe.** Las cinco `Assert` de `RendimientoListadoTests.cs` son: dos de
`HttpStatusCode.OK`, dos de `Equal(esperadas, ...)` y la del p95. Ninguna afirma `esperadas > 0`.

El modo de falla que el fix-plan nombró sigue abierto: si `Desde`/`Hasta` dejaran de coincidir con el
sembrado, `esperadas` valdría 0, la API devolvería 0 y el test **pasaría en verde cronometrando una
consulta vacía**. Es el mismo defecto que este ticket existe para cerrar, en el único de los dos
tests de rendimiento que no tiene guardarraíl propio — `RendimientoResumenTests` sí lo tiene
(`ConfirmarQueElMesTieneFilas()`), y ese contraste es justamente el que el fix-plan invoca.

**Hoy la propiedad se cumple de hecho:** el ancla es el 1 de enero del año en curso y el rango es
marzo del año en curso, así que caen 93 filas siempre. Por eso AC-05 no está *incumplido* — lo que
falta es lo que el paso 2 aprobó para que dejara de depender de que nadie toque ninguno de los dos
lados.

**La ronda 1 lo dio por PASS** verificando que `Desde`/`Hasta` derivan de `DateTime.Today.Year` y que
`esperadas` sale de `FechasSembradas`. Ambas cosas son ciertas, pero ninguna es la aserción que el
paso 2 comprometió, y ninguna cierra el paso en verde sobre cero filas.

**Corrección:** una línea en `RendimientoListadoTests`, antes del calentamiento — afirmar que
`esperadas` es mayor que 0, con un mensaje que diga el rango y el conteo, como hace
`ConfirmarQueElMesTieneFilas()`.

### ⚠️ W-4 — «el peor mes es noviembre» empata en diciembre los años bisiestos

El comentario corregido dice «el peor mes es **noviembre con 60 filas**». El **piso de 60 es
correcto**, pero en un año bisiesto diciembre también da 60: con 365 offsets desde el 1 de enero de
un año de 366 días, el 31 de diciembre nunca se siembra.

No cambia ninguna conclusión —el piso que AC-01 exige sigue siendo 2— y no es una afirmación falsa
como lo era W-1. Se registra porque es el mismo comentario que ya llevó un número mal una vez, y
porque ningún test afirma sobre estos números: se verifican leyéndolos.

### Los once AC

| AC | Verificación en la ronda 2 |
|---|---|
| AC-01 | ✅ Recalculado independientemente: mínimo por mes = 60, contra un piso exigido de 2. Test `Generador_EnCualquierMesDelAnio_DejaAlMenosDosFilasEnEseMes` recorre los 12 meses |
| AC-02 | ✅ `Generador_EnFechasPosterioresAlVencimientoViejo_CubreElMesDeEsaFecha` con 2027-01-01, 2027-06-15 y 2030-12-31 |
| AC-03 | ✅ El cuerpo del generador es `new DateOnly(hoy.Year, 1, 1)`. El «2026» que queda está en el XML-doc, citando el defecto |
| AC-04 | ✅ `ConfirmarQueElMesTieneFilas()` intacto, invocado en las líneas 76 y 159 de `RendimientoResumenTests` |
| **AC-05** | ❌ **Ver el FAIL.** La propiedad se cumple de hecho, pero la aserción que el paso 2 comprometió no está |
| AC-06 | ✅ 8/8 en verde, exit 0, reejecutado |
| AC-07 | ✅ Contra-prueba reejecutada **sobre el archivo ya partido**: 6/8 en rojo, exit 1 |
| AC-08 | ✅ La propiedad y el motivo están escritos junto al generador, ahora con el número correcto |
| AC-09 | ✅ **289 verdes reejecutados**: 184 backend + 105 frontend. `MovimientosSembrados = 1000`, `Ejecuciones = 100` |
| AC-10 | ✅ `git diff main..HEAD` sobre `backend/GestionGastos.Api/` y `frontend/src/`: vacío |
| AC-11 | ✅ 0 deps nuevas (diff vacío en `package.json`, lock y `.csproj`); `PresupuestoP95` = 1 s, `PresupuestoP95Pantalla` = 2 s |

### Los pasos del fix-plan

| Paso | Estado |
|---|---|
| 1 — función pura y ancla relativa | ✅ `GenerarFechasSembradas(DateOnly)`, patrón de `RangoDelMes.De` |
| 2 — rango de `RendimientoListadoTests` | ⚠️ El rango deriva del ancla ✅, **falta la aserción de «al menos una fila»** ❌ |
| 3 — test del generador | ✅ 8 tests, fechas simuladas, sin tocar el reloj |
| 4 — `RendimientoResumenTests` no se toca | ✅ Predicción cumplida: no aparece en `git diff main..HEAD` |

### Calidad

| Regla | Resultado |
|---|---|
| F-VER-03 cobertura | ✅ El código cambiado es infraestructura de test, ejercitada directamente por los 8 tests del generador; `GenerarFechasSembradas` no tiene ramas |
| F-VER-04 sad paths | ✅ El generador se ejercita con año bisiesto, común, primer y último día del mes, y años lejanos |
| F-VER-05 lint / typecheck | ✅ Build `-warnaserror` 0 warnings · `dotnet format --verify-no-changes` limpio · `eslint` limpio · `tsc --noEmit` limpio |
| W-VER-01 código muerto | ✅ Ninguno |
| W-VER-03 tests frágiles | ✅ Los tests del generador reciben fechas literales como **entrada de una función pura**, que es el patrón de `RangoDelMesTests`, no una dependencia del reloj |

### Acción

**Gate `verify` NO cumplido.** Bucle a CODE (intento 2/3) por el FAIL de AC-05: agregar la aserción
que el paso 2 del fix-plan comprometió. Es un cambio de una línea y no toca producción.

W-4 queda a criterio: es un comentario, no un test, y su corrección es de una frase.
W-3 sigue como deuda declarada de la ronda 1, sin tocar.
