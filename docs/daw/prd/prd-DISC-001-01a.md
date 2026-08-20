# PRD DISC-001-01a: Identidad y sesión

| Field | Value |
|-------|-------|
| Ticket | DISC-001-01a |
| Tracker | ninguno |
| Date | 2026-08-20 |
| PRD loops | 1 |

> Primero de los siete PRDs de **DISC-001** (mapa en `docs/daw/discovery/concept-DISC-001.md`),
> recorte de PRD-001 (`docs/daw/prd/PRD.md`, versión 5). La columna "Origen" de cada requerimiento
> traza al RF del PRD del producto.
>
> Dependencias: se implementa sobre `main` con FEAT-001a, `b` y `c` ya mergeados. La autenticación
> de PRD-001 se parte en tres, porque junta suma 5 RF, 3 RNF y 12 AC sobre esquema, API y frontend
> — por encima del umbral que ya obligó a partir FEAT-001 en tres:
>
> - **`01a` (este)** — identidad y sesión: alta, login, logout y sesión obligatoria.
> - **`01b`** — límite de intentos fallidos: el conteo y la ventana de bloqueo de RNF-05.
> - **`01c`** — aislamiento entre cuentas verificado con dos usuarios reales (AC-06..AC-08).
>
> Los tres tienen que estar en `main` antes de exponer la aplicación a usuarios reales.

## Context and Problem

FEAT-001 dejó la aplicación entera funcionando sobre **un único usuario semilla**: una fila fija en
la base, detrás de la abstracción `IUsuarioActual`. Fue una decisión explícita y deliberada — el
modelo lleva la pertenencia al usuario desde el día uno para que la autenticación **reemplace esa
abstracción** en vez de exigir una migración de datos.

Hoy eso significa que cualquiera que abra la aplicación ve y modifica los mismos movimientos. No hay
alta de cuenta, no hay login, no hay sesión, y los tres criterios de aislamiento de PRD-001
(AC-06..AC-08) no son verificables: con un solo usuario, un test de aislamiento no puede fallar.

Este PRD construye la mitad que produce identidad: registrarse, entrar, salir, y que ninguna
pantalla ni endpoint responda sin sesión. El momento en que `IUsuarioActual` deja de devolver la
fila semilla y pasa a devolver al usuario autenticado es la costura que hace observable todo lo
demás.

Los datos de la fila semilla **se descartan en la migración**. Son datos de desarrollo, no de un
usuario real: la aplicación arranca vacía y la primera cuenta que se registre empieza de cero.
Decidido el 2026-08-20; la contra es que se pierde lo que se haya cargado probando, y se acepta a
cambio de no dejar en el modelo una lógica de adopción que corre una sola vez en la vida del
producto.

## Goals

- Que cada persona tenga su propia cuenta y su propia sesión, en lugar de compartir una fila fija.
- Que ninguna pantalla ni endpoint de la aplicación responda a alguien sin sesión iniciada.
- Que las contraseñas queden almacenadas de forma que un volcado de la base no las revele.
- Que un atacante no pueda probar contraseñas contra un email de forma indefinida, ni averiguar si
  un email está registrado.

## Functional Requirements

- FR-01: El sistema debe permitir crear una cuenta indicando email y contraseña. Origen: RF-01.
- FR-02: El sistema debe rechazar el alta cuando el email ya corresponde a una cuenta existente, dejando la cuenta original sin cambios. Origen: RF-01.
- FR-03: El sistema debe iniciar sesión cuando el email y la contraseña presentados corresponden a una cuenta existente, y rechazar el acceso en caso contrario. Origen: RF-02.
- FR-04: El sistema debe exigir una sesión iniciada para acceder a cualquier pantalla y a cualquier endpoint de la aplicación, con la única excepción de las pantallas y endpoints de alta y de inicio de sesión. Origen: RF-03.
- FR-05: El sistema debe permitir cerrar la sesión, dejando al usuario en la pantalla de inicio de sesión. Origen: RF-05.
- FR-06: El sistema debe resolver el usuario actual —el propietario que se asigna a cada movimiento que se escribe y el que acota cada consulta que se lee— al usuario de la sesión iniciada, en lugar de a la fila semilla fija. Origen: RF-04 (parcial; los criterios de aislamiento se verifican en `01b`).
- FR-07: El sistema debe eliminar de la base de datos, mediante la migración que introduce las cuentas, la fila de usuario semilla y todos los movimientos asociados a ella. Origen: decisión del 2026-08-20 registrada en `concept-DISC-001.md`.

## Non-Functional Requirements

- NFR-01: El sistema debe almacenar cada contraseña como un hash bcrypt o argon2, nunca en texto plano ni con cifrado reversible, de modo que el valor almacenado no permita recuperar la contraseña original. Origen: RNF-03.
- NFR-02: El sistema debe expirar la sesión tras 24 h sin actividad, exigiendo autenticarse nuevamente a partir de ese momento. Origen: RNF-04.
- NFR-03: El sistema debe responder al alta y al inicio de sesión con el mismo mensaje y el mismo código, esté el email registrado o no, de modo que la respuesta no permita determinar si existe una cuenta con ese email. Origen: RNF-05 (parcial; el límite de intentos es `01b`).

## Acceptance Criteria

> **AC-09 es el único criterio de migración de este PRD.** Se verifica una sola vez, aplicando la
> migración sobre una base que todavía contiene la fila semilla, y después nunca vuelve a ser
> observable: una vez borrada la semilla, no hay estado inicial que reproduzca el escenario. El
> resto de los criterios describe comportamiento permanente y se verifica en cada corrida. La spec
> tiene que tratarlos distinto — el de migración va contra la migración, con su propia base de
> partida, y no contra la API.

- AC-01 (FR-01): WHEN una persona completa un email no registrado y una contraseña y confirma el alta, THE sistema SHALL crear la cuenta y SHALL permitir iniciar sesión con esas mismas credenciales.
- AC-02 (FR-02): IF el email indicado en un alta ya corresponde a una cuenta existente, THEN THE sistema SHALL rechazar el alta, SHALL dejar en una la cantidad de cuentas con ese email, y SHALL no modificar la contraseña de la cuenta existente.
- AC-03 (FR-03): WHEN un usuario registrado presenta su email y su contraseña correctos, THE sistema SHALL iniciar la sesión y SHALL llevarlo a la pantalla principal.
- AC-04 (FR-03): IF las credenciales presentadas no corresponden a ninguna cuenta, o la contraseña no corresponde al email indicado, THEN THE sistema SHALL rechazar el acceso y SHALL no iniciar ninguna sesión.
- AC-05 (FR-04): IF alguien sin sesión iniciada solicita una pantalla o un endpoint de la aplicación distinto de los de alta e inicio de sesión, THEN THE sistema SHALL denegar la operación, SHALL no ejecutar su efecto, y SHALL llevarlo a la pantalla de inicio de sesión.
- AC-06 (FR-05): WHEN un usuario con sesión iniciada cierra sesión, THE sistema SHALL llevarlo a la pantalla de inicio de sesión, y un intento posterior de acceder a una pantalla de la aplicación SHALL volver a exigir autenticación.
- AC-07 (FR-06): WHEN un usuario con sesión iniciada registra un movimiento, THE sistema SHALL asignarle como propietario al usuario de esa sesión.
- AC-08 (FR-06): WHEN un usuario con sesión iniciada abre el listado y el resumen, THE sistema SHALL calcularlos únicamente sobre los movimientos cuyo propietario es el usuario de esa sesión.
- AC-09 (FR-07) — **criterio de migración, no de comportamiento**: WHEN se aplica la migración que introduce las cuentas sobre una base que contiene la fila semilla y sus movimientos, THE sistema SHALL dejar la tabla de movimientos sin ninguna fila asociada a la semilla y SHALL dejar la tabla de usuarios sin la fila semilla.
- AC-10 (NFR-01): WHEN se inspecciona en la base de datos el registro de una cuenta recién creada, THE sistema SHALL mostrar en el campo de contraseña un valor con el formato de un hash bcrypt o argon2, y SHALL no mostrar la contraseña en texto plano.
- AC-11 (NFR-01): WHEN dos cuentas distintas se crean con la misma contraseña, THE sistema SHALL almacenar dos valores de hash distintos.
- AC-12 (NFR-02): IF una sesión permanece más de 24 h sin actividad y su usuario solicita entonces una pantalla de la aplicación, THEN THE sistema SHALL denegar la operación y SHALL exigir autenticarse nuevamente.
- AC-13 (NFR-03): WHEN se compara la respuesta a un intento de inicio de sesión sobre un email registrado con la respuesta sobre un email no registrado, ambos con contraseña incorrecta, THE sistema SHALL devolver el mismo mensaje y el mismo código de estado en los dos casos.

## Out of Scope

- **Límite de intentos fallidos de inicio de sesión** (RNF-05 de PRD-001, la parte del bloqueo): es `01b`. Este PRD deja el login construido y NFR-03 ya impide distinguir un email registrado de uno que no lo está; lo que falta es el conteo de fallos y la ventana de 15 minutos.
- **Verificación del aislamiento entre cuentas con dos usuarios reales** (AC-06..AC-08 de PRD-001): es `01c`. Este PRD deja la costura construida —FR-06— y `01c` la verifica y cierra sus huecos.
- Recuperación de contraseña olvidada y cambio de contraseña: fuera de alcance en PRD-001.
- Verificación del email por correo, confirmación de alta o cualquier envío de mail.
- Inicio de sesión con proveedores externos (Google, etc.), segundo factor y "recordarme".
- Roles, permisos o cualquier distinción entre tipos de usuario: todas las cuentas son iguales.
- Perfil de usuario, cambio de email y baja de cuenta.
- Categorías propias por usuario: es el PRD 03, que depende de este.
- Rehabilitar antes de tiempo un email bloqueado por el límite de intentos: es de `01b`, y allí también queda fuera.

## Risks and Mitigations

- **Riesgo: entre `01a` y `01c` el aislamiento queda heredado pero no verificado.** Al cambiar FR-06, las lecturas quedan acotadas por el filtro global de EF que ya existe, así que el aislamiento *probablemente* funcione desde el minuto uno — pero nadie lo habrá comprobado con dos cuentas reales. → Mitigación: `01b` y `01c` son los dos tickets siguientes, sin nada de otro tema intercalado, y la aplicación no se expone a usuarios reales hasta que los tres estén en `main`.
- **Riesgo: el filtro global de EF protege las lecturas, no las escrituras.** No aplica a INSERT. Un movimiento escrito sin asignar el propietario a mano queda huérfano o mal atribuido, y ningún filtro lo detecta. → Mitigación: AC-07 lo verifica explícitamente sobre la escritura, no sobre la lectura.
- **Riesgo: mientras `01b` no esté, un atacante puede probar contraseñas contra un email sin límite.** Este PRD entrega login sin ninguna protección de fuerza bruta. → Mitigación: `01b` es el ticket inmediatamente siguiente, y la aplicación no se expone a usuarios reales hasta que esté. El riesgo es real y acotado a la ventana entre los dos tickets; se registra para que esa ventana sea una decisión y no un olvido.
- **Riesgo: el hashing exige una dependencia nueva** — .NET no trae bcrypt ni argon2 en su biblioteca estándar. → Mitigación: `AGENTS.md` pide justificar toda dependencia nueva en la spec; la justificación es NFR-01, y la alternativa (implementar el hashing a mano) es peor por definición. La elección concreta se registra como ADR en el PLAN.
- **Riesgo: descartar los datos semilla borra lo que se haya cargado probando.** → Mitigación: aceptado de forma explícita el 2026-08-20. Son datos de desarrollo; quien quiera conservarlos hace un volcado antes de aplicar la migración.
- **Riesgo: una sesión que expira en medio del trabajo puede leerse como un error de la aplicación.** → Mitigación: AC-12 exige que el sistema lleve a la pantalla de inicio de sesión, no que falle en silencio.

## Dependencies

- FEAT-001a, FEAT-001b y FEAT-001c mergeados en `main`: el esquema de movimientos, la API y el frontend sobre los que se monta la sesión.
- La abstracción `IUsuarioActual`, que es el único punto donde hoy se resuelve el propietario y el único que FR-06 reemplaza.
- MySQL 8.4.10 disponible para persistir las cuentas.
- Una dependencia externa de hashing (bcrypt o argon2), a elegir y justificar en el PLAN.
- Los tres ítems de deuda de infraestructura (D-1 linter del backend, D-2 Vitest sin `typecheck`, D-3 fixture de rendimiento), que por decisión del usuario van antes que este PRD.
