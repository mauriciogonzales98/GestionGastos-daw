# PRD FEAT-001c: Resumen del mes con desglose por categoría

| Field | Value |
|-------|-------|
| Ticket | FEAT-001c |
| Tracker | ninguno |
| Date | 2026-08-16 |
| PRD loops | 0 |

> Sub-ticket `c` de **FEAT-001** (índice en `docs/daw/prd/prd-FEAT-001.md`), que a su vez es un
> recorte de PRD-001 (`docs/daw/prd/PRD.md`, versión 5). La columna "Origen" de cada requerimiento
> traza al RF del PRD del producto.
>
> Dependencias entre sub-tickets: depende de FEAT-001a (modelo de datos y alta) y de FEAT-001b
> (filtros del listado, sin los cuales FR-03 no es observable). Es el último de los tres.

## Context and Problem

Con FEAT-001a y FEAT-001b la aplicación registra movimientos y los deja consultar acotados por
categoría y por fecha. Falta lo que el PRD-001 plantea como la segunda de las dos preguntas que la
aplicación tiene que responder: *cómo vengo este mes*.

Hoy esa respuesta exige que el usuario lea el listado y sume de cabeza. Este sub-ticket la pone en
pantalla: cuánto entró, cuánto salió, cuál es el balance, y en qué categorías se fue el gasto.

El resumen se calcula **siempre sobre el mes calendario actual**, con independencia de cómo esté
filtrado el listado. Es una decisión tomada de forma explícita y tiene una contra conocida: un
usuario que filtra el listado por marzo y ve el resumen de agosto puede leerlo como un error. La
mitigación es rotular el resumen con el mes al que corresponde. Se registra acá para que quede claro
que se eligió, y no que se pasó por alto.

## Goals

- Que la pantalla principal responda de un vistazo cuánto entró, cuánto salió y cuánto quedó en el
  mes en curso.
- Que el usuario vea en qué categorías se fue el gasto del mes, sin tener que recorrer el listado.
- Que esos números se calculen en la base de datos y no en el navegador, para que sigan siendo
  rápidos cuando el volumen crezca.

## Functional Requirements

- FR-01: El sistema debe mostrar en la pantalla principal un resumen del mes calendario actual con el total ingresado, el total gastado y el balance, calculado como el total ingresado menos el total gastado. Origen: RF-22, RF-20.
- FR-02: El sistema debe mostrar en ese mismo resumen el total de gastos del mes calendario actual desglosado por categoría, como un listado de valores, incluyendo únicamente las categorías con al menos un gasto en el mes. Origen: RF-19 (sin la representación gráfica).
- FR-03: El sistema debe calcular el resumen siempre sobre el mes calendario actual, sin que los filtros de categoría y de fecha aplicados al listado de movimientos lo modifiquen. Origen: RF-22.
- FR-04: El sistema debe rotular el resumen con el mes y el año a los que corresponde. Origen: RF-22.
- FR-05: El sistema debe restringir el cálculo del resumen a los movimientos del usuario actual. Origen: RF-04 (parcial).

## Non-Functional Requirements

- NFR-01: La pantalla principal, con el listado y el resumen, debe cargar en menos de 2 s en el percentil 95 sobre una cuenta con 1000 movimientos registrados. Origen: RNF-01.
- NFR-02: El sistema debe calcular los totales y el desglose mediante agregación en la consulta a la base de datos, transfiriendo al frontend a lo sumo 1 fila por categoría más los 3 totales, y nunca sumando los montos en el cliente. Origen: mitigación del riesgo de RNF-01 en PRD-001.

## Acceptance Criteria

- AC-01 (FR-01): WHEN el usuario entra a la pantalla principal con gastos e ingresos cargados en el mes actual, THE sistema SHALL mostrar el total ingresado como la suma de los montos de los ingresos del mes, el total gastado como la suma de los montos de los gastos del mes, y el balance como el total ingresado menos el total gastado.
- AC-02 (FR-01, FR-02): IF el usuario no tiene ningún movimiento en el mes actual, THEN THE sistema SHALL mostrar el total ingresado, el total gastado y el balance en cero, SHALL indicar que no hay gastos para desglosar, y SHALL no mostrar ningún mensaje de error.
- AC-03 (FR-01): WHEN los gastos del mes superan a los ingresos del mes, THE sistema SHALL mostrar el balance como un valor negativo, distinguible del positivo.
- AC-04 (FR-02): WHEN el usuario entra a la pantalla principal con gastos en varias categorías en el mes actual, THE sistema SHALL mostrar por cada categoría con gastos un total igual a la suma de los montos de sus gastos del mes actual, y SHALL no listar las categorías sin gastos en el mes.
- AC-05 (FR-02): WHEN se suman los totales por categoría del desglose, THE sistema SHALL arrojar exactamente el total gastado que muestra el resumen.
- AC-06 (FR-03): WHEN el usuario filtra el listado por una categoría o por un rango de fechas distinto del mes actual, THE sistema SHALL mantener el resumen calculado sobre el mes calendario actual, sin alterar sus totales ni su desglose.
- AC-07 (FR-03): WHEN el usuario tiene movimientos con fecha anterior o posterior al mes actual, THE sistema SHALL excluir sus montos del total ingresado, del total gastado, del balance y del desglose por categoría.
- AC-08 (FR-03): WHEN el mes actual es el mes en curso y existe un movimiento con fecha igual a su primer día y otro con fecha igual a su último día, THE sistema SHALL incluir los montos de ambos en los totales del resumen.
- AC-09 (FR-04): WHEN el usuario entra a la pantalla principal, THE sistema SHALL mostrar junto al resumen el mes y el año a los que corresponden sus números.
- AC-10 (FR-05): WHEN se calcula el resumen, THE sistema SHALL incluir únicamente movimientos cuyo propietario es el usuario actual, y ninguno de otro propietario.
- AC-11 (NFR-01): WHEN se mide la carga de la pantalla principal sobre 100 ejecuciones en una cuenta con 1000 movimientos, THE sistema SHALL mostrar el listado y el resumen en menos de 2 s en el percentil 95.
- AC-12 (NFR-02): WHEN se inspecciona la respuesta del endpoint de resumen, THE sistema SHALL devolver los 3 totales ya agregados y a lo sumo 1 fila por categoría, y SHALL no devolver la lista de movimientos individuales.

## Out of Scope

- Alta, modificación y eliminación de movimientos, y filtros del listado: son FEAT-001a y FEAT-001b, de los que este sub-ticket depende.
- Representación gráfica del desglose por categoría (RF-19 de PRD-001): el desglose es un listado de valores. El gráfico llega con el ticket del dashboard.
- Dashboard como sección aparte, con filtro de fechas propio (RF-19..RF-21 de PRD-001): el resumen de este sub-ticket vive en la pantalla principal y su período es fijo.
- Resumen de períodos distintos del mes en curso: no hay selector de mes ni comparación entre meses.
- Desglose de ingresos por categoría: FR-02 desglosa únicamente los gastos, que es lo que pide RF-19 de PRD-001.
- Totales separados por moneda (RF-29 de PRD-001): mientras haya una sola moneda no hay nada que separar.
- Presupuestos, topes por categoría y alertas: PRD-001 los declara fuera de alcance.
- Exportación del resumen a PDF o Excel: PRD-001 lo declara fuera de alcance.

## Risks and Mitigations

- Riesgo: el resumen fijo al mes actual (FR-03) leyéndose como un error cuando el usuario filtra el listado por otro rango y los números no acompañan → mitigación: FR-04 exige rotular el resumen con su mes, y AC-09 lo verifica. Si en uso sigue resultando confuso, cambiarlo a que siga al filtro es un ticket chico, no un rediseño.
- Riesgo: calcular los totales trayendo todos los movimientos al frontend, que es la forma más directa de incumplir NFR-01 cuando el volumen crece → mitigación: NFR-02 lo prohíbe, AC-12 lo verifica sobre la respuesta del endpoint y AC-11 lo mide.
- Riesgo: el corte del mes desplazándose por zona horaria y dejando el movimiento del día 1 o del día 31 fuera del resumen → mitigación: AC-08 verifica explícitamente los dos días extremos del mes.
- Riesgo: el desglose por categoría y el total gastado calculándose con consultas distintas que se desincronizan, mostrando un desglose que no suma el total → mitigación: AC-05 verifica la identidad entre la suma del desglose y el total gastado.
- Riesgo: sumar en un tipo de punto flotante al agregar en la consulta y arrastrar centavos de error a los tres totales → mitigación: los montos se persisten en decimal exacto por NFR-03 de FEAT-001a, y la agregación conserva ese tipo.
- Riesgo: mostrar el balance negativo sin distinguirlo del positivo y que el usuario lea un déficit como superávit → mitigación: AC-03 exige que sea distinguible.

## Dependencies

- FEAT-001a (`docs/daw/prd/prd-FEAT-001a.md`): entrega el modelo de datos, la abstracción de usuario actual sobre la que se apoya FR-05, el catálogo de categorías del desglose y el tipo decimal exacto de los montos.
- FEAT-001b (`docs/daw/prd/prd-FEAT-001b.md`): entrega los filtros del listado, sin los cuales FR-03 y AC-06 no son observables. Este sub-ticket no puede cerrar antes de que `b` cierre.
- Base de datos MySQL 8.4.5, schema `gestiongastos`, y sus funciones de agregación, sobre las que NFR-02 apoya el cálculo de los totales (declarada en `AGENTS.md`, sección Stack).
- Entity Framework Core 9.0.18 con Pomelo.MySQL 9.0.0 para traducir la agregación de NFR-02 a SQL.
- Backend .NET 10 exponiendo la API HTTP que consume el frontend React 19 + Vite; ambos declarados en `AGENTS.md`, sección Stack.
- Ticket posterior de autenticación: reemplazará la abstracción de usuario actual sobre la que se apoya FR-05.
- PRD padre FEAT-001 (`docs/daw/prd/prd-FEAT-001.md`) y PRD-001 (`docs/daw/prd/PRD.md`), de los que este documento es la tercera parte.
