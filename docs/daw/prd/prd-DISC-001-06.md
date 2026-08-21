# PRD DISC-001-06: Maquetación y accesibilidad

| Field | Value |
|-------|-------|
| Ticket | DISC-001-06 |
| Tracker | ninguno |
| Date | 2026-08-20 |
| PRD loops | 0 |

> Noveno y último de los PRDs de **DISC-001** (mapa en `docs/daw/discovery/concept-DISC-001.md`),
> recorte de PRD-001 (`docs/daw/prd/PRD.md`, versión 5). La columna "Origen" de cada requerimiento
> traza al RNF del PRD del producto.
>
> Va al final del mapa porque se mide sobre las pantallas terminadas: una pasada de maquetación
> sobre pantallas que todavía no existen se rehace.

## Context and Problem

Ninguna de las features entregadas hasta hoy definió su maquetación. Está anotado en el índice de
`prd-FEAT-001.md`, y es un hecho, no una impresión: el CSS del proyecto resuelve **lo semántico**
—color de error, foco visible, contraste AA— porque cada ticket lo necesitaba para cumplir sus
criterios, pero **las clases de disposición no tienen regla**. Se escribieron los nombres, no las
reglas. La pantalla se sostiene por el flujo por defecto del navegador.

RNF-06 de PRD-001 fija además un piso de accesibilidad verificable, con tres exigencias concretas:
el formulario de registro tiene que poder completarse y enviarse íntegramente con el teclado, todo
control interactivo tiene que tener foco visible y una etiqueta asociada, y el texto y los controles
tienen que cumplir contraste AA. AC-55 verifica la primera. Las tres se cumplen hoy **por partes**,
ticket por ticket, sin que nadie las haya comprobado sobre la aplicación completa.

Este ticket es la pasada que cierra las dos cosas a la vez, y va al final por una razón simple: la
superficie a maquetar y a auditar son todas las pantallas, y hasta que la última no exista, la
pasada hay que repetirla. La contra de ese orden, dicha de frente: **la aplicación se ve sin
terminar durante todos los tickets anteriores**. Es una decisión de orden, no un olvido.

Una aclaración de alcance, porque "maquetación" es una palabra elástica: este ticket **no rediseña
el producto**. No cambia qué pantallas hay, qué hace cada una ni cómo se navega. Ordena lo que ya
existe y le pone reglas.

## Goals

- Que la aplicación se vea terminada y no como un formulario sin estilos sobre una lista.
- Que cada clase de disposición que el código usa tenga una regla que la respalde.
- Que el piso de accesibilidad de RNF-06 esté verificado sobre la aplicación entera, y no ticket por
  ticket.
- Que la aplicación se pueda usar en la pantalla de un teléfono, que es donde se cargan los gastos.

## Functional Requirements

- FR-01: El sistema debe permitir completar y enviar el formulario de registro de un movimiento íntegramente con el teclado, sin requerir el uso del puntero en ningún paso. Origen: RNF-06, AC-55.
- FR-02: El sistema debe mostrar foco visible en todo control interactivo que reciba el foco, y debe asociar a cada uno una etiqueta accesible. Origen: RNF-06.
- FR-03: El sistema debe asociar cada mensaje de error de validación al campo que lo origina, de modo que quien recorre el formulario con el teclado o con un lector de pantalla reciba el motivo del rechazo al llegar a ese campo. Origen: RNF-06.
- FR-04: El sistema debe definir en la hoja de estilos una regla para cada clase de disposición que el código utiliza, sin dejar ninguna clase referenciada y sin regla. Origen: deuda registrada en el índice de `prd-FEAT-001.md`.
- FR-05: El sistema debe presentar todas sus pantallas de forma utilizable en un ancho de ventana de 360 px, sin desbordes horizontales ni contenido inalcanzable. Origen: decisión de este PRD; PRD-001 no fija un objetivo de ancho y la aplicación se usa para cargar gastos desde el teléfono.
- FR-06: El sistema debe conservar sin cambios el comportamiento de todas las pantallas: qué pantallas existen, qué hace cada una y cómo se navega entre ellas. Origen: decisión de alcance de este PRD.

## Non-Functional Requirements

- NFR-01: El sistema debe cumplir una relación de contraste de al menos 4,5:1 en el texto normal y de al menos 3:1 en el texto grande y en los componentes de interfaz, en el 100 % de los elementos de todas sus pantallas. Origen: RNF-06.
- NFR-02: La suite debe verificar el recorrido completo por teclado del formulario de registro y la presencia de etiqueta accesible en el 100 % de los controles interactivos de las pantallas de la aplicación. Origen: RNF-06, AC-55.
- NFR-03: El sistema debe dejar 0 clases de disposición referenciadas en el código sin una regla correspondiente en la hoja de estilos. Origen: deuda registrada en el índice de `prd-FEAT-001.md`.

## Acceptance Criteria

- AC-01 (FR-01): WHEN se recorre y se envía el formulario de registro usando únicamente el teclado, THE sistema SHALL registrar el movimiento, y cada control recorrido SHALL mostrar foco visible y tener una etiqueta asociada.
- AC-02 (FR-02, NFR-02): WHEN se recorren con el teclado los controles interactivos de todas las pantallas, THE sistema SHALL exhibir foco visible en cada uno y SHALL exponer para cada uno una etiqueta accesible.
- AC-03 (FR-03): IF el usuario intenta guardar un movimiento con un campo inválido, THEN THE sistema SHALL mostrar el motivo asociado a ese campo y SHALL exponer esa asociación a quien recorre el formulario con el teclado o con un lector de pantalla.
- AC-04 (FR-04, NFR-03): WHEN se comparan las clases de disposición referenciadas en el código con las reglas de la hoja de estilos, THE sistema SHALL exhibir 0 clases sin regla.
- AC-05 (FR-05): WHEN se abre cada pantalla de la aplicación en una ventana de 360 px de ancho, THE sistema SHALL mostrar todo su contenido alcanzable y SHALL no producir desborde horizontal.
- AC-06 (NFR-01): WHEN se mide la relación de contraste de los textos y los controles de todas las pantallas, THE sistema SHALL exhibir al menos 4,5:1 en el texto normal y al menos 3:1 en el texto grande y en los componentes de interfaz.
- AC-07 (FR-06): WHEN se ejecuta la suite de pruebas existente después de la pasada de maquetación, THE sistema SHALL pasar todos los casos que pasaba antes, sin que ninguno haya sido modificado para acomodar el cambio visual.
- AC-08 (FR-02): WHEN el usuario recorre la aplicación con el teclado, THE sistema SHALL seguir un orden de foco que corresponda al orden de lectura de cada pantalla.
- AC-09 (FR-01): WHEN el foco está sobre el último control del formulario de registro y el usuario continúa avanzando, THE sistema SHALL llevar el foco fuera del formulario sin dejarlo atrapado en ningún control.

## Out of Scope

- **Rediseñar el producto**: agregar, quitar o reorganizar pantallas, cambiar el flujo de navegación o modificar qué hace cada pantalla. FR-06 lo prohíbe explícitamente.
- **Modo oscuro, temas o cualquier preferencia visual configurable por el usuario.**
- **Animaciones y transiciones.**
- **Nivel AAA de contraste**: RNF-06 fija AA y este ticket lo cumple, no lo supera.
- **Soporte de lectores de pantalla más allá de lo que RNF-06 exige**: etiquetas, foco y asociación de errores. No entra una auditoría completa de ARIA ni la verificación con lectores concretos.
- **Internacionalización** o traducción de la interfaz.
- **Adoptar un framework de estilos o un sistema de diseño**: es una dependencia nueva y no la pide ningún requerimiento.
- **Impresión o vista para imprimir.**
- **Anchos menores a 360 px** o adaptaciones específicas por dispositivo.

## Risks and Mitigations

- **Riesgo: "maquetación" se expande sola hasta volverse un rediseño.** Es el riesgo principal de este ticket, porque una vez que alguien toca el CSS, todo se ve mejorable. → Mitigación: FR-06 y AC-07 lo acotan de forma verificable — la suite existente tiene que seguir pasando **sin que ningún test se haya modificado para acomodar el cambio visual**. Un test que hubo que retocar es la señal de que el comportamiento cambió.
- **Riesgo: la accesibilidad se verifica a ojo y queda en una impresión.** → Mitigación: NFR-01, NFR-02 y NFR-03 están escritos como porcentajes y conteos, y AC-04 y AC-06 se miden sobre la aplicación entera, no sobre la pantalla que alguien miró.
- **Riesgo: ir al final significa que la aplicación se ve sin terminar durante todos los tickets previos.** → Mitigación: ninguna; es la contra aceptada del orden elegido. Se anota para que sea una decisión y no una sorpresa. Si el proyecto necesita mostrarse antes, este ticket se adelanta sabiendo que habrá que repetir la pasada sobre las pantallas que falten.
- **Riesgo: el ancho de 360 px no sale de PRD-001.** Lo fija este PRD porque "maquetación" sin un objetivo de ancho no es verificable. → Mitigación: queda declarado como decisión propia en FR-05 y no como traducción de un requerimiento; si el objetivo es otro, se cambia acá.
- **Riesgo: tocar el CSS de foco y de error puede romper criterios que otros tickets ya verifican.** El color de error y el foco visible existen porque FEAT-001a los necesitaba. → Mitigación: AC-07, y el hecho de que esos criterios ya tienen tests propios en la suite.

## Dependencies

- Todas las pantallas de la aplicación entregadas: FEAT-001a, `b` y `c` mergeados, y —según cuándo se ejecute— los PRDs 01a..01c (pantallas de alta y login), 03 (categorías propias) y 05 (dashboard). Cada uno que falte es una pantalla que la pasada no va a cubrir.
- El CSS existente del proyecto, que ya resuelve color de error, foco visible y contraste AA en los elementos que los tickets anteriores necesitaron.
- La suite de pruebas existente, que AC-07 usa como referencia de que el comportamiento no cambió.
- El `typecheck` del frontend (`pnpm --dir frontend exec tsc --noEmit`), declarado en la sección Stack de `AGENTS.md`.
