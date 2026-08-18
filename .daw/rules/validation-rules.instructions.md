---
applyTo: '**'
version: 2.0.0
---

# Validation Rules — Índice del catálogo

Este catálogo está **partido por familia**: cada skill de validación carga solo su archivo, no el
catálogo entero. Los routers de fase NO cargan este índice — lo hace la skill cuando se invoca.

**Carga siempre `common.md` junto con el archivo de la familia.**

| Skill | Fase | Archivo de reglas |
|---|---|---|
| (todas) | — | `.daw/rules/validation/common.md` — FAIL vs WARNING, modificador QUICK-FIX, formato del informe |
| `daw-validate-prd` | DEFINE | `.daw/rules/validation/prd.md` |
| `daw-validate-spec` | PLAN | `.daw/rules/validation/spec.md` |
| `daw-threat-modeling` | PLAN | `.daw/rules/validation/threat.md` |
| `daw-security-sast` | CODE | `.daw/rules/validation/sast.md` |
| `daw-verify-module` | VERIFY | `.daw/rules/validation/verify.md` |

Las reglas se evalúan **mecánicamente**, sin margen de interpretación subjetiva. Cada regla tiene un
ID único, una severidad (FAIL o WARNING) y una base en estándares o buenas prácticas de la industria.

---

## Quantitative Summary

| Area | FAIL rules | WARNING rules | Total |
|---|---|---|---|
| PRD | 8 | 5 | 13 |
| Spec / Fix-Plan | 15 | 3 | 18 |
| Threat Model | 7 | 2 | 9 |
| SAST | 19 | 1 | 20 |
| Module Verify | 6 | 3 | 9 |
| **Total** | **55** | **14** | **69** |

---

## Operational Skills (no rules in this catalog)

The following skills perform operational checks (direct pass/fail) and have NO rules enumerated in
this catalog. Their behavior is defined in their respective instruction files:

| Skill | Phase | Behavior | Defined in |
|---|---|---|---|
| `daw-validate-arch` | CODE | Validates the project's architecture conventions. The rules depend on the target project (`AGENTS.md`). If it reports violations → BLOCKED. | `.daw/rules/code.instructions.md` |
| `daw-test` | CODE | Runs the test suite. If it fails → BLOCKED. It is a runner, not an artifact validator. | `.daw/rules/code.instructions.md`, `.daw/rules/testing.instructions.md` |

These skills are operational gates: they run and report pass/fail. Test quality rules (coverage,
traceability, sad paths) are evaluated in the VERIFY phase by `daw-verify-module` (`.daw/rules/validation/verify.md`).

---
