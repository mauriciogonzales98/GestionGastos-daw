# PRD DISC-001-02: Nota descriptiva del movimiento

| Field | Value |
|-------|-------|
| Ticket | DISC-001-02 |
| Tracker | ninguno |
| Date | 2026-08-20 |
| PRD loops | 0 |

> Cuarto de los ocho PRDs de **DISC-001** (mapa en `docs/daw/discovery/concept-DISC-001.md`),
> recorte de PRD-001 (`docs/daw/prd/PRD.md`, versión 5). La columna "Origen" de cada requerimiento
> traza al RF del PRD del producto.
>
> **No tiene dependencias con los demás PRDs de DISC-001 y ninguno depende de él.** Es el ticket
> que entra en cualquier hueco del plan, incluso en paralelo en otro `git worktree`.

## Context and Problem

La categoría dice de qué tipo es un gasto; no dice cuál fue. "Transporte, $8.500" no distingue el
viaje al aeropuerto de la carga de la SUBE, y a los tres días el usuario ya no se acuerda. PRD-001
lo resuelve en RF-33 con una nota descriptiva opcional: texto libre, hasta 120 caracteres, visible
en el listado.

La decisión de producto importante ya está tomada en PRD-001 y este PRD la respeta sin discutirla:
**la nota es descriptiva, no clasificatoria**. No se busca, no se filtra, no se agrupa y no aparece
en ningún total. El riesgo que motiva esa restricción está escrito en el propio PRD-001 — una nota
libre que se pudiera filtrar se convierte en una segunda taxonomía informal ("alquiler", "Alquiler",
"alq") que el sistema no entiende y que da una falsa sensación de estar clasificando. La categoría
sigue siendo el único eje de análisis.

También hay que respetar lo que hace la nota **al camino rápido de carga**, que es la fricción que
el producto entero intenta evitar: la nota es el único campo libre del formulario y por eso es
opcional. El usuario nunca tiene que escribir nada para registrar un movimiento.

## Goals

- Que el usuario pueda anotar en qué gastó, más allá de la categoría, sin abrir una segunda
  taxonomía.
- Que anotarlo sea siempre opcional y no agregue un paso al camino rápido de carga.
- Que lo anotado se lea en el listado, junto al movimiento al que corresponde.

## Functional Requirements

- FR-01: El sistema debe permitir asociar a cada movimiento una nota descriptiva de texto libre de hasta 120 caracteres al registrarlo, dejando el campo opcional. Origen: RF-33.
- FR-02: El sistema debe mostrar la nota de cada movimiento en el listado, junto al movimiento al que pertenece. Origen: RF-33.
- FR-03: El sistema debe rechazar el registro y la modificación de un movimiento cuya nota supere los 120 caracteres, indicando el motivo y sin crear ni alterar ningún movimiento. Origen: RF-33.
- FR-04: El sistema debe permitir modificar y vaciar la nota de un movimiento propio ya registrado. Origen: RF-33, RF-14.
- FR-05: El sistema debe registrar el movimiento sin nota cuando el campo se envía vacío, y debe mostrarlo en el listado sin texto de relleno y sin error. Origen: RF-33.

## Non-Functional Requirements

- NFR-01: El sistema debe mostrar la nota como texto plano en el 100 % de los casos, sin interpretar ninguna de sus secuencias como marcado y sin ejecutar contenido a partir de ella. Origen: seguridad; la nota es la única entrada de texto libre de la aplicación.
- NFR-02: El sistema debe dejar sin variación los totales, el balance y el desglose por categoría del resumen ante cualquier valor de la nota, incluido el vacío. Origen: RF-33 ("la nota no clasifica ni agrupa").
- NFR-03: El listado debe seguir cargando en menos de 2 s en el percentil 95 sobre una cuenta con 1000 movimientos, con la nota incluida en cada fila. Origen: RNF-01.

## Acceptance Criteria

- AC-01 (FR-01, FR-02): WHEN el usuario completa monto, categoría y una nota y guarda, THE sistema SHALL registrar el movimiento con esa nota y SHALL mostrarla en el listado junto a ese movimiento.
- AC-02 (FR-05): WHEN el usuario guarda un movimiento con el campo de nota vacío, THE sistema SHALL registrar el movimiento sin nota y SHALL mostrarlo en el listado sin texto de relleno y sin error.
- AC-03 (FR-03): IF la nota enviada supera los 120 caracteres, THEN THE sistema SHALL rechazar el guardado, SHALL indicar el motivo, y SHALL no crear ni modificar ningún movimiento.
- AC-04 (FR-01, FR-03): WHEN el usuario guarda un movimiento con una nota de exactamente 120 caracteres, THE sistema SHALL aceptarlo y SHALL mostrar los 120 caracteres en el listado.
- AC-05 (FR-04): WHEN el usuario modifica la nota de un movimiento propio y guarda, THE sistema SHALL mostrar en el listado el valor nuevo y SHALL no mostrar el anterior.
- AC-06 (FR-04, FR-05): WHEN el usuario borra por completo la nota de un movimiento propio que la tenía y guarda, THE sistema SHALL dejar ese movimiento sin nota y SHALL mostrarlo en el listado sin texto de relleno.
- AC-07 (NFR-02): WHEN el usuario agrega, modifica o borra la nota de un movimiento, THE sistema SHALL dejar el total ingresado, el total gastado, el balance y el desglose por categoría con los mismos valores que antes de la operación.
- AC-08 (NFR-01): IF la nota contiene una secuencia con forma de marcado o de guion, THEN THE sistema SHALL mostrarla en el listado como los mismos caracteres que el usuario escribió y SHALL no ejecutar ni interpretar nada a partir de ella.
- AC-09 (FR-01): WHEN el usuario abre el formulario de registro, THE sistema SHALL presentar el campo de nota como opcional y SHALL permitir guardar el movimiento sin haberlo tocado.
- AC-10 (NFR-03): WHEN se mide la carga del listado sobre 100 ejecuciones en una cuenta con 1000 movimientos que tienen nota, THE sistema SHALL mostrarlo en menos de 2 s en el percentil 95.

## Out of Scope

- **Buscar, filtrar, agrupar o totalizar por la nota.** PRD-001 lo deja fuera de alcance de forma explícita, y es la restricción que impide que la nota se convierta en una segunda taxonomía. Si aparece la necesidad real de totalizar por algo más fino que la categoría, se resuelve con un catálogo de etiquetas, no estirando la nota.
- **Etiquetas reutilizables** sobre los movimientos: una segunda dimensión de clasificación además de la categoría.
- **Mostrar la nota en el resumen o en el dashboard.** La nota se lee en el listado; los totales no la tocan.
- **Formato dentro de la nota** (negrita, saltos de línea con significado, markdown, enlaces que se puedan seguir).
- **Autocompletado o sugerencias** a partir de notas anteriores: sería la puerta de atrás a la taxonomía informal que RF-33 evita.
- **Ampliar el límite de 120 caracteres** o hacerlo configurable.
- **Adjuntar comprobantes o archivos** al movimiento: fuera de alcance en PRD-001.

## Risks and Mitigations

- **Riesgo: la nota se convierte en una segunda taxonomía informal.** El usuario empieza a escribir "alquiler" en todos los movimientos de alquiler y espera poder agruparlos. → Mitigación: la restricción de PRD-001, respetada en Out of Scope — no se busca, no se filtra, no se agrupa. El límite de 120 caracteres y la ausencia de autocompletado empujan en la misma dirección.
- **Riesgo: es la única entrada de texto libre de la aplicación, y va a parar al listado.** Es el lugar natural de una inyección en la pantalla. → Mitigación: NFR-01 y AC-08 lo verifican explícitamente. React escapa por defecto, lo que hace que el riesgo real sea que alguien lo desactive a propósito para "que se vea mejor"; el criterio existe para que eso rompa un test.
- **Riesgo: un campo más en el formulario es más fricción, que es lo que el producto combate.** → Mitigación: AC-09 exige que el campo sea opcional y que se pueda guardar sin tocarlo. La nota nunca está en el camino rápido.
- **Riesgo: 120 caracteres es un número arbitrario** y va a quedar corto para alguien. → Mitigación: viene de RF-33 y se respeta; ampliarlo es una modificación de PRD-001, no una decisión de este ticket. AC-04 fija el borde exacto para que el límite no se corra por descuido.
- **Riesgo: agregar una columna de texto a la tabla de movimientos toca la consulta del listado**, que tiene un presupuesto de tiempo. → Mitigación: NFR-03 y AC-10 lo miden sobre 1000 movimientos con nota.

## Dependencies

- FEAT-001a mergeado en `main`: el modelo de movimientos, el formulario de registro y el listado sobre los que se agrega el campo.
- FEAT-001b mergeado en `main`: la modificación de un movimiento propio, de la que depende FR-04.
- MySQL 8.4.10 y una migración que agregue la columna de la nota a la tabla de movimientos.
- El filtro de tests de rendimiento del CI (`FullyQualifiedName!~Rendimiento`), declarado en la sección Stack de `AGENTS.md`, que AC-10 necesita para no dar rojos sin significado en un runner compartido.
