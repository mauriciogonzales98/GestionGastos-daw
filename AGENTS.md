# AGENTS.md — project context

> **DAW template.** Fill in the `[...]` with what is true of YOUR project and delete what does not
> apply. This file describes **the project**; **the process** is DAW's job (phases, gates, when to
> test, when to commit). Do not mix the two: process rules written here compete with the pipeline's.
>
> It is **tool-agnostic on purpose**: Claude Code reads it through the import in `CLAUDE.md`, Codex
> CLI, Copilot CLI, Cursor and OpenCode read it directly, and Gemini CLI gets it through
> `GEMINI.md`. The same file serves whichever tool you open the repo with — which is the point:
> porting the pipeline to another tool must not mean rewriting what your project is.

---

## Language

**Always respond in the language the user writes in.** Write every artifact you produce — PRDs,
specs, ADRs, reports, commit messages, status lines — in that same language, regardless of the
language these instructions are written in.

If this project has a fixed working language, state it here and use it instead:

> Working language: Spanish

---

## What this project is

App de registro y gestión de gastos e ingresos personales, cargados por formulario, con un dashboard para visualizarlos.

**Reference PRD:** `docs/daw/prd/PRD.md`

---

## Stack

**This is the only place the stack lives.** DAW reads it from here and generates no derived file.
Fill it in even if the repo is empty: without a stack there is nothing to plan or implement against.

If the repo already has code and this section is empty, DAW will detect the stack from your config
files and **propose the text for you to paste here**. You always confirm it.

| Field | Value |
|-------|-------|
| Language | Typescript para frontend, C# para backend |
| Runtime | Node 22.x + pnpm |
| Framework | React 19 + Vite, Node 22.x, .NET 10 (SDK 10.0.301), Entity Framework Core 9.0.18 + Pomelo.MySQL 9.0.0 |
| Database | MySQL 8.4.10 local, puerto 3306, schema `gestiongastos` |
| Test runner | xUnit en backend, Vitest en frontend |
| Linter / formatter | ESLint + Prettier |
| Package manager | pnpm |
| Install | `pnpm --dir frontend install --frozen-lockfile` |
| Lint | `pnpm --dir frontend lint` |
| Format | `pnpm --dir frontend format` |
| Typecheck | `pnpm --dir frontend exec tsc --noEmit` |
| Test (frontend) | `pnpm --dir frontend test` |
| Test (backend) | `dotnet test backend/` — requiere `ConnectionStrings__Default` apuntando a `gestiongastos_test` (ADR-002) |

---

## Architecture conventions

**DAW validates your code against this section** during the CODE phase, via `daw-validate-arch`.
Leave it empty and that validation has nothing to compare against, so it stops being worth running.

- **Folder structure:** frontend y backend separados en sus respectivas carpetas(`backend/` para el backend y `frontend/` para el frontend). 
- **Error handling:** typed errors; never a silent catch
- **Dependencies:** no new libraries without justifying them in the spec

---

## Code conventions

- La cadena de conexión va en user-secrets, nunca en `appsettings`
- No `any`. If it is unavoidable, it comes with a comment explaining why.
- Comments only when the *why* is not obvious from the code

---

## What NOT to do in this project

This section is worth its weight in gold: it is where the scars go, the things that already went
wrong once.

- No guardar contraseñas en texto plano: deben almacenarse con hash seguro (bcrypt/argon2) (RNF-03).
- No commitear credenciales: ni en `appsettings*.json` ni en los `.sql` de `backend/db/`.

---

## Domain glossary

The terms specific to your product, so the agent uses them correctly instead of inventing synonyms.

- **[Gasto]:** Es dinero que salio de mi cuenta
- **[Ingreso]:** Es dinero que ingreso a mi cuenta

---

> ℹ️ **What does NOT belong in this file, because DAW provides it:** the order work happens in, when
> the spec gets written, when tests run, when to commit, what it takes to move between phases. All
> of that lives in `.daw/` and applies on its own.

<!-- BEGIN DAW (managed by DAW — do not edit by hand) -->
# DAW — Dilux Agentic Workflow

This repo uses **DAW**: an agent-driven development pipeline with the phases
`CLASSIFY → DEFINE → PLAN → CODE → VERIFY → RELEASE`.

Before answering, read `.daw/orchestrator.md` and run its Boot Sequence. It is a strict state
machine: it decides what you are allowed to do based on the phase recorded in `.daw-state.json`.

The project's own context — stack, architecture, domain — is elsewhere in this file. It lives here,
in `AGENTS.md`, and not in any one tool's file, on purpose: it is tool-agnostic and comes along
unchanged when the pipeline is ported to another agent.
<!-- END DAW -->
