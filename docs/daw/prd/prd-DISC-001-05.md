# PRD DISC-001-05: Dashboard con gráficos

| Field | Value |
|-------|-------|
| Ticket | DISC-001-05 |
| Tracker | ninguno |
| Date | 2026-08-20 |
| PRD loops | 0 |

> Octavo de los nueve PRDs de **DISC-001** (mapa en `docs/daw/discovery/concept-DISC-001.md`),
> recorte de PRD-001 (`docs/daw/prd/PRD.md`, versión 5). La columna "Origen" de cada requerimiento
> traza al RF del PRD del producto.
>
> Depende de `04a` y `04b`: el dashboard es todo totales, y RF-29 prohíbe que un total mezcle
> monedas.

## Context and Problem

PRD-001 plantea que la aplicación tiene que responder dos preguntas: *cómo vengo este mes* y *en qué
se me va la plata*. FEAT-001c contestó la primera con el resumen del mes, y contestó la segunda **a
medias**: el desglose por categoría existe, pero como una lista de valores. RF-19 pide además que
esos totales se representen gráficamente.

Este ticket construye la sección de dashboard, y su diferencia con el resumen que ya existe no es el
gráfico sino **el período**. El resumen está clavado al mes calendario en curso —es una decisión
explícita de FEAT-001c, con su contra anotada— mientras que el dashboard es el lugar donde el
usuario elige qué mirar: un rango de fechas cualquiera (RF-21) y una moneda o todas (RF-30).

Hay una tensión que este PRD resuelve de forma explícita: el desglose por categoría va a existir en
dos pantallas, calculado con la misma regla. Si cada una lo calcula por su cuenta, en algún momento
van a discrepar y ninguna de las dos va a ser evidentemente la correcta. Por eso NFR-02 exige que
los dos salgan de la misma agregación.

La otra decisión de fondo es qué significa "representado gráficamente". PRD-001 deja el tipo de
gráfico a criterio de diseño, pero no deja abierto que el gráfico sea la **única** forma de leer los
datos: RNF-06 fija un piso de accesibilidad para toda la aplicación, y un valor que solo existe
como un sector de una torta no lo cumple.

## Goals

- Que el usuario vea de un vistazo en qué categorías se le va la plata, en el período que él elija.
- Que pueda mirar un mes anterior, un trimestre o un año sin que el resumen del mes en curso se
  mueva.
- Que los números del dashboard y los del resumen no puedan discrepar.
- Que quien no pueda leer el gráfico igual pueda leer los datos.

## Functional Requirements

- FR-01: El sistema debe mostrar, en una sección de dashboard, el total de gastos agrupado por categoría y por moneda dentro del período seleccionado, representado gráficamente. Origen: RF-19.
- FR-02: El sistema debe mostrar en el dashboard, por cada moneda con movimientos en el período, un balance igual al total de ingresos menos el total de gastos de esa moneda. Origen: RF-20, RF-29.
- FR-03: El sistema debe permitir filtrar los datos del dashboard por rango de fechas, incluidos sus extremos. Origen: RF-21.
- FR-04: El sistema debe permitir filtrar los datos del dashboard por moneda, tomando "todas las monedas" como valor por defecto. Origen: RF-30.
- FR-05: El sistema debe presentar los totales por categoría también en forma textual, de modo que cada valor representado en el gráfico se pueda leer sin interpretarlo. Origen: RNF-06.
- FR-06: El sistema debe indicar en el dashboard que no hay datos cuando el período seleccionado no tiene movimientos, mostrando los totales y el balance en cero para cada moneda y sin mostrar ningún mensaje de error. Origen: AC-31 de PRD-001.
- FR-07: El sistema debe mantener el resumen del mes en curso de la pantalla principal sin alterarlo cuando se cambian los filtros del dashboard. Origen: RF-22, FR-03 de `prd-FEAT-001c.md`.

## Non-Functional Requirements

- NFR-01: El dashboard debe cargar en menos de 2 s en el percentil 95 sobre una cuenta con 1000 movimientos, y en menos de 4 s en el percentil 95 sobre una cuenta con 10000 movimientos. Origen: RNF-01.
- NFR-02: El sistema debe calcular los totales del dashboard mediante agregación en la consulta a la base de datos, con la misma agregación que alimenta el resumen del mes, transfiriendo al frontend a lo sumo 1 fila por cada par de moneda y categoría más los 3 totales de cada moneda, y nunca sumando los montos en el cliente. Origen: RNF-01, y coherencia con NFR-02 de `prd-FEAT-001c.md`.
- NFR-03: El sistema debe distinguir las categorías del gráfico por algún atributo además del color, y debe cumplir contraste AA —4,5:1 en texto normal, 3:1 en texto grande y en componentes de interfaz— en todos los elementos del dashboard. Origen: RNF-06.

## Acceptance Criteria

- AC-01 (FR-01): WHEN una cuenta tiene gastos en varias categorías dentro del período seleccionado, THE sistema SHALL mostrar por cada categoría un total igual a la suma de los montos de sus gastos en ese período y en esa moneda, y SHALL representar esos totales gráficamente.
- AC-02 (FR-02): WHEN una cuenta tiene ingresos y gastos en el período seleccionado, THE sistema SHALL mostrar por cada moneda un balance igual a la suma de sus ingresos menos la suma de sus gastos en esa moneda.
- AC-03 (FR-03): WHEN el usuario selecciona un rango de fechas en el dashboard, THE sistema SHALL calcular los totales por categoría y el balance de cada moneda únicamente con los movimientos cuya fecha cae dentro de ese rango, incluidos sus extremos.
- AC-04 (FR-04): WHEN el usuario filtra el dashboard por dólares, THE sistema SHALL mostrar únicamente los totales por categoría y el balance en dólares; y sin filtro de moneda SHALL mostrar los de todas las monedas.
- AC-05 (FR-01, FR-02): WHEN una cuenta tiene gastos en pesos y en dólares en una misma categoría y período, THE sistema SHALL mostrar el total en pesos y el total en dólares por separado, y ningún total SHALL incluir montos de la otra moneda.
- AC-06 (FR-05): WHEN el usuario recorre el dashboard sin interpretar el gráfico, THE sistema SHALL exponer en forma textual el nombre y el total de cada categoría representada.
- AC-07 (FR-06): IF el período seleccionado no tiene ningún movimiento, THEN THE sistema SHALL mostrar el total de cada moneda y su balance en cero, SHALL indicar que no hay datos para graficar, y SHALL no mostrar ningún mensaje de error.
- AC-08 (FR-07): WHEN el usuario cambia el rango de fechas o la moneda del dashboard, THE sistema SHALL dejar el resumen del mes en curso de la pantalla principal con los mismos totales y el mismo desglose que antes del cambio.
- AC-09 (NFR-02): WHEN se comparan el desglose por categoría del dashboard filtrado por el mes en curso y el desglose del resumen de la pantalla principal, THE sistema SHALL mostrar los mismos totales para las mismas categorías.
- AC-10 (NFR-02): WHEN se inspecciona la respuesta del endpoint que alimenta el dashboard, THE sistema SHALL devolver por cada moneda sus 3 totales ya agregados y a lo sumo 1 fila por categoría, y SHALL no devolver la lista de movimientos individuales.
- AC-11 (NFR-01): WHEN se mide la carga del dashboard sobre 100 ejecuciones en una cuenta con 1000 movimientos, THE sistema SHALL mostrarla en menos de 2 s en el percentil 95.
- AC-12 (NFR-01): WHEN se mide la carga del dashboard sobre 100 ejecuciones en una cuenta con 10000 movimientos, THE sistema SHALL mostrarla en menos de 4 s en el percentil 95.
- AC-13 (NFR-03): WHEN se inspeccionan los elementos del dashboard, THE sistema SHALL exhibir una relación de contraste de al menos 4,5:1 en el texto normal y de al menos 3:1 en el texto grande y en los componentes de interfaz, y SHALL distinguir cada categoría del gráfico por un atributo además del color.

## Out of Scope

- **Conversión de divisas, total consolidado y balance único**: PRD-001 los excluye de forma explícita.
- **Evolución en el tiempo**: gráficos de línea, comparación entre meses o tendencias. RF-19 pide el total por categoría dentro de un período, no su variación.
- **Elegir el tipo de gráfico** o personalizar colores, orden o agrupamiento desde la interfaz.
- **Exportar el dashboard o sus datos** a PDF, Excel o imagen: fuera de alcance en PRD-001.
- **Presupuestos, topes y alertas por categoría**: fuera de alcance en PRD-001.
- **Desglose de ingresos por categoría.** RF-19 pide el desglose de los gastos; el ingreso entra al dashboard por el balance de RF-20.
- **Filtrar el dashboard por categoría**: los filtros del dashboard son el rango de fechas (RF-21) y la moneda (RF-30).
- **Guardar los filtros del dashboard** entre sesiones.
- **Cambiar el resumen del mes en curso** de la pantalla principal: FR-07 exige justamente que no se toque.

## Risks and Mitigations

- **Riesgo: el gráfico exige una dependencia nueva**, y `AGENTS.md` pide justificar toda dependencia en la spec. → Mitigación: la justificación es RF-19, que pide una representación gráfica de forma explícita. Lo que el PLAN tiene que evaluar es el costo: una librería de gráficos suele traer más superficie de la que este dashboard necesita, y un gráfico de barras se puede dibujar con SVG sin dependencia alguna. La decisión se registra como ADR.
- **Riesgo: el desglose por categoría queda calculado en dos lugares y en algún momento discrepan.** → Mitigación: NFR-02 exige la misma agregación para las dos pantallas y AC-09 lo verifica comparando los dos desgloses sobre el mismo período.
- **Riesgo: RNF-01 con 10000 movimientos nunca se midió.** AC-33 de PRD-001 existe desde la primera versión y ningún ticket llegó a ese volumen: FEAT-001a, `b` y `c` midieron con 1000. Es la primera vez que el objetivo de 4 s se pone a prueba, y es donde una agregación que funcionaba puede dejar de funcionar. → Mitigación: AC-12 lo mide, y el PLAN debería atacarlo temprano y no en el último bloque.
- **Riesgo: el gráfico se vuelve la única forma de leer el dato.** Un valor que solo existe como un sector de una torta no cumple RNF-06 y tampoco se puede testear sin inspeccionar píxeles. → Mitigación: FR-05 y AC-06 exigen el equivalente textual, que además es lo que hace verificables a AC-01 y AC-05 sin depender del render del gráfico.
- **Riesgo: distinguir categorías solo por color** deja afuera a quien no distingue esos colores, y con siete categorías predefinidas de gasto más las propias, la paleta se agota rápido. → Mitigación: NFR-03 y AC-13 exigen un atributo además del color.
- **Riesgo: los tests de rendimiento con 10000 movimientos son lentos y miden tiempo de pared**, con la misma sensibilidad al entorno que ya tienen los de FEAT-001. → Mitigación: se nombran siguiendo la convención que el filtro `FullyQualifiedName!~Rendimiento` del CI ya reconoce.

## Dependencies

- `DISC-001-04a` y `DISC-001-04b` mergeados en `main`: el catálogo de monedas y los totales separados por moneda, sin los cuales FR-02, FR-04 y AC-05 no se pueden cumplir sin violar RF-29.
- FEAT-001c mergeado en `main`: el resumen del mes y su consulta agregada, que NFR-02 obliga a compartir y que FR-07 obliga a no alterar.
- `DISC-001-03` (categorías propias), si ya está mergeado: el dashboard tiene que graficar también las categorías propias y las dadas de baja que conservan movimientos. No lo bloquea, pero cambia el conjunto de datos a graficar.
- Una eventual dependencia externa de gráficos, a decidir y justificar en el PLAN.
- El filtro de tests de rendimiento del CI (`FullyQualifiedName!~Rendimiento`), declarado en la sección Stack de `AGENTS.md`, que AC-11 y AC-12 necesitan.
