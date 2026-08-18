---
applyTo: '**'
version: 1.0.1
---

# 1. Validación de PRD (`daw-validate-prd`)

> Parte del catálogo de validación de DAW. Las reglas transversales (FAIL vs WARNING,
> modificador QUICK-FIX, formato del informe) están en `.daw/rules/validation/common.md`:
> cárgalo junto con este archivo.

**Applies in:** the DEFINE phase.
**Basis:** IEEE 830 (Software Requirements Specification), INVEST (Independent, Negotiable,
Valuable, Estimable, Small, Testable), EARS (Easy Approach to Requirements Syntax).

### FAIL rules

| ID | Check | Precise description | Basis |
|---|---|---|---|
| F-PRD-01 | FR with no AC | Every functional requirement (FR-xx) must have at least one acceptance criterion (AC-xx) validating it. If an FR has no linked AC → FAIL. | IEEE 830 §4.3.1: every requirement must be verifiable. INVEST: Testable. If you cannot verify it, you cannot ship it. |
| F-PRD-02 | Non-binary AC | Every acceptance criterion must be evaluable as PASS or FAIL without ambiguity. It must follow the pattern: "Given [context], when [action], then [measurable result]". If an AC says "works correctly", "behaves appropriately" or uses any unmeasurable adjective → FAIL. | BDD: acceptance criteria are verifiable contracts. An ambiguous AC produces divergent implementations. |
| F-PRD-03 | NFR with no metric | Every non-functional requirement (NFR-xx) must include a quantitative value. Valid examples: "< 500ms p95", "99.9% uptime", "≥ 80% coverage". Invalid examples: "fast", "secure", "scalable", "highly available". | IEEE 830 §3.4: NFRs must be measurable. "Fast" is not a requirement, it is a wish. Without a number there is no possible acceptance criterion. |
| F-PRD-04 | Empty "Out of Scope" | For tier FEATURE: the "Out of Scope" section must exist and contain at least one explicit item. If the section is missing or empty → FAIL. | The #1 cause of scope creep is not defining what is NOT included. Whatever is not explicitly excluded is assumed to be included. |
| F-PRD-05 | Non-unique IDs | Every FR, NFR and AC must have a unique identifier (FR-01, NFR-01, AC-01). If there are duplicate or missing IDs → FAIL. | Without unique IDs there is no traceability. You cannot link PRD → spec → test → code. |
| F-PRD-06 | Ambiguous verb | Requirements must use defined imperative verbs: "must", "must not". If a requirement uses "should", "could", "may", "ideally", "it is recommended" → FAIL. | IEEE 830 §3.1: "shall" for mandatory; "should" is forbidden in requirements because it creates contractual ambiguity. A requirement that "should" be met is a requirement that can be ignored. |
| F-PRD-07 | Undeclared dependencies | If an FR references another module, an external service, or an existing feature, that dependency must be listed in the "Dependencies" section. If there are undeclared cross-references → FAIL. | Undeclared dependencies cause implementation blockers and integration errors. |
| F-PRD-08 | Missing structural section | The PRD must contain ALL of these sections: Context and Problem, Goals, Functional Requirements, Non-Functional Requirements, Acceptance Criteria, Out of Scope (FEATURE), Dependencies. If any is missing → FAIL. | A structurally incomplete PRD cannot be validated. Missing sections are requirements nobody thought about. |
| F-PRD-09 | AC not in EARS form | Every acceptance criterion must match one of the five EARS patterns (see below). An AC that matches none → FAIL, quoting it and naming the pattern it most likely wants. **Does not apply to DISCOVERY**, whose PRDs are exploratory, or to QUICK-FIX, whose artifact is the 4-line fix-brief. | EARS (Easy Approach to Requirements Syntax, Rolls-Royce) turns a criterion into a shape a reader can check rather than a sentence they have to interpret. It is what AWS's Kiro adopted for spec-driven work with agents, for the same reason: a template makes an *absent* case visible, and free prose does not. |

### WARNING rules

| ID | Check | Precise description | Basis |
|---|---|---|---|
| W-PRD-01 | FR with no rationale | An FR has no context for why it exists. The requirement is clear, but the motivation is not explained. | IEEE 830 §2.4: rationale makes trade-off decisions easier. Does not block, because the requirement is executable without it. |
| W-PRD-02 | Too many ACs per FR | An FR has more than 5 acceptance criteria. It may indicate the requirement should be split. | INVEST: Small. Does not block, because it can be legitimate (a complex requirement), but it flags a possible granularity problem. |
| W-PRD-03 | Passive voice | A requirement uses the passive voice ("the data is validated" instead of "the system must validate the data"). The passive voice hides the responsible actor. | Clarity. Does not block, because the meaning may be inferable from context. |
| W-PRD-04 | No unwanted-behaviour AC | **No acceptance criterion uses the EARS unwanted-behaviour pattern** (`IF … THEN … SHALL`). Count them: zero → WARNING naming the FRs whose failure modes nobody wrote down. | Completeness, made checkable. Phrased as "an FR does not mention what happens if it fails" this was a judgement the reader had to make and could quietly not make; as a count of a pattern it is either there or it is not. Still does not block at PRD level — a feature can legitimately have no error path, and the rule that *does* block is F-SPEC-16, one phase later, where the answer is actionable. |
| W-PRD-05 | Empty "Risks and Mitigations" | The section exists but has no content, or does not exist. | Planning. Does not block, because technical risks are explored further in the spec and threat model. |

### The five EARS patterns (for F-PRD-09 and W-PRD-04)

| # | Pattern | Template | For |
|---|---|---|---|
| 1 | Ubiquitous | `THE <system> SHALL <response>` | Always true, no trigger |
| 2 | Event-driven | `WHEN <trigger>, THE <system> SHALL <response>` | A response to something happening |
| 3 | State-driven | `WHILE <precondition>, THE <system> SHALL <response>` | True for as long as a state holds |
| 4 | Optional feature | `WHERE <feature is included>, THE <system> SHALL <response>` | Only in variants that have it |
| 5 | **Unwanted behaviour** | `IF <trigger>, THEN THE <system> SHALL <response>` | **Faults, failures, errors, misuse** |

They compose: `WHILE <precondition>, WHEN <trigger>, THE <system> SHALL <response>`.

**Pattern 5 is the one that earns the notation its place here.** The other four describe what the
system does when things go as expected, and those get written without being asked for. The failure
cases are the ones that get left out — and in free prose, leaving them out looks exactly like a
requirement that has none. Given a template, their absence is countable.

```
AC-03  WHEN a ticket is submitted, THE system SHALL classify it and store the draft reply.
AC-04  IF the classifier is unavailable, THEN THE system SHALL queue the ticket and
       notify the operator within 30 seconds.
```

`SHALL` and not "should": F-PRD-06 already forbids the ambiguous verbs, and EARS uses the one it
demands.

