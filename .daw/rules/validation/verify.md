---
applyTo: '**'
version: 1.0.1
---

# 5. Verificación de módulo (`daw-verify-module`)

> Parte del catálogo de validación de DAW. Las reglas transversales (FAIL vs WARNING,
> modificador QUICK-FIX, formato del informe) están en `.daw/rules/validation/common.md`:
> cárgalo junto con este archivo.

**Applies in:** the VERIFY phase.
**Basis:** requirements traceability, test coverage, the principle of independent verification.

### FAIL rules

| ID | Check | Precise description | Basis |
|---|---|---|---|
| F-VER-01 | AC with no passing test | **Every AC in the PRD must have at least one test validating it, and that test must be passing.** If an AC has no test, or the test exists but fails → FAIL. | The AC is the contract with the user. Without a test validating it, there is no evidence it works. |
| F-VER-02 | Spec task not implemented | **Every task/block in the spec, or step in the fix-plan, must be implemented.** If a whole block or a fix-plan step has no corresponding code → FAIL. | The spec is the approved plan. Partial implementation = an incomplete feature = a bug. |
| F-VER-03 | Test coverage below the minimum | **Line coverage ≥ 80%, branch coverage ≥ 80%, and function coverage ≥ 80%** over the new/modified code. If any is below → FAIL. | `.daw/rules/testing.instructions.md` defines these three minimums. Verify they are met, not just that "there are tests". |
| F-VER-04 | No sad-path tests | **Every endpoint or function accepting input must have at least one test with invalid input.** If there are only happy-path tests → FAIL. | The worst bugs live in edge cases and sad paths. Testing only the happy path tests only 20% of the real behavior. |
| F-VER-05 | Lint/type checker fails | If the project has a linter or type checker configured and there are errors → FAIL. | Lint/type errors indicate code that can fail at runtime. You cannot verify code that does not compile or pass lint. |
| F-VER-06 | Spec tests not implemented | **Every test listed in the spec must exist and pass.** If the spec says "test: creating a product with an empty name returns 400" and that test does not exist → FAIL. | The spec's tests are quality commitments the user approved. Not implementing them is breaching the spec. |

### WARNING rules

| ID | Check | Precise description | Basis |
|---|---|---|---|
| W-VER-01 | Dead code | Unused imports, unreferenced functions, declared-but-unused variables. | Cleanliness. Does not affect functionality but reduces maintainability. |
| W-VER-02 | Business-logic coverage < 90% | Core business logic (services, domain) has coverage between 80–90%. It does not fail (it is above the minimum) but it should be higher. | `.daw/rules/testing.instructions.md` recommends 90%+ for business logic. |
| W-VER-03 | Fragile test | A test depending on execution order, global state, or hardcoded values (timestamps, IDs). | Test maintainability. Does not block but causes future flakiness. |

