---
applyTo: '**'
version: 1.0.1
---

# 2. Validación de spec y fix-plan (`daw-validate-spec`)

> Parte del catálogo de validación de DAW. Las reglas transversales (FAIL vs WARNING,
> modificador QUICK-FIX, formato del informe) están en `.daw/rules/validation/common.md`:
> cárgalo junto con este archivo.

**Applies in:** the PLAN phase.
**Basis:** bidirectional traceability (IEEE 830 §2.6), OWASP ASVS (security in design), the
completeness principle (every design decision must be documented).

### 2.1 PRD → Spec coverage (FAIL = incomplete coverage)

| ID | Check | Precise description | Basis |
|---|---|---|---|
| F-SPEC-01 | FR with no coverage | **Every FR in the PRD must map to at least one block of the spec.** If an FR-xx from the PRD is not referenced in any block → FAIL. No exceptions. If the FR was deliberately deferred → it must first be removed from the PRD (with the user's approval), not ignored in the spec. | This is the fundamental rule of the PRD→Spec contract. An approved FR nobody implements is a broken promise. If the scope changed, the PRD must reflect it. |
| F-SPEC-02 | AC with no test | **Every AC in the PRD must map to at least one test described in the spec.** If an AC-xx has no associated test in any block → FAIL. | IEEE 830 verifiability. If no test is planned for an AC, that AC will not be verified. What is not verified, does not work. |
| F-SPEC-03 | NFR with no strategy | **Every NFR in the PRD must have a documented technical strategy in the spec.** If the PRD says "< 500ms p95" and the spec does not explain how that is achieved → FAIL. | NFRs with no strategy are discovered as problems in production. The strategy may be "the framework handles it by default", but it has to be written down. |

### 2.2 Spec → PRD traceability

| ID | Check | Precise description | Basis |
|---|---|---|---|
| W-SPEC-01 | Block with no FR | A block of the spec references no FR from the PRD. It may be gold-plating (unapproved scope) or a legitimate technical enabler. Report it for review. | Bidirectional traceability. A block with no FR may be necessary (infra, setup) but must be justified. WARNING because there are legitimate technical blocks with no direct FR. |

### 2.3 Per-block completeness

| ID | Check | Precise description | Basis |
|---|---|---|---|
| F-SPEC-04 | Block with no files | Every block must explicitly list which files are created and/or modified, with relative paths. If a block has no file list → FAIL. | A block with no files is not actionable. The implementer does not know where to work. |
| F-SPEC-05 | Block with no completion criterion | Every block must have a verifiable completion criterion (not "it is done" but "tests X, Y, Z pass and the endpoint returns 200"). If missing → FAIL. | Without a completion criterion, you cannot determine when a block is ready. Causes infinite "is it done yet?" loops. |
| F-SPEC-06 | Block with no tests | Every block must list at least one required test, with a description of what it validates. If a block has no tests → FAIL. | Code with no planned tests = code with no verification. Tests are not optional. |
| F-SPEC-07 | API with no contract | Every block that creates or modifies an endpoint must specify: HTTP method, path, request body/params (with types), response body (with types), error codes, and authentication/authorization requirements. If any is missing → FAIL. | An endpoint without a complete contract produces: trial-and-error integration, runtime type errors, and authentication vulnerabilities. OWASP ASVS V13: API Security. |
| F-SPEC-08 | Data model with no constraints | Every block that creates or modifies a model/schema must specify: entity name, fields with types, constraints (nullable, unique, FK, default), and indexes where applicable. If any is missing → FAIL. | A schema without constraints = data corruption. A field that "should" be unique but has no constraint will end up with duplicates. |
| F-SPEC-09 | Input with no validation | Every block that receives user input must specify validation rules: type, maximum length, format, allowed values. If the block accepts input and does not document validation → FAIL. | OWASP Top 10 A03 (Injection). Input with no documented validation = input with no implemented validation = a vulnerability. |
| F-SPEC-10 | No error handling | Every block must document which errors can occur and how they are handled (error code, message, action). If a block has no error-handling section → FAIL. | Undocumented error handling gets implemented ad hoc: every developer invents their own format, errors get swallowed, and the user sees inconsistent messages or stack traces. |
| F-SPEC-11 | Undocumented dependencies between blocks/steps | The spec (or fix-plan) must have a dependencies section stating which blocks (FEATURE) or steps (FIX) depend on which. If blocks or steps reference other blocks' entities/services with no declared ordering → FAIL. | Without a dependency order, parallel implementation causes merge conflicts and integration errors. |
| F-SPEC-16 | Documented error with no test | **Every error a block documents under F-SPEC-10 must appear in that block's test list (F-SPEC-06).** Count them: fewer tests naming an error condition than errors documented → FAIL, naming which ones are unaccounted for. | This is the same standard F-VER-04 already applies — every input path needs a sad-path test — moved to the phase where it can still be met. F-SPEC-10 makes the errors *written down*; without this rule nothing makes them *tested*, so a spec passes PLAN with a full error section and a happy-path-only test list. VERIFY then catches it two phases later, when the code exists and the test can no longer be written first: a test added to cover an error path that already works documents the status quo, which is exactly what Rule #-1 in `testing.instructions.md` says proves nothing. The gap is not in CODE. It is here. |

### 2.4 Spec ↔ PRD consistency

| ID | Check | Precise description | Basis |
|---|---|---|---|
| F-SPEC-12 | Contradicts the PRD | The spec must not contradict any requirement in the PRD. If the PRD says "response < 200ms" and the spec designs a synchronous chain of 5 external calls with no cache → FAIL (the design cannot meet the requirement). | The spec is the HOW for the PRD's WHAT. If the HOW cannot satisfy the WHAT, one of the two has to change — you do not ignore the contradiction. |
| F-SPEC-13 | Inconsistent terminology | The spec must use the same terminology as the PRD. If the PRD says "Product" and the spec says "Item" or "Article" for the same entity → FAIL. | Divergent terminology causes divergent implementations. If the name changes, there must be an explicit mapping. |

### 2.5 Fix-Plan (tier FIX)

| ID | Check | Precise description | Basis |
|---|---|---|---|
| F-SPEC-14 | Fix with no regression test | Every fix-plan must include at least one test reproducing the original bug. If there is no regression test → FAIL. | A fix with no regression test will break again. The test must fail BEFORE the fix and pass AFTER. Standard practice in every engineering team. |
| F-SPEC-15 | Fix-plan with no rollback plan | For tier FIX: the fix-plan must include rollback steps and the indicators for when to apply them. If reverting really is trivial, the section still has to exist and say so explicitly. Absent → FAIL. | A fix that reaches production with no thought about reverting it can end up worse than the defect. Standard in incident management (ITIL, Google SRE). If a change is too small to deserve a rollback plan, it is a QUICK-FIX, not a FIX. |

### 2.6 Spec warnings

| ID | Check | Precise description | Basis |
|---|---|---|---|
| W-SPEC-02 | Large block | A block modifies more than 5 files or has more than 500 words of description. It may indicate it should be split. | Maintainability. Does not block, because some blocks (refactoring, infra) legitimately touch many files. |
| W-SPEC-03 | No rollback (FEATURE) | For tier FEATURE: the spec includes no rollback or reverse-migration considerations. Does not apply if there are no schema changes or data migrations. | Good practice. Not always necessary for pure features with no migration. |

