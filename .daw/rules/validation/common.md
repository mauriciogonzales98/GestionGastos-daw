---
applyTo: '**'
version: 1.0.1
---

# Validación — Reglas transversales

> Cárgalo SIEMPRE que se ejecute una skill de validación, junto con el archivo de su familia:
> `prd.md`, `spec.md`, `threat.md`, `sast.md` o `verify.md`.

> **Requirement identifiers.** Functional requirements are `FR-xx`, non-functional requirements are
> `NFR-xx`, acceptance criteria are `AC-xx`. These prefixes are what the rules match on — keep them
> as written even when the PRD's prose is in another language.

## Guiding Principle: FAIL vs WARNING

```
FAIL = The absence of this element affects the artifact's coverage,
       completeness, correctness, traceability or security.
       → Blocks progress. Must be resolved before continuing.

WARNING = The artifact serves its purpose without this element, but
          could be improved in quality, clarity or maintainability.
          → Reported, does not block. Stays as an optional improvement.

When in doubt → FAIL.
A false FAIL costs one conversation.
A false WARNING costs rework, bugs or security incidents.
```

### What can NEVER be a WARNING

- A PRD requirement with no coverage in the spec.
- An acceptance criterion with no test.
- An attack surface with no mitigation.
- A confirmed security vulnerability (any severity ≥ Medium).
- An undocumented security finding (not even false positives).

---

## Tier Modifier: QUICK-FIX

When `tier == "QUICK-FIX"`, the DEFINE artifact is a **4-line fix-brief**, not a PRD. This
catalog's rules apply with the following conditionality:

| Family | Behavior with QUICK-FIX |
|---|---|
| `F-PRD-*` / `W-PRD-*` (PRD validation) | **Do not apply.** `daw-validate-prd` for QUICK-FIX requires ONLY the fix-brief's 4 sections: **Bug**, **Change**, **Regression test**, **Risk**. If all 4 are present and non-empty → PASS. |
| `F-SPEC-*` / `W-SPEC-*` (spec/fix-plan) | **Do not apply.** QUICK-FIX has no PLAN phase, so it has no spec, no rollback plan and no RCA — it is too small for any of the three to be worth writing. The regression test is declared in the fix-brief (the "Regression test" field). |
| `F-TM-*` / `W-TM-*` (threat model) | **Skipped** if the diff does NOT touch security-sensitive paths (backed by the shared gate's QUICK-FIX scope guard, which is a path/LOC denylist, not a proof that no attack surface exists; SAST runs regardless). They are replaced by the automatic entry: "No new attack surface — diff confined to `{paths}`, ≤ 10 LOC, no auth/API/schema/input-validation". |
| `F-SAST-*` (SAST) | **Apply in full.** SAST is QUICK-FIX's only security validation and it is a blocking gate (the `sast` gate). It is NOT relaxed. |
| `F-VER-*` (verify-module) | **Do not apply.** QUICK-FIX has no VERIFY phase. |

Guiding rule: QUICK-FIX reduces validation noise (it does not demand NFRs, an "Out of Scope"
section, or PRD↔spec traceability for a typo) **without** touching the security floor: a regression
test + a clean SAST remain mandatory.

---

## Validation Report Format

Every validation skill must produce a report in this format:

```
┌─────────────────────────────────────────────────────────────┐
│  /daw-validate-[type] [artifact] — [PASSED | FAILED]         │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  [Section 1: category name]                                  │
│    ✅ [ID]: [check description] (if it passed)               │
│    ❌ [ID]: [check description] (if it failed)               │
│       → [explanation of what is missing and how to fix it]   │
│    ⚠️  [ID]: [check description] (if warning)                 │
│       → [improvement suggestion]                             │
│                                                              │
│  [Section 2: category name]                                  │
│    ...                                                       │
│                                                              │
│  ────────────────────────────────────────────────────────────│
│  Total: [N] passed, [M] failed, [K] warnings                 │
│  Result: [PASSED if M == 0 | FAILED if M > 0]                │
│  Next: [what to do based on the result]                      │
└─────────────────────────────────────────────────────────────┘
```

**Result rule:**
- If there is at least 1 FAIL → result = **FAILED**. Gate blocked.
- If there are 0 FAILs → result = **PASSED** (with or without warnings).
- Warnings are reported but never block.
