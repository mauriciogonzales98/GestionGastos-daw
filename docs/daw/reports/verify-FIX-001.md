# Verificación FIX-001

## Ronda 1: BLOCKED

| Field | Value |
|-------|-------|
| Ticket | FIX-001 |
| Tier | FIX |
| Ronda | 1 |
| Fecha | 2026-08-20 |
| Resultado | **BLOCKED** — 2 FAIL, 6 WARN, 15 PASS |
| Verificador | `daw-module-verifier`, sobre código que no escribió |

## Resultado

```
FAILs: 2 | WARNs: 6 | PASSes: 15  →  BLOCKED
```

La implementación **funciona**: build en 0 warnings y 0 errors con `-warnaserror`,
`dotnet format --verify-no-changes` en 0, suite 247/247, SAST sin vulnerabilidades. Lo que falla no
es que el linter no ande. Es que **nada en el repositorio comprueba que ande**.

## FAIL-1 (F-VER-06) — El test de regresión no existe como algo repetible

**Bloqueante.** El fix-plan promete, como primer ítem de su sección "Tests"
(`docs/daw/specs/fix-FIX-001.md:170-173`), un test de regresión que reproduzca el defecto original:
una violación deliberada de una regla activa **falla** el build después del fix y **compilaba en
silencio** antes.

Ese test **no está en el repositorio en ninguna forma ejecutable**: ni caso de xUnit, ni script, ni
paso de CI. `grep -ril` sobre `backend/` y `.github/` no encuentra nada.

Lo único que existe es la **narración en prosa** de una comprobación manual, repetida en el mensaje
del commit `ca8fa1d` y en `docs/daw/security/sast-FIX-001.md:97-105`. Durante CODE se creó un
archivo con una violación de CA1822, se confirmó que rompía el build, y se borró.

**Por qué es un FAIL y no un WARN.** Este ticket entrega exactamente una cosa: una barrera. Su
defecto original era la ausencia de señal. Verificar la barrera una vez a mano y borrar la evidencia
deja el ticket en el mismo estado que denuncia: si mañana alguien pone `EnforceCodeStyleInBuild` en
`false`, o borra `Directory.Build.props`, **el build sigue verde y ningún test se pone en rojo**. La
barrera se puede desarmar en silencio, que es literalmente la causa raíz descrita en el RCA.

Arrastra además la **contra-prueba de la mitigación R-05** del threat model
(`fix-FIX-001.md:179-181`), también prometida como test y también solo comprobada a mano.

**Compromete:** AC-03 (el comando falla identificando archivo, línea y regla), AC-04 (una migración
de EF no produce hallazgos) y AC-10 (un PR con un incumplimiento rompe el CI).

## FAIL-2 (F-VER-02) — Los 12 checkboxes del fix-plan siguen sin marcar

`docs/daw/specs/fix-FIX-001.md` tiene **12 checkboxes `[ ]` y 0 `[x]`**. Ninguno se cerró.

**Con una tensión del método que corresponde dejar anotada, no resolver por la vía de los hechos:**
el orquestador prohíbe modificar la spec en las fases CODE, VERIFY y RELEASE. Los checkboxes de un
fix-plan, por lo tanto, **no se pueden marcar nunca** después de PLAN, y sin embargo F-VER-02
pregunta si están marcados. No se tocó el archivo para no violar la prohibición. Queda registrado
como errata del método, no como algo que este ticket pueda arreglar desde acá.

## WARNINGs registrados

- **W-1 · Alcance real de CA1725 y CA1050 más ancho que el declarado.** El paso 2 del fix-plan dice
  "en producción", pero las dos supresiones están bajo la sección `[*.cs]`, que es global y alcanza
  también al proyecto de tests. Hoy no hay diferencia observable —Tests no dispara esas reglas— y
  ninguna es de categoría Security ni Reliability, así que no viola R-02. Pero el archivo hace algo
  más ancho que lo que el plan aprobó, y eso se corrige.
- **W-2 · AC-04 verificado por inferencia.** No se generó una migración nueva con
  `dotnet ef migrations add`; se comprobó que la migración existente no produce hallazgos. La
  pregunta del AC es sobre una migración **futura**.
- **W-3 · AC-11 medido sobre el paso aislado.** Se midieron 12 s del comando de formato en local,
  no la diferencia de duración del pipeline completo con y sin el paso, que es lo que pide el AC.
  El margen contra los 60 s es amplio, así que el riesgo es bajo.
- **W-4 · AC-10 sin corrida real de CI.** No hay evidencia de un PR con una violación deliberada
  fallando en GitHub Actions, solo la corrida local equivalente.
- **W-5 · Desvío del paso 3 sin trazar contra el plan en disco.** El mecanismo de CA1861 cambió de
  "corregir con `static readonly`" a "suprimir en `.editorconfig` para todo el proyecto de tests".
  Está documentado en el commit y en el propio `.editorconfig`, y es válido bajo AC-12, pero el
  fix-plan sigue diciendo "corregir los 26 hallazgos activos". Misma imposibilidad que FAIL-2: la
  spec no se puede tocar desde acá.
- **W-6 · La cuenta del plan decía 26 y los hallazgos activos eran 25.** El vigésimo sexto era el
  CA1711 que el propio paso 2 suprime, así que nunca llegó a estar activo.

## Lo que sí pasó (15)

Pasos 1, 2, 4 y 5 del fix-plan implementados con evidencia; AC-01, AC-02, AC-05, AC-06, AC-07,
AC-08, AC-09 y AC-12 verificados; F-VER-05 (build y formato limpios) confirmado ejecutando los dos
comandos; las 17 supresiones con su motivo escrito, **ninguna de categoría Security ni Reliability**
—R-02 del threat model se sostiene—; y W-VER-03 sin tests frágiles: los cambios en tests son firmas
de retorno de helpers `private` y atributos de supresión.

## Acción

**Bucle correctivo de vuelta a CODE.** No se parchea código en VERIFY.

Lo que hay que construir en la ronda 2:

1. Una verificación **repetible** de la barrera, que corra en local y en el CI, y que cubra las dos
   direcciones que hoy solo se comprobaron a mano: una violación deliberada rompe el build fuera de
   `Migrations/`, y no lo rompe dentro.
2. Acotar CA1725 y CA1050 al proyecto de producción, como decía el plan (W-1).

Los WARNINGs W-2 a W-6 quedan registrados y no bloquean.

---

## Ronda 2: PASSED

| Field | Value |
|-------|-------|
| Ticket | FIX-001 |
| Tier | FIX |
| Ronda | 2 (tras el bucle correctivo de vuelta a CODE) |
| Fecha | 2026-08-21 |
| Resultado | **PASSED** — 0 FAIL, 5 WARN, 22 PASS |
| Verificador | `daw-module-verifier`, sobre código que no escribió |

```
FAILs: 0 | WARNs: 5 | PASSes: 22  →  PASSED
```

La ronda 1 no falló porque el linter no anduviera: falló porque **nada en el repositorio comprobaba
que anduviera**. Lo que cierra esta ronda es exactamente eso — la barrera ahora tiene quien la
vigile, y esa vigilancia corre en cada PR.

### Evidencia ejecutada en esta ronda

| Comando | Resultado |
|---|---|
| `dotnet build backend/GestionGastos.sln -warnaserror --no-incremental` | 0 Warning(s), 0 Error(s) — 10,4 s |
| `dotnet format backend/GestionGastos.sln --verify-no-changes` | exit 0 — 11,2 s |
| `./backend/verificar-linter.sh` | exit 0, los 3 chequeos ok, árbol de trabajo limpio después |
| `dotnet test backend/GestionGastos.sln` | 142/142 |
| `pnpm --dir frontend test` | 105/105 → **247/247** |
| `pnpm --dir frontend exec tsc --noEmit` · `pnpm --dir frontend lint` | exit 0 · exit 0 |

Ningún archivo de `backend/GestionGastos.Api/**.cs` fue tocado en toda la rama: los 15 hallazgos de
producción se resolvieron todos por configuración, como el fix-plan había previsto.

### FAILs de la ronda 1 — estado

- **FAIL-1 (F-VER-06) — CERRADO.** `backend/verificar-linter.sh` existe, es ejecutable, y está
  enchufado como último paso del job `backend` de `.github/workflows/ci.yml` (commit `a99724f`).
  Verifica las **dos direcciones** que el fix-plan prometía y que la ronda 1 solo tenía como
  narración en prosa de una comprobación manual borrada: una violación deliberada de CA1822 en
  `GestionGastos.Api/Common/` **rompe** el build, y la misma violación dentro de `Migrations/`
  **no** lo rompe —contra-prueba de la mitigación R-05 del threat model—. Se probó además que el
  script falla de verdad: con `EnforceCodeStyleInBuild` en `false` sale 1. Con esto quedan
  verificados AC-03, AC-04 y AC-10, que la ronda 1 daba por comprometidos.
- **FAIL-2 (F-VER-02) — reclasificado como errata del método, no como FAIL.** El orquestador
  prohíbe modificar la spec en CODE, VERIFY y RELEASE, así que un fix-plan **no tiene forma de
  cerrar sus propios checkboxes** después de PLAN. Se evaluó la **sustancia** de los 12 ítems
  contra el repositorio en vez del estado del checkbox: 10 completos, 2 parciales (W-2 y W-3, sin
  agravarse), 0 sin sustancia. La tensión del método queda anotada acá para que la vea quien
  revise la regla, no resuelta por la vía de los hechos.

### Trazabilidad AC → implementación → verificación (F-VER-01)

| AC | Implementación | Verificación |
|---|---|---|
| AC-01 | `Directory.Build.props` + `.editorconfig` | `dotnet format --verify-no-changes` exit 0, árbol limpio |
| AC-02 | ídem | build 0 hallazgos, exit 0 |
| AC-03 | `backend/verificar-linter.sh` paso 2/3 | ✅ **resuelto en esta ronda** |
| AC-04 | `.editorconfig` `[GestionGastos.Api/Migrations/*.cs]` | ⚠️ paso 3/3 del script, aún por inferencia (W-2) |
| AC-05 | `[GestionGastos.Api.Tests/**.cs]` CA1707 = none | build sin CA1707 |
| AC-06 | `[GestionGastos.Api/**.cs]` CA1725/CA1050 = none | ✅ alcance corregido (W-1) |
| AC-07 | 8 correcciones mecánicas en tests | 247/247, sin cambios de comportamiento esperado |
| AC-08 | comentario adyacente a cada supresión | leído en `.editorconfig` |
| AC-09 | `AGENTS.md`, tabla Stack | fila `Lint (backend)` presente, coletilla eliminada |
| AC-10 | paso «Barrera del linter» en `ci.yml` | ✅ **resuelto en esta ronda** |
| AC-11 | paso `Formato` en `ci.yml` | ⚠️ 11,2 s medidos en aislado, no como delta del pipeline (W-3) |
| AC-12 | desvío registrado en `ca8fa1d` y en `.editorconfig` | 8 corregidas, 17 suprimidas con motivo, ninguna Security/Reliability |

### Pasos del fix-plan (F-VER-02, por sustancia)

Los 5 implementados con evidencia en disco: paso 1 `Directory.Build.props`; paso 2 `.editorconfig`
con 5 supresiones por id exacto —ninguna por categoría ni comodín, ninguna de Security ni
Reliability— y la exclusión acotada a `GestionGastos.Api/Migrations/`; paso 3 las correcciones y
supresiones con motivo; paso 4 los tres pasos de CI; paso 5 `AGENTS.md`.

### Calidad

- **F-VER-05** ✅ — build y formato limpios, ejecutados en esta ronda.
- **F-VER-03** ⬜ no aplica de forma significativa: el ticket no agrega lógica de negocio. Es
  configuración de build más 8 correcciones mecánicas de tipo declarado en tests existentes.
- **F-VER-04** ⬜ no aplica: no hay endpoint ni función que reciba entrada de usuario.
- **W-VER-01** ✅ sin código muerto ni imports sin usar en el diff de la ronda 2.
- **W-VER-03** ✅ `verificar-linter.sh` no depende de orden ni de estado global, y limpia sus
  archivos temporales con `trap … EXIT` en las dos ramas de salida.

### WARNINGs vigentes (5) — ninguno bloquea

- **W-1 — CERRADO.** CA1725 y CA1050 pasaron de `[*.cs]` a `[GestionGastos.Api/**.cs]`.
- **W-2 · AC-04 por inferencia.** El paso 3 del script simula un archivo dentro de `Migrations/`;
  no se generó una migración real con `dotnet ef migrations add`. Vigente, sin agravarse.
- **W-3 · AC-11 medido en aislado.** 11,2 s del comando contra un presupuesto de 60 s. Falta la
  diferencia de duración del pipeline completo con y sin el paso. Margen amplio, riesgo bajo.
- **W-4 · AC-10 sin corrida real de GitHub Actions.** Parcialmente mitigado: antes no existía el
  paso que correr, ahora existe y se probó localmente. Falta observarlo en rojo en un PR real.
- **W-5 · Desvío del paso 3 sin trazar contra el plan en disco.** CA1861 se suprimió en vez de
  corregirse con `static readonly`. Documentado en `ca8fa1d` y en `.editorconfig`, válido bajo
  AC-12; misma imposibilidad de tocar la spec que FAIL-2.
- **W-6 · La cuenta del plan decía 26 hallazgos activos y eran 25.** El vigésimo sexto era el
  CA1711 que el propio paso 2 suprime. Sin impacto.

### Seguridad

SAST ronda 2 PASSED, 0 vulnerabilidades y 0 supresiones nuevas (`docs/daw/security/sast-FIX-001.md`,
commit `257d943`). R-02 y R-05 del threat model revalidados después del cambio de alcance de W-1.

### Acción

**Gate `verify` cumplido.** Listo para pasar a RELEASE.
