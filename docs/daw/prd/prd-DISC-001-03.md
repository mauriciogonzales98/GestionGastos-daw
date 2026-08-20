# PRD DISC-001-03: Categorías propias del usuario

| Field | Value |
|-------|-------|
| Ticket | DISC-001-03 |
| Tracker | ninguno |
| Date | 2026-08-20 |
| PRD loops | 0 |

> Quinto de los ocho PRDs de **DISC-001** (mapa en `docs/daw/discovery/concept-DISC-001.md`),
> recorte de PRD-001 (`docs/daw/prd/PRD.md`, versión 5). La columna "Origen" de cada requerimiento
> traza al RF del PRD del producto.
>
> Depende de la autenticación (`01a`..`01c`): sin cuentas reales, "categoría propia" no significa
> nada y AC-12 de PRD-001 no es observable.

## Context and Problem

Hoy el catálogo de categorías es **global, fijo y de solo lectura**: diez filas sembradas con ids
fijos —siete de gasto, tres de ingreso—, sin propietario, servidas por un único
`GET /api/categorias` que no filtra por usuario porque no hay nada por lo que filtrar.

PRD-001 pide en RF-07, RF-08 y RF-09 que además el usuario pueda crear las suyas, renombrarlas y
darlas de baja. Eso convierte el catálogo en dos cosas a la vez: una parte compartida e inmutable, y
una parte privada y editable de cada cuenta.

**El detalle que decide el ticket está en el esquema.** La tabla `categorias` tiene un índice único
sobre `(nombre, tipo)` que hoy es global — así se garantiza que "Otros" exista una vez como gasto y
una vez como ingreso. Con categorías propias ese índice deja de servir: si un usuario crea "Mascota"
y otro también, el segundo choca contra una fila que no puede ver. La unicidad tiene que pasar a ser
**por usuario**, y las predefinidas quedan como las filas sin propietario que todos comparten.

La segunda decisión estructural es la **baja lógica** de RF-09, que existe por una razón concreta:
un movimiento ya registrado tiene que seguir mostrando el nombre de su categoría aunque el usuario
la haya eliminado, y su monto tiene que seguir sumando en el desglose del resumen. Borrar la fila
rompería las dos cosas. Es el mismo criterio que AC-14 de PRD-001 verifica de punta a punta.

Hay además una deuda heredada que este ticket es el lugar natural para saldar: hoy
`FiltrosMovimientos` y `FormularioMovimiento` piden cada uno `GET /api/categorias` al montar —dos
peticiones al mismo endpoint por arranque, y una tercera al abrir la edición—. Con un catálogo fijo
era ineficiente pero inocuo; con un catálogo que el usuario modifica, dos copias del mismo dato en
pantalla pueden mostrar cosas distintas después de crear una categoría.

## Goals

- Que el usuario pueda nombrar sus gastos e ingresos con las categorías que usa de verdad, sin
  quedar atado a las diez predefinidas.
- Que eliminar una categoría no borre ni distorsione la historia ya registrada.
- Que el catálogo de una cuenta sea invisible para las demás.
- Que la pantalla muestre un único catálogo, y no dos copias que pueden discrepar.

## Functional Requirements

- FR-01: El sistema debe permitir crear una categoría propia indicando nombre y tipo, gasto o ingreso. Origen: RF-07.
- FR-02: El sistema debe ofrecer a cada usuario, en el formulario de registro y en el filtro por categoría, las categorías predefinidas del tipo correspondiente más sus propias categorías activas de ese tipo, y ninguna categoría propia de otro usuario. Origen: RF-06, RF-07.
- FR-03: El sistema debe permitir modificar el nombre de una categoría propia, conservando su tipo. Origen: RF-08.
- FR-04: El sistema debe eliminar una categoría propia mediante baja lógica, conservando la fila y su nombre. Origen: RF-09.
- FR-05: El sistema debe dejar de ofrecer una categoría dada de baja en el formulario de registro y en el filtro por categoría, y debe seguir mostrando su nombre en los movimientos ya registrados con ella. Origen: RF-09.
- FR-06: El sistema debe rechazar la modificación y la eliminación de una categoría predefinida, dejándola sin cambios. Origen: RF-06.
- FR-07: El sistema debe rechazar la creación de una categoría propia cuyo nombre y tipo coincidan con los de otra categoría activa disponible para ese mismo usuario, sea propia o predefinida, indicando el motivo. Origen: decisión de este PRD; RF-07 no lo especifica y el esquema obliga a definirlo.
- FR-08: El sistema debe rechazar la creación y la modificación de una categoría propia cuyo nombre esté vacío o supere los 60 caracteres, indicando el motivo. Origen: RF-07, RF-08; el límite es el que ya tiene la columna.

## Non-Functional Requirements

- NFR-01: El sistema debe acotar la creación, modificación, eliminación y consulta de categorías propias al usuario de la sesión, y la suite debe cubrir con al menos un caso de acceso cruzado entre dos cuentas el 100 % de los endpoints de categorías. Origen: RF-04.
- NFR-02: La aplicación debe solicitar el catálogo de categorías a lo sumo 1 vez por carga de la pantalla principal, y debe reflejar en todos los controles que lo usan el resultado de crear, renombrar o dar de baja una categoría sin recargar la página. Origen: deuda registrada en el índice de `prd-FEAT-001.md`; hoy son 2 peticiones por arranque y una tercera al abrir la edición.
- NFR-03: El sistema debe dejar sin variación el total ingresado, el total gastado, el balance y el monto de cada categoría del desglose ante la baja lógica de una categoría con movimientos. Origen: RF-09, AC-14 de PRD-001.

## Acceptance Criteria

- AC-01 (FR-01, FR-02): WHEN el usuario crea una categoría propia de tipo gasto, THE sistema SHALL ofrecerla en el selector de categorías de gasto de ese usuario y SHALL no ofrecerla a ningún otro usuario.
- AC-02 (FR-02): WHEN un usuario recién registrado y sin categorías propias abre el formulario de registro de un gasto, THE sistema SHALL ofrecer las categorías predefinidas de tipo gasto y ninguna de tipo ingreso.
- AC-03 (FR-06): IF el usuario intenta modificar o eliminar una categoría predefinida, THEN THE sistema SHALL rechazar la operación y SHALL dejar esa categoría con el mismo nombre y el mismo tipo.
- AC-04 (FR-03): WHEN el usuario cambia el nombre de una categoría propia que tiene movimientos asociados, THE sistema SHALL mostrar el nombre nuevo en esos movimientos del listado y en el desglose del resumen.
- AC-05 (FR-04, FR-05): WHEN el usuario elimina una categoría propia que tiene movimientos asociados, THE sistema SHALL dejar de ofrecerla en el formulario de registro y en el filtro por categoría, y SHALL seguir mostrando su nombre en esos movimientos del listado.
- AC-06 (FR-04, NFR-03): WHEN el usuario elimina una categoría propia que tiene movimientos asociados, THE sistema SHALL dejar el total gastado, el total ingresado, el balance y el monto de esa categoría en el desglose con los mismos valores que antes de eliminarla.
- AC-07 (FR-07): IF el usuario intenta crear una categoría propia con el mismo nombre y el mismo tipo que una categoría activa que ya tiene disponible, sea propia o predefinida, THEN THE sistema SHALL rechazar la creación, SHALL indicar el motivo y SHALL no crear ninguna categoría.
- AC-08 (FR-07): WHEN dos usuarios distintos crean cada uno una categoría propia con el mismo nombre y el mismo tipo, THE sistema SHALL aceptar las dos y SHALL ofrecerle a cada usuario únicamente la suya.
- AC-09 (FR-07, FR-04): WHEN el usuario crea una categoría propia con el mismo nombre y tipo que otra que él mismo había dado de baja, THE sistema SHALL aceptar la creación, SHALL ofrecer la categoría nueva en el selector, y SHALL seguir mostrando la categoría dada de baja en los movimientos que la usan.
- AC-10 (FR-08): IF el nombre de una categoría propia se envía vacío o con más de 60 caracteres, THEN THE sistema SHALL rechazar la operación, SHALL indicar el motivo y SHALL no crear ni modificar ninguna categoría.
- AC-11 (NFR-01): IF un usuario intenta modificar o eliminar por su identificador una categoría propia de otro usuario, THEN THE sistema SHALL denegar la operación, SHALL dejar esa categoría sin cambios y SHALL responder con el mismo código y el mismo cuerpo que ante un identificador inexistente.
- AC-12 (NFR-02): WHEN el usuario carga la pantalla principal, THE sistema SHALL solicitar el catálogo de categorías a lo sumo 1 vez.
- AC-13 (NFR-02): WHEN el usuario crea o renombra una categoría propia, THE sistema SHALL reflejar el cambio tanto en el selector del formulario de registro como en el filtro por categoría, sin recargar la página.

## Out of Scope

- **Modificar o eliminar las categorías predefinidas del sistema**: PRD-001 lo deja fuera de alcance de forma explícita, y FR-06 lo prohíbe.
- **Reasignar los movimientos de una categoría dada de baja** a otra categoría: la baja lógica existe precisamente para no tocarlos.
- **Restaurar una categoría dada de baja.** AC-09 fija que el camino es crear una nueva con el mismo nombre; no hay pantalla ni endpoint para reactivar la anterior.
- **Borrado físico de una categoría propia**, incluso sin movimientos asociados.
- **Color, ícono, orden manual, jerarquía o subcategorías.**
- **Presupuestos o topes por categoría**: fuera de alcance en PRD-001.
- **Compartir categorías entre cuentas** o proponer las de un usuario a otro.
- **Límite a la cantidad de categorías propias** de una cuenta.
- **Fusionar dos categorías** en una.

## Risks and Mitigations

- **Riesgo: el índice único `(nombre, tipo)` es global y rompe en cuanto dos usuarios elijan el mismo nombre.** El segundo choca contra una fila que ni siquiera puede ver, y el mensaje de error que recibiría delataría su existencia. → Mitigación: FR-07 y AC-08 fijan que la unicidad es **por usuario**; el cambio de índice es trabajo de la migración y queda para el PLAN.
- **Riesgo: la baja lógica reaparece donde no debe.** Es el riesgo que PRD-001 anota explícitamente: una categoría dada de baja que sigue apareciendo en un selector o en un filtro porque esa consulta no la excluye. → Mitigación: FR-05 y AC-05 lo verifican en los dos lugares que la ofrecen, formulario y filtro, y AC-06 verifica que sí siga sumando donde debe.
- **Riesgo: la unicidad y la baja lógica interactúan mal.** Si la unicidad ignorara el estado de baja, un usuario no podría volver a usar un nombre que él mismo dio de baja; si lo ignora del todo, el listado puede mostrar dos categorías homónimas. → Mitigación: AC-09 fija el comportamiento elegido —se puede volver a crear, y la vieja sigue nombrando sus movimientos—, que es la lectura que preserva la historia.
- **Riesgo: crear una categoría propia reintroduce la fricción que el producto combate**, si el usuario termina con muchas categorías casi iguales. → Mitigación: la de PRD-001 — las predefinidas son la opción por defecto y crear una propia es una acción aparte, fuera del camino rápido de carga. Este PRD no la pone en el formulario de registro.
- **Riesgo: dos copias del catálogo en pantalla pueden discrepar.** Con un catálogo fijo la doble petición era solo ineficiente; con uno mutable, crear una categoría puede actualizar un selector y no el otro. → Mitigación: NFR-02, AC-12 y AC-13. El refactor cambia la interfaz pública de `FiltrosMovimientos` y `FormularioMovimiento` y obliga a reescribir sus fixtures — está anotado como tal en el índice de FEAT-001.

## Dependencies

- `DISC-001-01a`, `01b` y `01c` mergeados en `main`: sin cuentas reales no hay "categoría propia", y AC-01, AC-08 y AC-11 no son observables.
- La tabla `categorias` y su índice único `ux_categorias_nombre_tipo`, introducidos por FEAT-001a, que la migración de este ticket tiene que rehacer.
- Las diez categorías predefinidas sembradas con ids fijos en `CategoriaConfiguracion`, que FR-06 vuelve inmutables y que la migración debe conservar sin propietario.
- `GET /api/categorias` y los componentes `FiltrosMovimientos` y `FormularioMovimiento` de FEAT-001a y `b`, que NFR-02 obliga a reorganizar.
- El resumen con desglose por categoría de FEAT-001c, sobre el que AC-04 y AC-06 verifican el efecto del renombre y de la baja.
- MySQL 8.4.10 y una migración que agregue el propietario y el estado de baja a `categorias`.
