# ADR-003: Construir sin autenticación detrás de `IUsuarioActual`

| Field | Value |
|-------|-------|
| Date | 2026-08-17 |
| Ticket | FEAT-001a |
| Status | Accepted |

## Context

FEAT-001a construye el registro y el listado de movimientos **sin autenticación**: ese es un ticket
posterior, separado deliberadamente para que este entregue algo utilizable antes. Pero cada
movimiento tiene un propietario, y el listado tiene que restringirse a él (FR-01).

La pregunta es cómo se obtiene "el usuario actual" cuando todavía no hay sesión, sin que enchufar la
autenticación después obligue a repasar cada consulta. El PRD lo declara como riesgo explícito.

## Options considered

### Option 1: interfaz `IUsuarioActual` + filtro global de consulta en el `DbContext`
- **Pros:** el propietario se obtiene de un único lugar y se aplica de un único lugar. El ticket de autenticación registra otra implementación y no toca ningún endpoint ni ninguna consulta. El filtro global (`HasQueryFilter`) hace que olvidar la restricción sea **imposible**, no improbable: no depende de que quien escriba la consulta número doce se acuerde.
- **Cons:** un filtro global es magia invisible: alguien que lea una consulta no ve por qué devuelve menos filas de las que espera. Y desactivarlo cuando haga falta (`IgnoreQueryFilters`) es fácil de hacer sin pensarlo.

### Option 2: pasar el `usuarioId` como parámetro y filtrar en cada consulta
- **Pros:** completamente explícito: cada consulta muestra su restricción en la línea. Nada oculto.
- **Cons:** es una regla que se cumple por disciplina. La consulta que se olvide de filtrar no falla, devuelve datos de otro — el peor modo de fallo posible, silencioso y con apariencia de éxito. Y son tres sub-tickets escribiendo consultas sobre la misma tabla.

### Option 3: constante con el id del usuario semilla
- **Pros:** trivial de escribir.
- **Cons:** no hay dónde enchufar la sesión después: habría que buscar y reemplazar cada uso. Es exactamente el riesgo que el PRD identifica.

## Decision

**Opción 1.** El argumento decisivo es el modo de fallo: olvidar un filtro explícito produce una fuga
de datos silenciosa, mientras que el filtro global convierte el olvido en algo que no puede pasar. Es
además la única opción donde el ticket de autenticación es un cambio de una línea de registración en
el contenedor.

`IUsuarioActual` se define con un miembro **asíncrono** desde el principio: la implementación resuelve
el usuario semilla consultando la base, y una propiedad síncrona obligaría a sync-over-async en el
camino caliente de los tres sub-tickets. Cambiar la firma después tocaría todos los endpoints de
FEAT-001b y FEAT-001c.

## Consequences

- Afecta: `backend/GestionGastos.Api/Common/IUsuarioActual.cs`, `UsuarioSemillaActual.cs`, `Data/AppDbContext.cs`.
- El usuario semilla es una **fila** de `usuarios` sembrada por la migración, no una constante en el código, con un email en TLD reservado (`dev@gestiongastos.local`) que no puede corresponder a nadie real.
- Se acepta que el filtro global es implícito. La contrapartida es un test que siembra un movimiento de otro propietario y verifica que no aparece (AC-02), que es lo que detectaría una desactivación accidental.
- `IgnoreQueryFilters` queda prohibido en el proyecto salvo justificación explícita en la spec del ticket que lo necesite.
- Los criterios AC-06, AC-07 y AC-08 de PRD-001 (aislamiento real entre usuarios) **no se pueden verificar** en este ticket: no hay dos usuarios que aislar. Quedan como criterios del ticket de autenticación, y esta es la deuda declarada de esta decisión.
