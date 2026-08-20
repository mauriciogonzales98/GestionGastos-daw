# PRD DISC-001-04a: Catálogo de monedas y totales por moneda

| Field | Value |
|-------|-------|
| Ticket | DISC-001-04a |
| Tracker | ninguno |
| Date | 2026-08-20 |
| PRD loops | 0 |

> Sexto de los nueve PRDs de **DISC-001** (mapa en `docs/daw/discovery/concept-DISC-001.md`),
> recorte de PRD-001 (`docs/daw/prd/PRD.md`, versión 5). La columna "Origen" de cada requerimiento
> traza al RF del PRD del producto.
>
> Primero de los dos cortes de multi-moneda. **`04a` (este)** introduce el catálogo y enseña a los
> totales a separar por moneda, sin que el usuario pueda todavía elegir una; **`04b`** abre el
> selector, la columna del listado y el filtro. El orden es deliberado y se explica abajo.

## Context and Problem

FEAT-001a tomó una decisión que hoy vuelve barato este ticket: **la moneda se persiste como dato del
movimiento y no como constante del código**. Cada fila de `movimientos` tiene su columna `moneda`,
un `char(3)` con `"ARS"` por defecto. Lo que falta no es la columna: es todo lo demás.

Hoy `"ARS"` es una constante en `Common/Moneda.cs`. No hay catálogo, no hay forma de agregar una
moneda sin tocar código, nada valida contra qué se compara, y —lo más importante— **los totales
suman todo junto**. El resumen de FEAT-001c agrega montos sin mirar la moneda, cosa que hoy es
correcta porque solo existe una.

RF-29 de PRD-001 es la regla dura de este bloque: *todo total, subtotal y balance debe sumar
únicamente montos de una misma moneda*. Un total que mezcle pesos con dólares no es un número
aproximado, es un número **sin significado**.

**De ahí sale el orden de los dos tickets.** Si `04b` fuera primero, el usuario podría registrar un
gasto en dólares mientras el resumen sigue sumando todo junto, y la aplicación mostraría durante un
ticket entero exactamente el número que RF-29 prohíbe. Al revés no pasa nada: este ticket enseña a
los totales a separar por moneda cuando **todavía hay una sola**, de modo que su salida es idéntica
a la de hoy y no se rompe nada visible. La segunda moneda recién se vuelve alcanzable en `04b`,
cuando los totales ya saben qué hacer con ella.

## Goals

- Que las monedas sean un dato administrable y no una constante repartida por el código.
- Que sumar una moneda nueva no requiera tocar la aplicación.
- Que ningún total, subtotal ni balance pueda mezclar montos de monedas distintas, ni siquiera por
  descuido.

## Functional Requirements

- FR-01: El sistema debe ofrecer un catálogo de monedas no modificable por el usuario, que contiene inicialmente pesos y dólares. Origen: RF-31.
- FR-02: El sistema debe marcar en el catálogo exactamente una moneda como predeterminada, que inicialmente es pesos. Origen: RF-25, RF-31.
- FR-03: El sistema debe permitir sumar una moneda al catálogo modificando únicamente datos, sin modificar el código de la aplicación. Origen: RF-32.
- FR-04: El sistema debe rechazar el registro y la modificación de un movimiento cuya moneda no esté en el catálogo, indicando el motivo y sin crear ni alterar ningún movimiento. Origen: RF-26.
- FR-05: El sistema debe calcular todo total, subtotal y balance sumando únicamente montos de una misma moneda. Origen: RF-29.
- FR-06: El sistema debe devolver el resumen del mes como un conjunto de totales por cada moneda con movimientos en el período, cada uno con su total ingresado, su total gastado, su balance y su desglose por categoría. Origen: RF-20, RF-22, RF-29.
- FR-07: El sistema debe dejar, mediante la migración que introduce el catálogo, todos los movimientos ya registrados con la moneda predeterminada del catálogo. Origen: RF-24; los movimientos existentes se cargaron cuando la moneda era una constante.

## Non-Functional Requirements

- NFR-01: El sistema debe permitir agregar una moneda al catálogo con 0 líneas de código modificadas y 0 recompilaciones de la aplicación. Origen: RF-32.
- NFR-02: El sistema debe calcular los totales por moneda mediante agregación en la consulta a la base de datos, transfiriendo al frontend a lo sumo 1 fila por cada par de moneda y categoría más los 3 totales de cada moneda, y nunca sumando los montos en el cliente. Origen: NFR-02 de `prd-FEAT-001c.md`, que este PRD extiende.
- NFR-03: La pantalla principal debe seguir cargando en menos de 2 s en el percentil 95 sobre una cuenta con 1000 movimientos repartidos en 2 monedas. Origen: RNF-01.

## Acceptance Criteria

- AC-01 (FR-01, FR-02): WHEN se consulta el catálogo de monedas de una instalación recién migrada, THE sistema SHALL devolver pesos y dólares, y SHALL marcar exactamente una de las dos como predeterminada.
- AC-02 (FR-03, NFR-01): WHEN se agrega una moneda al catálogo únicamente como dato y se registra un movimiento con ella, THE sistema SHALL aceptar el movimiento sin que se haya modificado ninguna línea de código ni recompilado la aplicación.
- AC-03 (FR-04): IF se envía un movimiento cuya moneda no está en el catálogo, THEN THE sistema SHALL rechazar el guardado, SHALL indicar el motivo y SHALL no crear ningún movimiento.
- AC-04 (FR-04): IF se modifica un movimiento existente indicando una moneda que no está en el catálogo, THEN THE sistema SHALL rechazar la modificación y SHALL dejar ese movimiento con su moneda y su monto anteriores.
- AC-05 (FR-05, FR-06): WHEN una cuenta tiene gastos en pesos y en dólares en una misma categoría y período, THE sistema SHALL devolver para esa categoría un total en pesos igual a la suma de los gastos en pesos y un total en dólares igual a la suma de los gastos en dólares, y ningún total SHALL incluir montos de la otra moneda.
- AC-06 (FR-06): WHEN una cuenta tiene ingresos y gastos en las dos monedas dentro del período, THE sistema SHALL devolver un balance en pesos igual a los ingresos en pesos menos los gastos en pesos, y un balance en dólares igual a los ingresos en dólares menos los gastos en dólares.
- AC-07 (FR-06): WHEN una cuenta no tiene ningún movimiento en una moneda del catálogo dentro del período, THE sistema SHALL no devolver totales para esa moneda y SHALL no mostrar ningún mensaje de error.
- AC-08 (FR-06): IF una cuenta no tiene ningún movimiento en el período, THEN THE sistema SHALL devolver el resumen sin totales de ninguna moneda y SHALL no mostrar ningún mensaje de error.
- AC-09 (FR-07): WHEN se aplica la migración que introduce el catálogo sobre una base con movimientos ya registrados, THE sistema SHALL dejar cada uno de esos movimientos con la moneda predeterminada del catálogo y SHALL no dejar ningún movimiento con una moneda ausente del catálogo.
- AC-10 (FR-05, FR-06): WHEN existe una sola moneda con movimientos en el período, THE sistema SHALL devolver los mismos totales, el mismo balance y el mismo desglose que devolvía antes de este ticket.
- AC-11 (NFR-02): WHEN se inspecciona la respuesta del endpoint de resumen, THE sistema SHALL devolver por cada moneda sus 3 totales ya agregados y a lo sumo 1 fila por categoría, y SHALL no devolver la lista de movimientos individuales.
- AC-12 (NFR-03): WHEN se mide la carga de la pantalla principal sobre 100 ejecuciones en una cuenta con 1000 movimientos repartidos en 2 monedas, THE sistema SHALL mostrarla en menos de 2 s en el percentil 95.

## Out of Scope

- **Conversión de divisas.** PRD-001 lo excluye de forma explícita: no hay cotización, ni total consolidado, ni balance único. Los montos de cada moneda se suman y se muestran por separado.
- **El selector de moneda del formulario, la moneda en cada fila del listado y el filtro por moneda**: son `04b`. Este ticket no le da al usuario ninguna forma de elegir una moneda distinta de la predeterminada.
- **Alta, edición y baja de monedas desde la interfaz**: PRD-001 lo deja fuera; el catálogo se administra como dato.
- **Eliminar una moneda del catálogo** o marcar una moneda distinta como predeterminada una vez que hay movimientos registrados con la anterior.
- **Formato regional del monto por moneda** (separadores, posición del símbolo): es maquetación, y va en el PRD 06.
- **El dashboard con gráficos y su filtro por moneda** (RF-30): es el PRD 05, que depende de este.

## Risks and Mitigations

- **Riesgo: este ticket no cambia nada visible, y eso lo vuelve fácil de saltear o de dar por hecho.** Su entregable es una propiedad que solo se nota cuando aparece la segunda moneda. → Mitigación: AC-10 fija el criterio de que la salida no cambie con una sola moneda, y AC-05 y AC-06 verifican la separación cargando datos en dos monedas aunque el usuario todavía no pueda elegirlas desde la pantalla.
- **Riesgo: un total que mezcla monedas no se ve mal, se ve plausible.** Sumar 10.000 pesos con 50 dólares da 10.050, un número que nadie mira dos veces. → Mitigación: es exactamente lo que AC-05 y AC-06 verifican, y la razón por la que este ticket va antes que `04b`.
- **Riesgo: el resumen pasa de devolver un objeto a devolver una colección**, lo que rompe el contrato que el frontend de FEAT-001c ya consume. → Mitigación: es un cambio de contrato deliberado y está en FR-06; el PLAN tiene que tratarlo como tal. Vale recordar que la suite de Vitest no corre `typecheck`, así que un frontend desalineado con el DTO nuevo puede quedar verde y aparecer como `undefined` en pantalla — es la deuda D-2, que por el orden acordado ya estará saldada cuando llegue este ticket.
- **Riesgo: la agregación por moneda multiplica las filas del desglose** —una por par de moneda y categoría— y puede degradar la consulta. → Mitigación: NFR-02 y NFR-03 lo acotan y AC-12 lo mide sobre 1000 movimientos en dos monedas.
- **Riesgo: "sumar una moneda sin tocar el código" es fácil de creer y difícil de comprobar.** → Mitigación: AC-02 lo verifica de punta a punta agregando una moneda tercera como dato y registrando un movimiento con ella.

## Dependencies

- FEAT-001a mergeado en `main`: la columna `moneda` del movimiento, que este ticket valida contra el catálogo en lugar de contra una constante.
- FEAT-001c mergeado en `main`: el resumen del mes con desglose por categoría, cuyo contrato y cuya consulta agregada este ticket reescribe para separar por moneda.
- La constante `Common/Moneda.cs`, que deja de ser la fuente de verdad de las monedas válidas.
- MySQL 8.4.10 y una migración que cree el catálogo, lo siembre con pesos y dólares y normalice la moneda de los movimientos ya registrados.
- **D-2 (Vitest sin `typecheck`)**, saldada antes por el orden acordado: sin ella, el cambio de contrato del resumen puede pasar la suite y romper la pantalla.
- El filtro de tests de rendimiento del CI (`FullyQualifiedName!~Rendimiento`), declarado en la sección Stack de `AGENTS.md`, que AC-12 necesita.
