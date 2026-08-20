# PRD DISC-001-04b: Registrar y filtrar en varias monedas

| Field | Value |
|-------|-------|
| Ticket | DISC-001-04b |
| Tracker | ninguno |
| Date | 2026-08-20 |
| PRD loops | 0 |

> Séptimo de los nueve PRDs de **DISC-001** (mapa en `docs/daw/discovery/concept-DISC-001.md`),
> recorte de PRD-001 (`docs/daw/prd/PRD.md`, versión 5). La columna "Origen" de cada requerimiento
> traza al RF del PRD del producto.
>
> Segundo de los dos cortes de multi-moneda. `04a` dejó el catálogo y los totales separados por
> moneda; **`04b` (este)** es el que le da al usuario la posibilidad de elegir una. Depende de `04a`
> y no se puede adelantar: sin él, elegir dólares produce un total que suma pesos con dólares.

## Context and Problem

Después de `04a` la aplicación sabe todo lo que necesita saber sobre monedas —cuáles existen, cuál
es la predeterminada, cómo separar los totales— y el usuario no puede usar nada de eso. Todos sus
movimientos siguen quedando en pesos porque el formulario no ofrece otra cosa.

Este ticket abre esa puerta, y son cuatro aberturas concretas: **elegir** la moneda al registrar
(RF-24, RF-25), **verla** en cada fila del listado (RF-27), **filtrar** el listado por ella (RF-28)
y **corregirla** sin borrar el movimiento (RF-14 aplicado a la moneda).

La última no es un detalle. El riesgo de producto de multi-moneda, anotado en PRD-001, es cargar un
movimiento en la moneda equivocada por no mirar el selector — y sus tres mitigaciones son
precisamente los requerimientos de este ticket: pesos como valor por defecto, la moneda visible en
cada fila, y la posibilidad de corregirla.

## Goals

- Que el usuario pueda registrar un gasto o un ingreso en cualquier moneda del catálogo, sin que eso
  agregue un paso a quien usa una sola.
- Que al mirar el listado nunca haya duda de en qué moneda está cada monto.
- Que cargar un movimiento en la moneda equivocada se pueda corregir sin borrarlo y volver a
  cargarlo.

## Functional Requirements

- FR-01: El sistema debe permitir registrar cada movimiento en una de las monedas del catálogo, eligiéndola en el formulario de registro. Origen: RF-24.
- FR-02: El sistema debe proponer como valor por defecto del campo moneda la moneda marcada como predeterminada en el catálogo. Origen: RF-25.
- FR-03: El sistema debe ofrecer en el selector de moneda exactamente las monedas del catálogo. Origen: RF-31, RF-32.
- FR-04: El sistema debe mostrar la moneda de cada movimiento en su fila del listado. Origen: RF-27.
- FR-05: El sistema debe permitir filtrar el listado de movimientos por moneda, tomando "todas las monedas" como valor por defecto. Origen: RF-28.
- FR-06: El sistema debe ofrecer en el filtro por moneda exactamente las monedas del catálogo, más la opción "todas las monedas". Origen: RF-28, RF-32.
- FR-07: El sistema debe permitir modificar la moneda de un movimiento propio ya registrado. Origen: RF-14, RF-24.

## Non-Functional Requirements

- NFR-01: El sistema debe permitir registrar un movimiento sin tocar el campo moneda, de modo que quien opera en una sola moneda no agregue ningún paso respecto de hoy: 0 interacciones adicionales. Origen: RF-25; mitigación del riesgo de fricción de PRD-001.
- NFR-02: La aplicación debe solicitar el catálogo de monedas a lo sumo 1 vez por carga de la pantalla principal, y debe ofrecer el mismo conjunto de monedas en el selector del formulario y en el filtro del listado. Origen: mismo criterio que NFR-02 de `prd-DISC-001-03.md`, para no repetir el defecto de las dos copias del catálogo de categorías.
- NFR-03: El registro de un movimiento debe confirmarse en menos de 1 s en el percentil 95, con el selector de moneda en uso. Origen: RNF-02.

## Acceptance Criteria

- AC-01 (FR-01, FR-04): WHEN el usuario completa un gasto eligiendo dólares como moneda y lo guarda, THE sistema SHALL registrar el movimiento en dólares y SHALL mostrarlo en dólares en el listado.
- AC-02 (FR-02, NFR-01): WHEN el usuario completa monto y categoría sin tocar el campo moneda y guarda, THE sistema SHALL registrar el movimiento en la moneda predeterminada del catálogo, que inicialmente es pesos.
- AC-03 (FR-03): WHEN el usuario abre el formulario de registro, THE sistema SHALL ofrecer en el selector de moneda exactamente las monedas del catálogo, y exactamente una SHALL figurar como predeterminada.
- AC-04 (FR-03, FR-06): WHEN se agrega una moneda al catálogo únicamente como dato, THE sistema SHALL ofrecerla tanto en el selector del formulario como en el filtro del listado, sin que se haya modificado ninguna línea de código.
- AC-05 (FR-04): WHEN existen dos movimientos del mismo monto, uno en pesos y otro en dólares, THE sistema SHALL indicar en cada fila del listado en qué moneda está su monto.
- AC-06 (FR-05): WHEN el usuario filtra el listado por dólares, THE sistema SHALL mostrar únicamente movimientos en dólares.
- AC-07 (FR-05): WHEN el usuario abre el listado sin aplicar filtro de moneda, THE sistema SHALL mostrar los movimientos de todas las monedas.
- AC-08 (FR-05): WHEN el usuario aplica a la vez el filtro de moneda, el de categoría y el de rango de fechas, THE sistema SHALL mostrar únicamente los movimientos que cumplen las tres condiciones.
- AC-09 (FR-07): WHEN el usuario cambia a dólares la moneda de un movimiento propio registrado en pesos y guarda, THE sistema SHALL dejar de sumar su monto en los totales en pesos y SHALL sumarlo en los totales en dólares.
- AC-10 (FR-07): WHEN el usuario cambia la moneda de un movimiento propio, THE sistema SHALL conservar su monto, su categoría y su fecha sin alterarlos.
- AC-11 (FR-01): IF el usuario intenta guardar un movimiento con una moneda que no está en el catálogo, THEN THE sistema SHALL rechazar el guardado, SHALL indicar el motivo y SHALL no crear ningún movimiento.
- AC-12 (NFR-02): WHEN el usuario carga la pantalla principal, THE sistema SHALL solicitar el catálogo de monedas a lo sumo 1 vez, y el selector del formulario y el filtro del listado SHALL ofrecer el mismo conjunto de monedas.
- AC-13 (NFR-03): WHEN se mide el guardado de un movimiento sobre 100 ejecuciones con el selector de moneda en uso, THE sistema SHALL confirmarlo en menos de 1 s en el percentil 95.

## Out of Scope

- **Conversión de divisas, cotizaciones, total consolidado y balance único**: PRD-001 los excluye de forma explícita.
- **El catálogo de monedas, su administración como dato y la separación de los totales por moneda**: son `04a`, del que este ticket depende.
- **Alta, edición y baja de monedas desde la interfaz.**
- **El filtro por moneda del dashboard** (RF-30): es el PRD 05. Este ticket cubre el filtro del listado.
- **Recordar la última moneda elegida** por el usuario como nuevo valor por defecto: el valor por defecto es el del catálogo (RF-25), no un historial.
- **Formato regional del monto según la moneda** (separadores, posición del símbolo): es maquetación, PRD 06.
- **Avisar al usuario cuando registra en una moneda distinta de la habitual** o cualquier heurística que intente adivinar la moneda correcta.

## Risks and Mitigations

- **Riesgo: cargar un movimiento en la moneda equivocada por no mirar el selector.** Es el riesgo que PRD-001 anota para multi-moneda. → Mitigación: las tres que el propio PRD-001 propone, y las tres son requerimientos de este ticket — FR-02 (la predeterminada como valor por defecto), FR-04 (la moneda visible en cada fila) y FR-07 (corregirla sin borrar el movimiento).
- **Riesgo: un campo más en el formulario es fricción para quien usa una sola moneda**, que es la mayoría de los casos. → Mitigación: NFR-01 y AC-02 exigen que se pueda guardar sin tocarlo: 0 interacciones adicionales respecto de hoy.
- **Riesgo: repetir el defecto de las dos copias del catálogo**, esta vez con monedas: el selector y el filtro pidiendo cada uno su lista y pudiendo discrepar. → Mitigación: NFR-02 y AC-12, escritos con el mismo criterio que en el PRD 03. Si los dos tickets se implementan en ese orden, el segundo hereda la solución del primero.
- **Riesgo: el filtro por moneda se combina con los de categoría y fecha, y las combinaciones no se prueban.** Es donde suelen aparecer los `AND` que se pierden. → Mitigación: AC-08 verifica los tres filtros aplicados a la vez.
- **Riesgo: cambiar la moneda de un movimiento mueve su monto entre dos totales y es fácil que se sume en los dos o en ninguno.** → Mitigación: AC-09 verifica las dos direcciones del movimiento, no solo el destino.

## Dependencies

- `DISC-001-04a` mergeado en `main`: el catálogo de monedas contra el que se valida y los totales ya separados por moneda. Sin él, elegir una segunda moneda produce el total mezclado que RF-29 prohíbe.
- FEAT-001a mergeado en `main`: el formulario de registro y el listado sobre los que se agrega el campo y la columna.
- FEAT-001b mergeado en `main`: los filtros del listado, a los que se suma el de moneda, y la modificación de un movimiento propio, de la que depende FR-07.
- FEAT-001c mergeado en `main`: los totales del resumen, sobre los que AC-09 verifica el efecto del cambio de moneda.
- El filtro de tests de rendimiento del CI (`FullyQualifiedName!~Rendimiento`), declarado en la sección Stack de `AGENTS.md`, que AC-13 necesita.
