# Verificación FIX-001 — Ronda 1: BLOCKED

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
