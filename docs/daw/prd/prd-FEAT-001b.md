# PRD FEAT-001b: Filtros del listado, edición y eliminación de movimientos

| Field | Value |
|-------|-------|
| Ticket | FEAT-001b |
| Tracker | ninguno |
| Date | 2026-08-16 |
| PRD loops | 0 |

> Sub-ticket `b` de **FEAT-001** (índice en `docs/daw/prd/prd-FEAT-001.md`), que a su vez es un
> recorte de PRD-001 (`docs/daw/prd/PRD.md`, versión 5). La columna "Origen" de cada requerimiento
> traza al RF del PRD del producto.
>
> Dependencias entre sub-tickets: depende de FEAT-001a, que entrega el modelo de datos, el alta de
> movimientos y el listado sin filtros. FEAT-001c depende de este.

## Context and Problem

FEAT-001a deja una aplicación que carga movimientos y los lista todos, siempre. Eso alcanza mientras
hay diez movimientos y deja de alcanzar al segundo mes: el listado crece sin techo, y el usuario que
quiere ver en qué se le fue la plata en marzo tiene que buscarlo a ojo entre todo lo que cargó desde
que empezó.

Faltan además las dos operaciones sin las cuales un movimiento mal cargado no tiene arreglo:
corregirlo y borrarlo. Hoy un monto tipeado con un cero de más queda ahí para siempre y contamina
todos los totales que construya el sub-ticket siguiente.

Este sub-ticket cubre las dos cosas: acotar el listado por categoría y por rango de fechas —con el
mes actual como valor por defecto, que es lo que responde la pregunta *cómo vengo este mes*— y
permitir modificar y eliminar un movimiento propio con las mismas validaciones que rigen el alta.

## Goals

- Que el usuario pueda acotar el listado a una categoría, a un rango de fechas, o a ambos.
- Que abrir la aplicación muestre por defecto el mes en curso, y no todo el historial.
- Que un movimiento cargado con un error se pueda corregir o eliminar sin tocar la base de datos a
  mano.
- Que corregir un movimiento no sea una puerta trasera a las validaciones del alta.

## Functional Requirements

- FR-01: El sistema debe permitir modificar el monto, la categoría, la fecha y la nota de un movimiento propio ya registrado, aplicando las mismas validaciones de monto, categoría y nota que rigen el alta. Origen: RF-14.
- FR-02: El sistema debe permitir eliminar un movimiento propio ya registrado. Origen: RF-15.
- FR-03: El sistema debe rechazar toda modificación o eliminación de un movimiento inexistente o cuyo propietario no sea el usuario actual. Origen: RF-04 (parcial).
- FR-04: El sistema debe permitir filtrar el listado por categoría, tomando "todas las categorías" como valor por defecto. Origen: RF-17.
- FR-05: El sistema debe permitir filtrar el listado por rango de fechas, incluyendo ambos extremos, tomando el mes calendario actual como valor por defecto. Origen: RF-18.
- FR-06: El sistema debe rechazar un rango de fechas cuya fecha de inicio sea posterior a su fecha de fin. Origen: RF-18.

## Non-Functional Requirements

- NFR-01: El listado filtrado debe responder en menos de 1 s en el percentil 95 sobre una cuenta con 1000 movimientos registrados. Origen: RNF-01.
- NFR-02: El sistema debe resolver los dos filtros en la consulta a la base de datos, apoyado en un índice sobre propietario y fecha, y no debe transferir al frontend ningún movimiento fuera del rango filtrado.

## Acceptance Criteria

- AC-01 (FR-01): WHEN el usuario modifica el monto de un movimiento propio y guarda, THE sistema SHALL reflejar el monto nuevo en el listado, y no el anterior.
- AC-02 (FR-01): WHEN el usuario cambia la categoría y la fecha de un movimiento propio y guarda, THE sistema SHALL persistir ambos valores nuevos y SHALL incluir el movimiento en el listado solo si su fecha nueva cae dentro del rango filtrado.
- AC-03 (FR-01): WHEN el usuario edita la nota de un movimiento propio o la borra y guarda, THE sistema SHALL reflejar el valor nuevo en la fila del listado.
- AC-04 (FR-01): IF el usuario intenta guardar la modificación de un movimiento con un monto que no es un número mayor a cero con hasta dos decimales, sin categoría, con una categoría de tipo distinto al del movimiento, o con una nota de más de 120 caracteres, THEN THE sistema SHALL rechazar el guardado y dejar el movimiento con todos sus valores anteriores.
- AC-05 (FR-02): WHEN el usuario elimina un movimiento propio, THE sistema SHALL quitarlo del listado y SHALL dejar de devolverlo en consultas posteriores.
- AC-06 (FR-03): IF el usuario intenta modificar o eliminar un movimiento con un identificador que no existe o cuyo propietario no es el usuario actual, THEN THE sistema SHALL responder con un error 404 y no alterar ningún movimiento.
- AC-07 (FR-04): WHEN el usuario aplica el filtro de una categoría, THE sistema SHALL mostrar en el listado únicamente movimientos de esa categoría.
- AC-08 (FR-04): WHEN el usuario abre el listado sin tocar el filtro de categoría, THE sistema SHALL mostrar movimientos de todas las categorías.
- AC-09 (FR-05): WHEN el usuario abre el listado sin tocar el filtro de fechas, THE sistema SHALL mostrar únicamente los movimientos cuya fecha cae dentro del mes calendario actual.
- AC-10 (FR-05): WHEN el usuario aplica un rango de fechas, THE sistema SHALL mostrar únicamente los movimientos cuya fecha cae dentro de ese rango, incluidos los movimientos con fecha igual a cada uno de sus dos extremos.
- AC-11 (FR-04, FR-05): WHEN el usuario aplica a la vez el filtro de una categoría y un rango de fechas, THE sistema SHALL mostrar únicamente los movimientos que cumplen las dos condiciones.
- AC-12 (FR-06): IF el usuario aplica un rango cuya fecha de inicio es posterior a la de fin, THEN THE sistema SHALL rechazar el filtro con un error de validación, mostrar el motivo y mantener el listado con el rango anterior.
- AC-13 (NFR-01): WHEN se mide la carga del listado filtrado sobre 100 ejecuciones en una cuenta con 1000 movimientos, THE sistema SHALL responder en menos de 1 s en el percentil 95.
- AC-14 (NFR-02): WHEN se inspecciona la respuesta del endpoint del listado con un rango que excluye movimientos existentes, THE sistema SHALL no incluir ninguno de esos movimientos en la respuesta.

## Out of Scope

- Alta de movimientos, catálogo de categorías, modelo de datos y listado sin filtros: son FEAT-001a, del que este sub-ticket depende.
- Resumen del mes, balance y desglose de gastos por categoría: es FEAT-001c.
- Filtro por tipo de movimiento (gasto o ingreso): PRD-001 no lo pide; el listado sigue mostrando ambos tipos juntos.
- Filtro y búsqueda por la nota del movimiento: PRD-001 lo declara explícitamente fuera de alcance, la nota se lee y no se analiza.
- Filtro por moneda (RF-28 de PRD-001): mientras haya una sola moneda no tiene nada que discriminar.
- Autenticación (RF-01..RF-05 de PRD-001). FR-03 restringe por el usuario actual fijo que deja FEAT-001a, no por sesión.
- Paginación y ordenamiento configurable del listado. El orden es el de FEAT-001a, por fecha descendente.
- Baja lógica de movimientos, historial de cambios o papelera: la eliminación de FR-02 es definitiva.
- Modificación del tipo de un movimiento, convirtiendo un gasto en ingreso o al revés: se elimina y se carga de nuevo.

## Risks and Mitigations

- Riesgo: implementar la modificación como un camino aparte del alta y que se le escape alguna validación, dejando por la puerta de atrás montos negativos o notas de 500 caracteres → mitigación: FR-01 exige las mismas validaciones y AC-04 las verifica las cuatro sobre el endpoint de modificación.
- Riesgo: responder 403 en lugar de 404 ante un movimiento ajeno, revelando que ese identificador existe → mitigación: AC-06 fija 404 para las dos situaciones, inexistente y ajeno.
- Riesgo: resolver los filtros en memoria trayendo todos los movimientos del usuario, que funciona con 20 movimientos y deja de funcionar con 5000 → mitigación: NFR-02 lo prohíbe, AC-14 lo verifica sobre la respuesta y AC-13 lo mide.
- Riesgo: el extremo superior del rango de fechas quedando excluido por comparar con una fecha con hora, el error más común de todo filtro por rango → mitigación: AC-10 verifica explícitamente los movimientos con fecha igual a cada extremo.
- Riesgo: que el filtro por defecto del mes actual haga creer al usuario que perdió los movimientos de meses anteriores → mitigación: el rango vigente se muestra siempre visible junto al listado, y no como un estado implícito.
- Riesgo: la eliminación definitiva de FR-02 borrando un movimiento por un clic accidental, sin vuelta atrás → mitigación: la eliminación pide confirmación explícita antes de ejecutarse.

## Dependencies

- FEAT-001a (`docs/daw/prd/prd-FEAT-001a.md`): entrega el modelo de datos, la abstracción de usuario actual, el catálogo de categorías, el alta de movimientos y el listado sin filtros sobre el que este sub-ticket agrega los filtros. Este sub-ticket no puede empezar antes de que `a` cierre.
- Base de datos MySQL 8.4.10, schema `gestiongastos`, y el índice sobre propietario y fecha que exige NFR-02 (declarada en `AGENTS.md`, sección Stack).
- Entity Framework Core 9.0.18 con Pomelo.MySQL 9.0.0 para el acceso a datos, y la migración que agrega el índice de NFR-02.
- Backend .NET 10 exponiendo la API HTTP que consume el frontend React 19 + Vite; ambos declarados en `AGENTS.md`, sección Stack.
- Ticket posterior de autenticación: reemplazará la abstracción de usuario actual sobre la que se apoya FR-03.
- PRD padre FEAT-001 (`docs/daw/prd/prd-FEAT-001.md`) y PRD-001 (`docs/daw/prd/PRD.md`), de los que este documento es la segunda parte.
