# RCA FIX-004: El sembrado de rendimiento vence el 2027-01-01

| Field | Value |
|-------|-------|
| Ticket | FIX-004 |
| Fecha | 2026-08-22 |
| Origen | D-3 del mapa de DISC-001, registrado antes como W-VER-03 en la verificación de FEAT-001c |

## Síntoma

`MedicionDeRendimiento.FechasSembradas` genera 1000 fechas a partir de `new DateOnly(2026, 1, 1)`,
repartidas sobre 365 días. `RendimientoResumenTests` mide la pantalla principal contra **el mes en
curso**, que el servidor fija con el reloj real.

Desde el 2027-01-01 esas dos cosas dejan de intersecarse.

## Medición

Ejecutada el 2026-08-22 reproduciendo la aritmética del sembrado y del corte por mes:

| Momento | Filas sembradas dentro del mes en curso | `TotalGastadoEsperado` |
|---|---|---|
| **hoy (2026-08-22)** | **93** | **64 356** |
| 2027-01-01 | **0** | **0** |
| 2027-06-15 | **0** | **0** |

**El dato que define el alcance: no es "durante 2027".** `FechasSembradas` está anclado al año 2026
de forma absoluta, así que a partir del 2027-01-01 el sembrado deja el mes en curso vacío **para
siempre**, no en una ventana.

## Causa raíz

La fecha se escribió **relativa al reloj de quien la escribió, y quedó absoluta en el archivo**.

`new DateOnly(2026, 1, 1)` era, en el momento de escribirse, "el 1 de enero del año en curso". El
autor necesitaba que el sembrado cubriera el año que el resumen iba a mirar, y en 2026 esa
expresión lo cumplía. Lo que no se escribió en ninguna parte es que la propiedad requerida era
**"cubrir el mes en curso, sea cual sea"**, y no "cubrir 2026".

No es un descuido de escritura: es una **suposición sobre el momento de ejecución que quedó implícita
en un literal**. Un test que se ejecuta siempre "hoy" tomó prestado el "hoy" de su autor.

### Por qué no lo atrapó nada antes

Nada podía atraparlo. El arnés funciona perfectamente en 2026 y no hay ningún mecanismo en el
repositorio que ejecute los tests contra un reloj futuro. Es un defecto **latente por diseño del
tiempo**, no una regresión que un test pudiera haber cubierto.

Lo que sí existe —y el mapa de DISC-001 omitía— es la mitigación del daño, que se explica abajo.

## Lo que el mapa decía mal

Vale registrarlo porque es el cuarto ítem del mapa que resulta impreciso, y el primero que se
equivoca **exagerando** en vez de subestimando.

| Lo que el mapa afirma | Lo verificado |
|---|---|
| *"obliga a revisar los otros dos tests de rendimiento que comparten `SembrarMovimientosAsync`"* | **Sólo uno se ve afectado.** `RendimientoListadoTests` usa un rango fijo (`2026-03-01`/`2026-03-31`) que coincide con el sembrado y no depende del reloj. `RendimientoAltaTests` no tiene ninguna referencia a fechas |
| No menciona ninguna defensa existente | **`ConfirmarQueElMesTieneFilas()` ya existe** y afirma `IndicesSembradosDelMes.Count > 1`, fallando con un mensaje que nombra cuántos movimientos quedaron y entre qué fechas |

**El guardarraíl cambia la naturaleza del defecto, y para bien.** Sin él, `TotalGastadoEsperado`
valdría 0, el endpoint devolvería 0 para un mes vacío, y el test pasaría **en verde midiendo el
rendimiento de una consulta sin filas** — el modo de falla más peligroso que existe: una medición que
no mide nada y no lo dice. Quien escribió FEAT-001c lo previó y lo convirtió en un fallo explícito.

Así que el defecto real es: **un arnés con fecha de vencimiento que nadie puso a propósito**, con el
daño ya acotado a "falla ruidosamente" en vez de "miente en silencio".

## Alcance

- `backend/GestionGastos.Api.Tests/Infra/MedicionDeRendimiento.cs` — el literal de `FechasSembradas`.
- `backend/GestionGastos.Api.Tests/Movimientos/RendimientoListadoTests.cs` — su rango fijo de marzo
  de 2026 deja de coincidir con el sembrado si éste pasa a ser relativo, así que se mueve con él.
- `backend/GestionGastos.Api.Tests/Resumen/RendimientoResumenTests.cs` — es quien se beneficia; no
  debería necesitar cambios, y que no los necesite es parte de la verificación.
- **Ningún archivo de producción.**

## Lo que este RCA deja decidido y lo que no

**Decidido:** la propiedad que el sembrado tiene que cumplir es *cubrir el mes en curso en cualquier
fecha de ejecución*, y esa propiedad tiene que quedar **escrita**, no implícita en un literal — que
es exactamente lo que falló acá.

**Sin decidir, para PLAN:** cómo se ancla. Hay al menos tres formas —derivar del reloj en el momento
de sembrar, inyectar la fecha base, o fijar el reloj en los tests de rendimiento— y la elección
cambia qué garantías da el arnés y cuánto se mueve `RendimientoListadoTests`. Va con el impact scan
delante.
