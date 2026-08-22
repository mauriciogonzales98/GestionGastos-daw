# Fix-plan FIX-004: El sembrado de rendimiento vence el 2027-01-01

| Field | Value |
|-------|-------|
| Ticket | FIX-004 |
| Tier | FIX |
| RCA | docs/daw/specs/rca-FIX-004.md |
| Date | 2026-08-22 |
| Spec loops | 0 |

## Problem

El arnés que mide el rendimiento de la pantalla principal siembra 1000 movimientos con fechas
clavadas en 2026 (`new DateOnly(2026, 1, 1).AddDays(i % 365)`), mientras `RendimientoResumenTests`
mide contra **el mes en curso**, que sale del reloj real.

Desde el 2027-01-01 las dos cosas dejan de intersecarse, y **para siempre**: no es una ventana de un
año, es que el sembrado quedó anclado a un año absoluto. Medido: hoy caen 93 filas dentro del mes y
el oráculo vale 64 356; desde el 2027-01-01, 0 y 0.

## Root cause

La fecha se escribió **relativa al reloj de quien la escribió y quedó absoluta en el archivo**.
`new DateOnly(2026, 1, 1)` significaba "el 1 de enero del año en curso" en el momento de escribirse.
Lo que nunca se escribió es que la propiedad requerida era *cubrir el mes en curso, sea cual sea*.

**Dato que acota el daño, y que el fix debe preservar:** `ConfirmarQueElMesTieneFilas()`
(`RendimientoResumenTests.cs:218`) ya afirma que el mes sembrado tiene filas. Sin él, el test pasaría
en verde midiendo el rendimiento de una consulta vacía. El arnés ya sabe avisar; lo que falta es que
no tenga que hacerlo.

## Solution — steps

### Paso 1 — `MedicionDeRendimiento.cs`: extraer la generación a una función pura

**El diseño no se inventa acá: se copia del propio repositorio.** `RangoDelMes.De(DateOnly fecha)`
(`backend/GestionGastos.Api/Resumen/RangoDelMes.cs:17`) ya resuelve este problema en producción —
función pura parametrizada por fecha, que `ResumenEndpoints.cs:37` llama con
`DateOnly.FromDateTime(DateTime.Today)` y que `RangoDelMesTests` prueba con fechas simuladas,
incluido un febrero bisiesto. Es el patrón del proyecto para lógica que depende del calendario, y
este paso lo aplica al arnés.

- Se agrega `public static IReadOnlyList<DateOnly> GenerarFechasSembradas(DateOnly ancla)`, que
  reparte `MovimientosSembrados` fechas sobre 365 días a partir de `ancla`.
- `FechasSembradas` pasa a ser el envoltorio: llama a la función con **el 1 de enero del año en
  curso**, derivado de `DateTime.Today`.
- Junto a la función queda escrita la propiedad que las fechas deben cumplir y por qué no puede
  llevar un año literal (FR-05, AC-08). El literal fue la causa raíz; el comentario es lo que evita
  que alguien lo reintroduzca.

**Por qué el ancla es el 1 de enero del año en curso y no una ventana alrededor de hoy.** Con el año
completo, cualquier mes queda cubierto sin analizar bordes de calendario. Calculado, no estimado:
`i % 365` sobre 1000 elementos deja 270 días con 3 filas y 95 con 2, así que **el mínimo por día es
2** y el peor mes posible —un febrero común de 28 días— deja **56 filas**, contra el piso de 2 que
AC-01 exige. Una ventana corta centrada en hoy sí tendría bordes que demostrar caso por caso.

**Lo que NO cambia:** `MovimientosSembrados` (1000), `Ejecuciones` (100), `PresupuestoP95` (1 s) y
`PresupuestoP95Pantalla` (2 s). NFR-04 los congela para que las mediciones de antes y después sean
comparables.

### Paso 2 — `RendimientoListadoTests.cs`: derivar el rango del mismo ancla

`Desde` y `Hasta` están fijos en marzo de 2026 y coinciden con el sembrado **por casualidad de
calendario**. En cuanto el sembrado se vuelve relativo, ese rango deja de encontrar filas.

El impact scan lo confirmó: **no es opcional en ninguna de las tres opciones evaluadas**. Los dos
literales pasan a derivarse del mismo ancla que el sembrado, de modo que el rango consultado siga
conteniendo filas en cualquier fecha de ejecución (FR-03, AC-05).

El cálculo de `esperadas` (`línea 42`) no cambia: ya se deriva de `FechasSembradas` filtrando por el
rango, así que se mueve solo.

### Paso 3 — Test nuevo: la comprobación ejecutable

`backend/GestionGastos.Api.Tests/Infra/GeneradorDeFechasSembradasTests.cs` (nuevo).

Llama a `GenerarFechasSembradas` con **fechas simuladas posteriores al 2027-01-01** y comprueba que
el mes de esa fecha simulada queda cubierto. Sigue el patrón exacto de `RangoDelMesTests`: fechas
literales como entrada, aserciones sobre la salida, **sin tocar el reloj del sistema**.

Sin esto, el arreglo sólo se podría verificar leyendo el código — que es precisamente lo que hizo que
la verificación ronda 1 de FIX-001 saliera BLOCKED.

### Paso 4 — `RendimientoResumenTests.cs`: no se toca, y se comprueba que no hace falta

Este archivo ya es relativo al reloj (`línea 27`) y su oráculo depende del **orden** de las fechas,
no de sus valores absolutos. La predicción del plan es que **no necesita ningún cambio**.

Que no lo necesite es parte de la verificación, no una suposición: si al ejecutar hiciera falta
tocarlo, el diseño del paso 1 estaría mal y habría que revisarlo antes de seguir.

## Dependencies between steps

Orden: **1 → 2 → 3**, y el paso 4 es una comprobación que se hace después de los tres.

| Paso | Depende de | Motivo |
|---|---|---|
| 1 — función pura y ancla relativa | — | Es lo que los demás consumen |
| 2 — rango de `RendimientoListadoTests` | 1 | Deriva del ancla que el paso 1 define; entre el paso 1 y el 2 ese test queda en rojo, así que van en un solo commit |
| 3 — test del generador | 1 | Prueba la función que el paso 1 extrae |
| 4 — comprobar que Resumen no cambia | 1, 2, 3 | Es una verificación, no una modificación |

## Error handling

- **`GenerarFechasSembradas` recibe una fecha ancla que no es 1 de enero.** La función no lo exige:
  reparte 365 días desde el ancla que reciba. El envoltorio es el que decide anclar en enero, y el
  test del paso 3 ejercita anclas arbitrarias para que quede claro que la función no asume el día.
- **El sembrado deja el mes en curso sin filas suficientes.** `ConfirmarQueElMesTieneFilas()` ya lo
  cubre y **se conserva** (FR-02, AC-04): falla nombrando cuántos movimientos quedaron y entre qué
  fechas, en vez de reportar una medición vacía.
- **El rango de `RendimientoListadoTests` queda sin filas.** El test ya afirma contra `esperadas`
  derivado del sembrado; si el rango no contuviera filas, `esperadas` sería 0 y el test compararía
  0 con 0. Por eso el paso 2 agrega una afirmación de que el rango contiene al menos una fila
  (AC-05) — el mismo modo de falla que `ConfirmarQueElMesTieneFilas` cubre del otro lado.

## Tests

- [ ] **Test de regresión** — reproduce el defecto original: `GenerarFechasSembradas` evaluada con
      una fecha simulada posterior al 2027-01-01 produce fechas que cubren el mes de esa fecha.
      Antes del fix, el generador con año literal deja ese mes vacío (AC-02).
- [ ] El generador con anclas de distintos días del mes y distintos años produce cobertura del mes
      de la fecha simulada, incluido un febrero bisiesto (AC-01, AC-02).
- [ ] `GenerarFechasSembradas` no contiene ningún año escrito literalmente, verificado por
      inspección del archivo en el reporte de cierre (AC-03).
- [ ] `ConfirmarQueElMesTieneFilas()` sigue presente y sigue afirmando (AC-04).
- [ ] `RendimientoListadoTests` consulta un rango que contiene al menos una fila del sembrado
      (AC-05).
- [ ] La comprobación ejecutable sale 0 sobre el repositorio corregido (AC-06).
- [ ] **Contra-prueba:** al revertir el ancla a un año literal, la comprobación sale distinto de 0
      (AC-07). Sin esta, los tests de arriba no prueban que detecten nada.
- [ ] Junto al generador está escrita la propiedad que las fechas deben cumplir y el motivo por el
      que no puede llevar un año literal (AC-08).
- [ ] La suite completa pasa: 281 casos previos más los nuevos, y los 3 tests de rendimiento siguen
      sembrando 1000 movimientos (AC-09, NFR-01).
- [ ] `git diff` no muestra archivos bajo `backend/GestionGastos.Api/` ni bajo `frontend/src/`
      (AC-10, NFR-02).
- [ ] 0 dependencias nuevas y los presupuestos siguen en 1 s y 2 s (AC-11, NFR-03, NFR-04).

## Regression risk

**Bajo**, y acotado al arnés.

- **No se toca producción.** El cambio vive entero en el proyecto de tests, y AC-10 lo verifica con
  `git diff` en vez de afirmarlo.
- **Lo que sí cambia es qué miden dos tests.** `RendimientoListadoTests` pasa a medir un mes distinto
  del que medía —marzo de 2026— y `RendimientoResumenTests` pasa a medir sobre un sembrado que
  siempre cubre su mes. Los presupuestos no se mueven (NFR-04), así que los números siguen siendo
  comparables contra el mismo criterio.
- **El riesgo real es que el arreglo introduzca su propia dependencia del reloj y falle en un
  borde.** Mitigado por el ancla de año completo, cuyo peor caso está calculado —56 filas en febrero
  contra un piso de 2— y por el test del paso 3, que ejercita fechas simuladas en vez de confiar en
  el día en que corra el CI.
- **El otro riesgo es debilitar el guardarraíl por parecer redundante.** FR-02 lo declara requisito.
  Su valor no depende de que el sembrado esté bien hoy.

## Rollback plan *(mandatory)*

- **Pasos:** revertir el commit. El cambio es un archivo de infraestructura de tests, un archivo de
  test y un archivo de test nuevo; no hay migración, ni esquema, ni contrato de API, ni dato que
  revertir. El estado anterior vuelve a funcionar hasta el 2026-12-31.
- **Indicadores para aplicarlo:** los tests de rendimiento se vuelven inestables entre corridas —
  midiendo distinto según el día del mes— o el CI empieza a fallar por el arnés y no por el código
  bajo prueba. En cualquiera de los dos casos el diseño del ancla estaría mal y conviene volver atrás
  antes de parchear.
- **Ventana:** revertir es seguro hasta el 2026-12-31. Después de esa fecha el estado anterior está
  roto, así que un rollback tardío exige rehacer el arreglo, no volver al original.

## Lo que este ticket NO toca, y por qué conviene que quede escrito

El impact scan encontró **decenas de literales de fecha en 2026** en tests funcionales
(`CrearMovimientoTests`, `FiltrarMovimientosTests`, `ModeloDeDatosTests`, y otros). **Ninguno
comparte este defecto**: comparan un dato contra sí mismo —fecha guardada contra fecha literal, o
entrada literal contra salida esperada— y no contra el mes en curso calculado con el reloj real. No
vencen.

Queda escrito para que nadie los "arregle" por inercia después de leer este ticket: cambiarlos a
fechas relativas los volvería más frágiles, no menos.
