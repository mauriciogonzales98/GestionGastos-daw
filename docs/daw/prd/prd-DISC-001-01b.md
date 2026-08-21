# PRD DISC-001-01b: Límite de intentos fallidos de inicio de sesión

| Field | Value |
|-------|-------|
| Ticket | DISC-001-01b |
| Tracker | ninguno |
| Date | 2026-08-20 |
| PRD loops | 0 |

> Segundo de los ocho PRDs de **DISC-001** (mapa en `docs/daw/discovery/concept-DISC-001.md`),
> recorte de PRD-001 (`docs/daw/prd/PRD.md`, versión 5). La columna "Origen" de cada requerimiento
> traza al RF o RNF del PRD del producto.
>
> Segundo de los tres cortes de la autenticación: `01a` construye identidad y sesión, **`01b`
> (este)** agrega el límite de intentos, `01c` verifica el aislamiento entre cuentas. Depende de
> `01a` y va inmediatamente después, sin nada de otro tema intercalado.

## Context and Problem

`01a` entrega el inicio de sesión, y lo entrega **sin ninguna protección contra la fuerza bruta**:
cualquiera puede probar contraseñas contra un email de forma indefinida, tan rápido como el servidor
conteste. Esa ventana está registrada como riesgo aceptado en el PRD de `01a`, con la condición de
que este ticket sea el siguiente.

RNF-05 de PRD-001 fija la regla: tras 5 intentos fallidos consecutivos sobre un mismo email, todo
intento nuevo sobre ese email se rechaza durante al menos 15 minutos, **sin revelar si el email está
registrado**.

Sale como ticket propio y no como parte de `01a` por una razón concreta: es la única parte de la
autenticación con **estado propio que no es la sesión**. Hay que decidir dónde vive el contador,
cuándo se reinicia, qué pasa si el proceso se reinicia en medio de una ventana de bloqueo y qué pasa
con dos instancias de la aplicación contando por separado. Eso es un modelo de datos y un conjunto
de casos de borde que no tienen nada que ver con "dar de alta una cuenta y entrar", y mezclarlos
hacía de `01a` un ticket con dos temas.

La contra de partirlo, dicha en voz alta: **entre `01a` y `01b` la aplicación tiene login sin
límite de intentos**. Es una ventana entre dos tickets consecutivos, no un estado en el que se
piense dejar el producto.

## Goals

- Que probar contraseñas contra un email deje de ser gratis y sin fin.
- Que el bloqueo se levante solo, sin que nadie tenga que intervenir.
- Que un atacante no pueda usar el propio bloqueo para averiguar qué emails tienen cuenta.

## Functional Requirements

- FR-01: El sistema debe contar los intentos de inicio de sesión fallidos consecutivos por cada email presentado, con independencia de que ese email corresponda o no a una cuenta existente. Origen: RNF-05.
- FR-02: El sistema debe rechazar todo intento de inicio de sesión sobre un email que acumule 5 intentos fallidos consecutivos, durante al menos 15 minutos contados desde el quinto fallo, incluidos los intentos que presenten la contraseña correcta. Origen: RNF-05.
- FR-03: El sistema debe reiniciar a cero el contador de fallos consecutivos de un email cuando un intento sobre ese email resulta exitoso. Origen: RNF-05.
- FR-04: El sistema debe permitir nuevamente el inicio de sesión sobre un email bloqueado una vez transcurrida la ventana de 15 minutos, sin intervención de ninguna persona. Origen: RNF-05.
- FR-05: El sistema debe responder a un intento rechazado por el límite con el mismo mensaje y el mismo código de estado que a un intento rechazado por credenciales incorrectas. Origen: RNF-05.
- FR-06: El sistema debe contar los fallos y aplicar el bloqueo por email presentado, y no por sesión, por navegador ni por dirección de origen. Origen: RNF-05.

## Non-Functional Requirements

- NFR-01: El sistema debe conservar el contador de fallos y el estado de bloqueo de un email durante al menos los 15 minutos de la ventana, incluso si el proceso de la aplicación se reinicia dentro de ese lapso. Origen: RNF-05 ("durante al menos 15 minutos" no se cumple si un reinicio lo borra).
- NFR-02: La comprobación del límite debe agregar como máximo 50 ms al tiempo de respuesta del inicio de sesión, en el percentil 95. Origen: RNF-02 (presupuesto de tiempo del PRD del producto).
- NFR-03: El sistema debe tardar lo mismo, dentro de un margen de 50 ms en el percentil 95, en rechazar un intento sobre un email bloqueado que sobre un email no bloqueado con credenciales incorrectas, de modo que la diferencia de tiempo no permita distinguir un caso del otro. Origen: RNF-05 (la exigencia de no revelar, aplicada al canal lateral de tiempo).

## Acceptance Criteria

- AC-01 (FR-01, FR-02): IF se registran 5 intentos de inicio de sesión fallidos consecutivos sobre un mismo email, THEN THE sistema SHALL rechazar el sexto intento sobre ese email.
- AC-02 (FR-02): IF un email acumula 5 intentos fallidos consecutivos y se presenta a continuación su contraseña correcta dentro de los 15 minutos, THEN THE sistema SHALL rechazar el intento y SHALL no iniciar ninguna sesión.
- AC-03 (FR-02): WHEN han transcurrido menos de 15 minutos desde el quinto fallo consecutivo sobre un email, THE sistema SHALL seguir rechazando todo intento sobre ese email.
- AC-04 (FR-01): WHEN se acumulan 4 intentos fallidos consecutivos sobre un email, THE sistema SHALL aceptar un quinto intento que presente la contraseña correcta e iniciar la sesión.
- AC-05 (FR-03): WHEN un intento sobre un email con fallos previos no bloqueantes resulta exitoso, THE sistema SHALL dejar el contador de ese email en cero, de modo que hagan falta 5 fallos nuevos para bloquearlo.
- AC-06 (FR-04): WHEN transcurren 15 minutos desde el quinto fallo consecutivo sobre un email, THE sistema SHALL aceptar sobre ese email un intento con la contraseña correcta e iniciar la sesión, sin que nadie haya intervenido.
- AC-07 (FR-01): WHEN se acumulan 5 intentos fallidos consecutivos sobre el email A, THE sistema SHALL seguir aceptando un intento con contraseña correcta sobre el email B.
- AC-08 (FR-05): WHEN se comparan la respuesta a un intento rechazado por el límite y la respuesta a un intento rechazado por credenciales incorrectas, THE sistema SHALL devolver el mismo mensaje y el mismo código de estado en los dos casos.
- AC-09 (FR-01, FR-06): IF se acumulan 5 intentos fallidos consecutivos sobre un email no registrado, THEN THE sistema SHALL rechazar el sexto intento sobre ese email con el mismo mensaje y el mismo código que emplea para un email registrado y bloqueado.
- AC-10 (FR-06): WHEN un email acumula 5 fallos consecutivos desde un navegador, THE sistema SHALL rechazar también los intentos sobre ese mismo email realizados desde otro navegador dentro de la ventana.
- AC-11 (NFR-01): IF el proceso de la aplicación se reinicia mientras un email está dentro de su ventana de bloqueo, THEN THE sistema SHALL seguir rechazando los intentos sobre ese email hasta que se cumplan los 15 minutos.
- AC-12 (NFR-02): WHEN se mide el inicio de sesión sobre 100 ejecuciones con la comprobación del límite activa y sin ella, THE sistema SHALL mostrar una diferencia de a lo sumo 50 ms en el percentil 95.
- AC-13 (NFR-03): WHEN se miden sobre 100 ejecuciones el rechazo de un intento sobre un email bloqueado y el rechazo de un intento con credenciales incorrectas sobre un email no bloqueado, THE sistema SHALL mostrar una diferencia de a lo sumo 50 ms en el percentil 95.

## Out of Scope

- **Bloqueo por dirección IP o por dispositivo.** FR-06 lo excluye de forma deliberada: PRD-001 pide el límite por email. Un bloqueo por IP es la mitigación natural del riesgo de denegación de servicio que se anota más abajo, y es un ticket propio si ese riesgo se materializa.
- **CAPTCHA o cualquier desafío interactivo** antes o después del bloqueo.
- **Desbloqueo manual**, por parte del propio usuario o de un administrador: FR-04 exige que la ventana se levante sola, y no hay pantalla ni endpoint para adelantarla.
- **Avisarle al usuario que su cuenta fue bloqueada**, por email o por cualquier otro canal. PRD-001 deja fuera de alcance todo envío de correo.
- **Límite de intentos sobre el alta de cuentas** u otros endpoints: este PRD cubre el inicio de sesión.
- **Endurecer la política de contraseñas** (longitud mínima, complejidad, listas de contraseñas filtradas).
- **Registro de auditoría de los intentos fallidos** para consulta posterior. El contador es estado operativo, no una bitácora.

## Risks and Mitigations

- **Riesgo: el bloqueo por email es un vector de denegación de servicio dirigido.** Cualquiera que conozca el email de otra persona la deja afuera 15 minutos con 5 intentos fallidos, y puede repetirlo indefinidamente. → Mitigación: ninguna dentro de este alcance. Es exactamente el comportamiento que RNF-05 pide, y la alternativa —bloquear por IP— tiene su propia contra: deja pasar al atacante distribuido y castiga a los usuarios detrás de una misma salida a internet. Se registra para que quede escrito que se eligió, y no que se pasó por alto. Si molesta en uso real, se resuelve sumando un límite por IP **además** del de email, no reemplazándolo.
- **Riesgo: dos instancias de la aplicación contando por separado multiplican el límite.** Con el contador en memoria de cada proceso, dos instancias toleran 10 fallos en vez de 5. → Mitigación: NFR-01 ya obliga a que el estado sobreviva a un reinicio, lo que en la práctica descarta el contador puramente en memoria; la decisión concreta se registra como ADR en el PLAN.
- **Riesgo: el contador puede crecer sin límite.** Un atacante que golpee con millones de emails distintos deja una fila por email. → Mitigación: los registros vencidos se pueden purgar; el criterio concreto es decisión del PLAN, y este PRD solo exige que el estado viva los 15 minutos de la ventana.
- **Riesgo: la ventana se mide con el reloj del servidor.** Un cambio de hora o una desincronización la alarga o la acorta. → Mitigación: acotada — 15 minutos es una ventana corta y el efecto es transitorio. Se registra porque el mismo tipo de dependencia del reloj real ya produjo un warning con vencimiento en FEAT-001c (W-VER-03).
- **Riesgo: los tres criterios medidos por tiempo (AC-12, AC-13) son sensibles a un entorno cargado**, como ya lo son los tests de rendimiento de FEAT-001a, `b` y `c`. → Mitigación: el CI los excluye con el filtro `FullyQualifiedName!~Rendimiento` que ya existe; se nombran siguiendo esa convención para que el filtro los alcance.

## Dependencies

- `DISC-001-01a` (identidad y sesión) mergeado en `main`: este PRD agrega una comprobación sobre el inicio de sesión que allí se construye. Sin él no hay login sobre el cual contar fallos.
- MySQL 8.4.10 disponible, si el estado del contador se persiste en la base — que es lo que NFR-01 empuja.
- El filtro de tests de rendimiento del CI (`FullyQualifiedName!~Rendimiento`), declarado en la sección Stack de `AGENTS.md`, que AC-12 y AC-13 necesitan para no dar rojos sin significado en un runner compartido.
