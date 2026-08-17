# ADR-001: Estructura de carpetas y estilo de la API

| Field | Value |
|-------|-------|
| Date | 2026-08-17 |
| Ticket | FEAT-001a |
| Status | Accepted |

## Context

FEAT-001a es el primer código del repositorio: no hay estructura previa que imitar. Lo que se decida
acá lo heredan FEAT-001b, FEAT-001c y todo lo que venga después, sin volver a discutirse.

Hay además una contradicción real en el contexto cargado: el `AGENTS.md` de este repo dice "frontend
y backend separados en sus respectivas carpetas" y menciona `backend/db/`, mientras que el
`AGENTS.md` del directorio padre documenta `dotnet run --project src/Api` y `npm install` en
`src/web`. Dos verdades vigentes sobre lo mismo.

## Options considered

### Option 1: `backend/` + `frontend/`, proyecto único con carpetas por feature, Minimal APIs
- **Pros:** coincide con `backend/db/`, la única ruta que el `AGENTS.md` del repo da por existente. Un proyecto con `Movimientos/`, `Categorias/`, `Common/` y `Data/` mantiene junto lo que cambia junto. Minimal APIs es lo idiomático en .NET 10 y evita la ceremonia de controllers, que en 4 endpoints no compra nada.
- **Cons:** un proyecto único no impone separación de capas: nada impide que un endpoint consulte el `DbContext` directamente. Si la aplicación crece, separar después cuesta.

### Option 2: `src/Api` + `src/web`, capas `Api`/`Domain`/`Infrastructure`, controllers
- **Pros:** ya está documentado en el `AGENTS.md` padre, con los comandos exactos. Las capas imponen la separación por compilación: `Domain` no puede referenciar `Infrastructure`. Los controllers son familiares para cualquiera que venga de .NET clásico.
- **Cons:** tres proyectos y una capa de mapeo para una app con tres tablas es estructura sin problema que resolver. Contradice `backend/db/` del `AGENTS.md` del repo, que es el archivo específico de este proyecto.

## Decision

**Opción 1.** El `AGENTS.md` del repo gana sobre el del padre por ser el específico del proyecto, y su
mención de `backend/db/` es la única evidencia de una convención ya pensada acá. Las capas por
proyecto se posponen: la separación que hoy hace falta es entre features, no entre capas, y un
proyecto único con carpetas por feature la da sin costo. Minimal APIs porque cuatro endpoints no
justifican la indirección de controllers.

La contradicción se resuelve **corrigiendo el `AGENTS.md` padre**, no ignorándolo: dejar dos
convenciones vigentes garantiza que alguna sesión futura tome la equivocada.

## Consequences

- Estructura: `backend/GestionGastos.Api/`, `backend/GestionGastos.Api.Tests/`, `backend/db/`, `frontend/`.
- Afecta: `backend/GestionGastos.sln`, todos los `.csproj`, y las secciones "Cómo correr" y "Folder structure" de ambos `AGENTS.md`.
- Se acepta que nada impide técnicamente que un endpoint use el `DbContext` directamente. El control es la revisión de arquitectura de la fase CODE, no el compilador.
- Si el proyecto crece hasta que la falta de capas duela, separar `Domain` e `Infrastructure` es un ticket de refactor con sus propios tests, no una migración de datos.
