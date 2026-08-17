# PRD FEAT-001a: Alta de movimientos y listado simple

| Field | Value |
|-------|-------|
| Ticket | FEAT-001a |
| Tracker | ninguno |
| Date | 2026-08-16 |
| PRD loops | 0 |

> Sub-ticket `a` de **FEAT-001** (índice en `docs/daw/prd/prd-FEAT-001.md`), que a su vez es un
> recorte de PRD-001 (`docs/daw/prd/PRD.md`, versión 5). La columna "Origen" de cada requerimiento
> traza al RF del PRD del producto.
>
> Dependencias entre sub-tickets: `b` y `c` dependen de este. Este no depende de ninguno.

## Context and Problem

El repositorio no tiene código: solo el scaffolding del método y los PRD. La persona del PRD-001 —
alguien que quiere controlar sus gastos y hoy usa o abandonó una planilla— no puede hacer todavía lo
más elemental: anotar que gastó plata y ver que quedó anotada.

Este sub-ticket construye ese camino mínimo end-to-end y, con él, todo el arranque técnico del
proyecto: la solución .NET, el acceso a datos, la migración inicial con las tres tablas, la semilla
de categorías y el frontend. Es el único de los tres sub-tickets que define el modelo de datos, y por
eso va primero: los otros dos se apoyan en él.

Se construye **sin autenticación**, que es un ticket posterior. La consecuencia se asume de forma
explícita: el modelo lleva la pertenencia al usuario desde el día uno (FR-01), pero el usuario es uno
solo y fijo, sembrado en la base. El aislamiento real entre varios usuarios (AC-06 a AC-08 de
PRD-001) no se puede verificar acá y queda como criterio del ticket de autenticación.

El listado que entrega este sub-ticket es sin filtros: muestra todos los movimientos del usuario. Los
filtros por categoría y por rango de fechas son FEAT-001b.

## Goals

- Que un gasto o un ingreso se pueda cargar desde un formulario con monto, categoría y fecha, y nada
  más obligatorio.
- Que lo cargado se vea inmediatamente en un listado, con los datos suficientes para reconocerlo.
- Que el modelo de datos quede preparado para autenticación y para multi-moneda sin necesidad de una
  migración con datos adentro.
- Que las validaciones de monto, categoría y nota vivan en el servidor, y no solamente en el
  formulario.

## Functional Requirements

- FR-01: El sistema debe asociar todo movimiento a un usuario propietario y debe restringir toda consulta de movimientos a los del usuario actual. Origen: RF-04 (parcial). En este ticket el usuario actual es una fila semilla fija, provista por una única abstracción que el ticket de autenticación reemplazará por la sesión.
- FR-02: El sistema debe ofrecer un catálogo de categorías predefinidas, no modificable por el usuario, cada una con un tipo que es gasto o ingreso. Origen: RF-06. El catálogo se siembra como dato: gastos — Comida, Transporte, Vivienda, Servicios, Salud, Ocio, Otros; ingresos — Sueldo, Ingreso extra, Otros.
- FR-03: El sistema debe permitir registrar un gasto mediante un formulario, indicando monto, categoría y fecha. Origen: RF-10.
- FR-04: El sistema debe permitir registrar un ingreso mediante un formulario, indicando monto, categoría y fecha. Origen: RF-11.
- FR-05: El sistema debe proponer la fecha del día actual como valor por defecto del campo fecha. Origen: RF-12.
- FR-06: El sistema debe rechazar el registro de un movimiento cuyo monto no sea un número mayor a cero con hasta dos decimales. Origen: RF-13.
- FR-07: El sistema debe rechazar el registro de un movimiento que no tenga categoría asignada, y debe rechazar el registro de un gasto con una categoría de tipo ingreso y viceversa. Origen: RF-23.
- FR-08: El sistema debe permitir asociar a cada movimiento una nota descriptiva opcional de texto libre de hasta 120 caracteres, y debe rechazar el registro de un movimiento cuya nota supere ese largo. Origen: RF-33.
- FR-09: El sistema debe registrar todo movimiento en pesos argentinos (ARS) y debe persistir la moneda como un dato del movimiento, no como una constante del código. Origen: RF-24 (reducido a una sola moneda).
- FR-10: El sistema debe listar los movimientos del usuario actual, gastos e ingresos juntos, ordenados por fecha de forma descendente. Origen: RF-16.
- FR-11: El sistema debe mostrar en cada fila del listado la fecha, el tipo, la categoría, el monto con su moneda y la nota del movimiento. Origen: RF-16, RF-27, RF-33.

## Non-Functional Requirements

- NFR-01: El guardado de un movimiento debe confirmarse en menos de 1 s en el percentil 95. Origen: RNF-02.
- NFR-02: El formulario de registro debe poder completarse y enviarse íntegramente con el teclado; todo control interactivo debe tener foco visible y una etiqueta asociada; el texto y los controles deben cumplir un contraste de 4.5:1 en texto normal y 3:1 en texto grande y en componentes de interfaz. Origen: RNF-06.
- NFR-03: El sistema debe persistir los montos en un tipo decimal exacto de 2 decimales, nunca en punto flotante, con un máximo de 13 dígitos enteros.

## Acceptance Criteria

- AC-01 (FR-01): WHEN se registra un movimiento, THE sistema SHALL persistirlo con el identificador del usuario actual como propietario.
- AC-02 (FR-01): WHEN se consulta el listado, THE sistema SHALL devolver únicamente movimientos cuyo propietario es el usuario actual, y ninguno de otro propietario.
- AC-03 (FR-02): WHEN el usuario abre el formulario para cargar un gasto, THE sistema SHALL ofrecer en el selector de categoría las 7 categorías predefinidas de tipo gasto y ninguna de tipo ingreso.
- AC-04 (FR-02): WHEN el usuario abre el formulario para cargar un ingreso, THE sistema SHALL ofrecer en el selector de categoría las 3 categorías predefinidas de tipo ingreso y ninguna de tipo gasto.
- AC-05 (FR-03): WHEN el usuario completa monto, categoría y fecha de un gasto y confirma el guardado, THE sistema SHALL crear el movimiento como gasto y mostrarlo en el listado.
- AC-06 (FR-04): WHEN el usuario completa monto, categoría y fecha de un ingreso y confirma el guardado, THE sistema SHALL crear el movimiento como ingreso y mostrarlo en el listado.
- AC-07 (FR-05): WHEN el usuario abre el formulario de registro, THE sistema SHALL presentar el campo fecha con la fecha del día actual ya cargada.
- AC-08 (FR-06): IF el monto está vacío, no es numérico, es menor o igual a cero, o tiene más de dos decimales, THEN THE sistema SHALL rechazar el guardado, mostrar el motivo junto al campo monto y no crear ningún movimiento.
- AC-09 (FR-07): IF el usuario intenta guardar un movimiento sin categoría seleccionada, THEN THE sistema SHALL rechazar el guardado, mostrar el motivo y no crear ningún movimiento.
- AC-10 (FR-07): IF el usuario intenta guardar un gasto con una categoría de tipo ingreso, o un ingreso con una categoría de tipo gasto, THEN THE sistema SHALL rechazar el guardado con un error de validación y no crear ningún movimiento.
- AC-11 (FR-08): WHEN el usuario guarda un movimiento con una nota de 120 caracteres o menos, THE sistema SHALL registrar el movimiento con esa nota y mostrarla en su fila del listado.
- AC-12 (FR-08): WHEN el usuario guarda un movimiento dejando la nota vacía, THE sistema SHALL registrar el movimiento sin nota y mostrar su fila en el listado sin error y sin texto de relleno.
- AC-13 (FR-08): IF la nota supera los 120 caracteres, THEN THE sistema SHALL rechazar el guardado, mostrar el motivo y no crear ningún movimiento.
- AC-14 (FR-09): WHEN se registra un movimiento, THE sistema SHALL persistir "ARS" como su moneda, y el listado SHALL mostrar el monto acompañado de esa moneda.
- AC-15 (FR-10): WHEN el usuario abre el listado con gastos e ingresos cargados, THE sistema SHALL mostrar todos esos movimientos, de ambos tipos, ordenados de la fecha más reciente a la más antigua.
- AC-16 (FR-11): WHEN el listado muestra un movimiento, THE sistema SHALL presentar en su fila la fecha, el tipo, el nombre de la categoría, el monto con su moneda y la nota.
- AC-17 (NFR-01): WHEN se mide el guardado de un movimiento válido sobre 100 ejecuciones, THE sistema SHALL confirmar la operación en menos de 1 s en el percentil 95.
- AC-18 (NFR-02): WHEN se recorre y se envía el formulario de registro usando únicamente el teclado, THE sistema SHALL permitir completar y guardar un movimiento, y cada control recorrido SHALL mostrar foco visible y tener una etiqueta asociada.
- AC-19 (NFR-03): WHEN se registra un movimiento de monto 0.10 y otro de monto 0.20, THE sistema SHALL persistirlos de forma exacta y devolver 0.30 como su suma, sin error de redondeo.

## Out of Scope

- Filtrado del listado por categoría o por rango de fechas: es FEAT-001b. El listado de este sub-ticket devuelve todos los movimientos del usuario.
- Modificación y eliminación de movimientos ya registrados: es FEAT-001b.
- Resumen del mes, balance y desglose de gastos por categoría: es FEAT-001c.
- Autenticación, registro de cuentas, login y logout (RF-01..RF-05 de PRD-001). El usuario es uno solo y fijo.
- Aislamiento verificable entre varios usuarios (AC-06..AC-08 de PRD-001): no hay dos usuarios que aislar. FR-01 deja la columna y el filtro; el ticket de autenticación cierra esos criterios.
- Alta, modificación y baja lógica de categorías propias (RF-07..RF-09 de PRD-001). El catálogo es el sembrado, y no hay pantalla para gestionarlo.
- Dashboard con representación gráfica y filtros propios (RF-19..RF-21 de PRD-001).
- Soporte de varias monedas: catálogo, selector, filtro y totales separados (RF-24..RF-32 de PRD-001). FR-09 persiste la moneda como dato para que agregarlas después no requiera migrar datos.
- Paginación y búsqueda del listado.
- Todo lo que PRD-001 ya declara fuera de alcance: cuentas compartidas, conexión con bancos, notificaciones, exportación, presupuestos, carga por voz o imagen, recurrencias, adjuntos y etiquetas.

## Risks and Mitigations

- Riesgo: construir sin autenticación y descubrir después que la pertenencia al usuario quedó dispersa por las consultas, de modo que enchufar la sesión obligue a repasar cada una → mitigación: FR-01 concentra la obtención del usuario actual en una única abstracción, y el ticket de autenticación cambia su implementación sin tocar endpoints ni consultas.
- Riesgo: sembrar el usuario fijo como un valor en el código y que quede ahí cuando llegue la autenticación → mitigación: el usuario semilla es una fila de la tabla de usuarios, no una constante, y AC-01 verifica la persistencia del propietario.
- Riesgo: persistir los montos en punto flotante y arrastrar errores de redondeo a todos los totales que construyen los sub-tickets siguientes → mitigación: NFR-03 exige decimal exacto y AC-19 lo verifica sobre el caso clásico 0.10 + 0.20.
- Riesgo: este sub-ticket fija el esquema del que dependen `b` y `c`, y un error acá se descubre con el triple de código escrito encima → mitigación: es exactamente la razón por la que va primero y solo; la spec de PLAN define el modelo de datos completo antes de escribir el primer endpoint.
- Riesgo: las fechas cruzando zona horaria entre el navegador, la API y MySQL, que desplazan un movimiento de día → mitigación: la fecha del movimiento es una fecha sin hora, sin conversión de zona horaria en ninguna de las tres capas.
- Riesgo: validar el monto, la categoría y la nota solo en el formulario, dejando la API abierta a datos inválidos → mitigación: AC-08 a AC-13 se verifican contra la API, no contra el formulario.

## Dependencies

- Base de datos MySQL 8.4.5 local, schema `gestiongastos`, para persistir usuarios, categorías y movimientos (declarada en `AGENTS.md`, sección Stack).
- Entity Framework Core 9.0.18 con Pomelo.MySQL 9.0.0 para el acceso a datos, y la migración inicial que crea las tres tablas y siembra las categorías de FR-02 y el usuario de FR-01.
- Backend .NET 10 exponiendo la API HTTP que consume el frontend React 19 + Vite; ambos declarados en `AGENTS.md`, sección Stack.
- La cadena de conexión a MySQL, provista por user-secrets según la convención de `AGENTS.md`; nunca en `appsettings`.
- Ticket posterior de autenticación: consumirá la abstracción de usuario actual que deja FR-01 y cerrará los criterios AC-06..AC-08 de PRD-001 que este sub-ticket no puede verificar.
- PRD padre FEAT-001 (`docs/daw/prd/prd-FEAT-001.md`) y PRD-001 (`docs/daw/prd/PRD.md`), de los que este documento es la primera parte.
