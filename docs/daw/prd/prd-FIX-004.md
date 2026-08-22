# PRD FIX-004: El sembrado de rendimiento vence el 2027-01-01

| Field | Value |
|-------|-------|
| Ticket | FIX-004 |
| Tracker | ninguno |
| Date | 2026-08-22 |
| PRD loops | 0 |

> D-3 del mapa de DISC-001 y último ítem de la deuda de infraestructura. Análisis de causa raíz y
> medición completa en `docs/daw/specs/rca-FIX-004.md`.

## Context and Problem

El arnés que mide el rendimiento de la pantalla principal siembra 1000 movimientos con fechas
**clavadas en 2026**, mientras el endpoint de resumen calcula el mes en curso con el reloj real.
Desde el 2027-01-01 esas dos cosas dejan de intersecarse, y **para siempre**: no es una ventana de un
año, es que el sembrado quedó anclado a un año absoluto.

Medido el 2026-08-22: hoy caen **93 filas** dentro del mes en curso y el oráculo del test vale
**64 356**; desde el 2027-01-01, **0 y 0**.

La causa raíz es que la fecha se escribió **relativa al reloj de quien la escribió y quedó absoluta
en el archivo**. `new DateOnly(2026, 1, 1)` significaba "el 1 de enero del año en curso" en el
momento de escribirse. Lo que nunca se escribió es que la propiedad requerida era *cubrir el mes en
curso, sea cual sea*.

**Hay una atenuante importante, y define lo que este ticket tiene que preservar.**
`ConfirmarQueElMesTieneFilas()` ya existe y afirma que el mes sembrado tiene filas, fallando con un
mensaje que dice cuántas encontró y entre qué fechas. Sin ese guardarraíl, el test pasaría **en verde
midiendo el rendimiento de una consulta vacía** — una medición que no mide nada y no lo dice. El
arnés ya sabe avisar; lo que falta es que no tenga que hacerlo.

## Goals

- Que el arnés de rendimiento mida lo que dice medir en cualquier fecha de ejecución, sin vencimiento.
- Que la propiedad que hoy está implícita en un literal quede **escrita y verificada**, no confiada a
  que alguien recuerde el año en que se escribió.
- Que el guardarraíl existente siga en pie: la protección contra medir una consulta vacía no se
  reemplaza por la corrección, se conserva junto a ella.
- Que no cambie ningún archivo de producción.

## Functional Requirements

- FR-01: El sembrado de rendimiento debe generar fechas que cubran el mes en curso en cualquier fecha de ejecución, sin depender de ningún año escrito literalmente. Origen: causa raíz del RCA.
- FR-02: El proyecto debe conservar `ConfirmarQueElMesTieneFilas()` como afirmación activa del arnés, de modo que una medición sobre un mes vacío siga fallando de forma explícita. Origen: es la defensa que evita medir una consulta vacía en verde.
- FR-03: El proyecto debe mantener la coincidencia entre el rango que `RendimientoListadoTests` consulta y las fechas que el sembrado genera, de modo que ese test siga midiendo sobre filas existentes. Origen: su rango fijo de marzo de 2026 deja de coincidir si el sembrado pasa a ser relativo.
- FR-04: El proyecto debe verificar que el arnés funciona en fechas futuras mediante una comprobación ejecutable, y no por inspección del código. Origen: FIX-001 entregó una barrera cuya única prueba era una comprobación manual, y su verificación salió BLOCKED por eso.
- FR-05: El proyecto debe dejar escrita, junto al sembrado, la propiedad que las fechas deben cumplir, para que un cambio futuro que la rompa sea visible al leer el código. Origen: la propiedad implícita en un literal es exactamente lo que causó este defecto.

## Non-Functional Requirements

- NFR-01: El proyecto debe conservar los 281 casos de la suite en verde, y los 3 tests de rendimiento deben seguir midiendo sobre 1000 movimientos sembrados. Origen: el arnés se corrige, no se degrada.
- NFR-02: El proyecto debe modificar 0 archivos bajo `backend/GestionGastos.Api/` y 0 bajo `frontend/src/`. Origen: es un defecto del arnés de tests, no del producto.
- NFR-03: El proyecto debe agregar 0 dependencias nuevas. Origen: `AGENTS.md` exige justificar toda dependencia nueva en la spec, y acá no hay justificación posible para una.
- NFR-04: Los presupuestos de rendimiento vigentes deben permanecer sin cambios: 1 s para el p95 de una petición y 2 s para el de la pantalla completa. Origen: mover un presupuesto dentro de un ticket que corrige el arnés haría incomparables las mediciones de antes y de después.

## Acceptance Criteria

- AC-01 (FR-01): WHEN se ejecuta el arnés de rendimiento en cualquier fecha, THE sistema SHALL sembrar al menos 2 movimientos con fecha dentro del mes en curso.
- AC-02 (FR-01, FR-04): WHEN se evalúa la generación de fechas del sembrado simulando una fecha de ejecución posterior al 2027-01-01, THE sistema SHALL producir fechas que cubren el mes en curso de esa fecha simulada.
- AC-03 (FR-01): WHEN se inspecciona el generador de fechas del sembrado, THE sistema SHALL no contener ningún año escrito literalmente.
- AC-04 (FR-02): IF el sembrado dejara el mes en curso sin filas suficientes, THEN THE arnés SHALL fallar indicando cuántos movimientos quedaron y entre qué fechas, y SHALL no reportar una medición.
- AC-05 (FR-03): WHEN se ejecuta `RendimientoListadoTests`, THE sistema SHALL consultar un rango que contiene al menos 1 fila del sembrado, en cualquier fecha de ejecución.
- AC-06 (FR-04): WHEN se ejecuta la comprobación de fechas futuras, THE sistema SHALL terminar con código de salida 0 sobre el repositorio corregido.
- AC-07 (FR-04): IF se revirtiera el sembrado a un año literal, THEN THE comprobación de fechas futuras SHALL terminar con código de salida distinto de 0.
- AC-08 (FR-05): WHEN se lee el generador de fechas, THE sistema SHALL exhibir junto a él la propiedad que las fechas deben cumplir y el motivo por el que no puede llevar un año literal.
- AC-09 (NFR-01): WHEN se ejecuta la suite completa, THE sistema SHALL pasar los 281 casos, y los 3 tests de rendimiento SHALL seguir sembrando 1000 movimientos.
- AC-10 (NFR-02): WHEN se inspecciona el diff del ticket, THE sistema SHALL exhibir 0 archivos modificados bajo `backend/GestionGastos.Api/` y 0 bajo `frontend/src/`.
- AC-11 (NFR-03, NFR-04): WHEN se inspeccionan `frontend/package.json`, los `.csproj` y las constantes de presupuesto, THE sistema SHALL exhibir 0 dependencias nuevas y los mismos valores de 1 s y 2 s.

## Out of Scope

- **Cambiar los presupuestos de rendimiento.** NFR-04 los congela a propósito: este ticket corrige el
  instrumento, y mover la vara en el mismo movimiento haría incomparables las mediciones.
- **Cambiar cuántos movimientos se siembran** ni cuántas ejecuciones se cronometran. 1000 y 100 son
  parte del criterio que FEAT-001c aprobó.
- **Rediseñar los tests de rendimiento** más allá de lo que el anclaje de fechas exija.
- **Introducir una abstracción de reloj en producción** (`IClock`, `TimeProvider` o similar). El
  endpoint de resumen usa el reloj real por decisión de FEAT-001c, y cambiarlo es un ticket con su
  propio PRD, no un efecto colateral de arreglar un fixture.
- **Los WARN abiertos de FEAT-003** (W-3, W-4) y el `.gitattributes` que FIX-002 dejó anotado.
- **`ResumenTests.cs:248`**, que quedó redundante tras FEAT-003. Es otro ticket.

## Risks and Mitigations

- **Riesgo: el sembrado relativo rompe `RendimientoListadoTests`.** Su rango está fijo en marzo de
  2026 y coincide con el sembrado actual por casualidad de calendario. → Mitigación: FR-03 y AC-05
  exigen que el rango consultado contenga filas en cualquier fecha, así que ese test se mueve con el
  sembrado en el mismo cambio y no queda descoordinado.
- **Riesgo: el arreglo introduce su propia dependencia del reloj y falla en un borde.** Un sembrado
  derivado de "hoy" puede comportarse distinto el día 1 o el 31 de un mes, o en febrero. →
  Mitigación: AC-02 exige evaluar la generación contra una fecha simulada, y AC-01 pone un piso de 2
  movimientos en el mes en cualquier fecha, no un promedio.
- **Riesgo: se entrega un arreglo cuya única prueba es leer el código.** Es exactamente lo que hizo
  que la verificación ronda 1 de FIX-001 saliera BLOCKED. → Mitigación: FR-04, AC-06 y AC-07 exigen
  una comprobación **ejecutable** y que se demuestre que falla al revertir el arreglo.
- **Riesgo: al corregir el sembrado se debilita el guardarraíl** por parecer redundante. Si las
  fechas ya cubren el mes siempre, `ConfirmarQueElMesTieneFilas()` puede parecer innecesario. →
  Mitigación: FR-02 y AC-04 lo declaran requisito, no residuo. Es la última línea de defensa contra
  medir una consulta vacía, y su valor no depende de que el sembrado esté bien hoy.
- **Riesgo: el cambio se lee como cosmético y nadie mira qué miden los tests después.** → Mitigación:
  AC-09 fija los 281 casos y los 1000 movimientos sembrados como condición explícita.

## Dependencies

- `backend/GestionGastos.Api.Tests/Infra/MedicionDeRendimiento.cs`, donde vive `FechasSembradas` y
  las constantes de presupuesto que NFR-04 congela.
- `backend/GestionGastos.Api.Tests/Movimientos/RendimientoListadoTests.cs`, cuyo rango fijo depende
  del sembrado.
- `backend/GestionGastos.Api.Tests/Resumen/RendimientoResumenTests.cs`, que contiene
  `ConfirmarQueElMesTieneFilas()` y el oráculo `TotalGastadoEsperado`.
- `backend/GestionGastos.Api.Tests/Movimientos/RendimientoAltaTests.cs`, que comparte el sembrado sin
  depender de fechas — se verifica que siga sin depender.
- `docs/daw/specs/rca-FIX-004.md`, de donde salen la causa raíz y la medición.
- El mapa de DISC-001, `docs/daw/discovery/concept-DISC-001.md`, donde D-3 se marca al cerrar.
- La sección Stack de `AGENTS.md`, de donde salen los comandos con que se verifica y la regla que
  NFR-03 aplica: ninguna dependencia nueva sin justificarla en la spec.
- `docs/daw/prd/prd-FEAT-001c.md` y su spec, que fijaron los presupuestos de 1 s y 2 s, los 1000
  movimientos y las 100 ejecuciones que NFR-04 y NFR-01 congelan, y la decisión de que el resumen use
  el reloj real —la que hace que este defecto exista y que Out of Scope deja explícitamente sin
  revisar.
- **Como precedente, no como dependencia funcional:** `docs/daw/reports/verify-FIX-001.md`, de donde
  sale la lección de que una barrera sin comprobación ejecutable no cuenta como verificada.
