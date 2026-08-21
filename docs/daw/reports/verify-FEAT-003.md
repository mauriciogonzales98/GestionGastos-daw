# Verificación FEAT-003

## Ronda 1: BLOCKED

| Field | Value |
|-------|-------|
| Ticket | FEAT-003 |
| Tier | FEATURE |
| Ronda | 1 |
| Fecha | 2026-08-21 |
| Resultado | **BLOCKED** — 4 FAIL, 5 WARN, 12 PASS |
| Verificador | `daw-module-verifier`, sobre código que no escribió |

```
FAILs: 4 | WARNs: 5 | PASSes: 12  →  BLOCKED
```

**Contexto que hace esta ronda más relevante que de costumbre:** el implementador delegado se cortó
por límite de sesión tras escribir sólo el archivo de tests del Block 1, así que el orquestador
escribió los cuatro bloques restantes. Una verificación por alguien que no escribió el código valía
acá más que en un ticket normal, y encontró cosas que la autorrevisión no había visto.

La implementación **funciona**: la barrera detecta el rename coherente que antes no detectaba nadie,
270 tests en verde, 0 dependencias nuevas, ningún archivo de producción tocado. Lo que falla es la
completitud de la verificación de la propia barrera.

## FAIL-1 (F-VER-01) — AC-09 sin test que lo valide

AC-09 pide medir la diferencia de duración del pipeline **con y sin** el paso, sobre corridas reales.
Lo único que existe es la medición local del script: 44,46 s contra un techo de 90 s.

**Y es estructuralmente imposible de tomar en VERIFY.** El workflow sólo se dispara con
`pull_request` contra `main`, y el PR pertenece a RELEASE. No es un olvido: es que el criterio, tal
como está escrito, no se puede satisfacer en la fase donde se lo verifica.

`common.md` es terminante: *"An acceptance criterion with no test [...] can NEVER be a WARNING"*.
Y el precedente pesa: en FIX-001 este mismo hueco quedó anotado como W-3 y W-4, no se cerró, y
terminó costando el ticket FIX-002 entero cuando la primera corrida real de CI encontró lo que
ninguna medición local podía encontrar.

**Es un problema del criterio, no de la implementación**, y por eso necesita una decisión del usuario
en vez de más código.

## FAIL-2 (F-VER-06) — Un test que la spec exige no existe

El Block 5 de la spec lista como *Required test*: *"Una corrida sin MySQL falla con el mensaje del
fixture y no con uno de contrato"*. Verificado por grep: no hay ninguna referencia a
`CadenaHaciaUnPuertoCerrado` en `Contrato/`, y `ManejoDeErroresTests.cs` es anterior al ticket.

No se escribió. Es el error documentado bajo F-SPEC-10 del Block 5 y su sad path correspondiente
—distinguir "la base no responde" de "el contrato no coincide"— quedó sin cubrir.

## FAIL-3 — Una rama de error del parser, sin ejercitar

`LectorDeTiposDelFrontend.VerificarQueLosTiposReferenciadosExisten` (líneas 257-283) lanza cuando un
campo referencia un tipo que no existe. **Ningún test la ejercita**, confirmado por grep sobre los 6
`[Fact]` del bloque.

**Precisión sobre cómo se reportó, que no cambia el veredicto:** el commit `5df6d82` dice *"se agrega
una verificación que la spec no pedía"*, no *"se agrega un test"*, así que no se declaró un test
inexistente. Pero el hueco es real y es el que cuenta: se agregó una rama de error a un componente
cuya única virtud es ser estricto, y se la dejó sin comprobar. En un ticket cuya tesis es que **lo
que no se comprueba no cuenta como verificado**, eso no puede pasar.

El verificador identificó además otras dos ramas del parser sin ejercitar: una construcción no
reconocida **fuera** de una interfaz, y un literal inválido dentro de una unión.

## FAIL-4 (F-VER-03) — Cobertura no medible sobre el código nuevo

El código nuevo vive en el proyecto de tests, y coverlet no lo instrumenta: el XML de cobertura sólo
reporta clases de `GestionGastos.Api`. No hay número que citar.

Con una rama crítica confirmada sin ejercitar (FAIL-3), y siendo la regla del catálogo *"ante la duda
→ FAIL"*, corresponde FAIL. Vale registrar que es una consecuencia del diseño elegido —poner la
verificación en el proyecto de tests fue lo que la hizo costar 0 dependencias— y no un descuido de la
implementación.

## WARNINGs registrados

- **W-1 · AC-08 sin corrida real de PR.** El paso de CI existe y el mecanismo subyacente está probado
  localmente, pero no hay una corrida de `pull_request` que lo confirme de punta a punta. Misma
  causa estructural que FAIL-1. **Exige confirmación explícita en RELEASE**, y quedar registrado si
  no ocurre.
- **W-2 · El paso de CI mostrando campo y endpoint al fallar**: mismo caso que W-1.
- **W-3 · Dos bullets del Block 4 sin test automatizado.** "El script sale distinto de 0 si se
  desarma la verificación" se comprobó a mano en dos formas distintas durante CODE y quedó
  registrado en el commit `748d6bd`, pero la propia spec lo redacta como narración y no como test con
  nombre. Y la corrida interrumpida —el `trap`— no se ejercita en ningún lado.
- **W-4 · IDs de semilla hardcodeados** (`CategoriaComidaId = 1`, `CategoriaSueldoId = 8`) en los dos
  archivos de test nuevos. Frágil si cambia el orden de la semilla. Es el mismo patrón que tickets
  anteriores ya usan, así que no es una regresión, pero se acumula.
- **W-5 · W-VER-02 no evaluable** por la misma razón que FAIL-4.

## Lo que sí pasó (12)

AC-01 a AC-07 y AC-10 a AC-15 verificados, varios reejecutando los comandos. En particular:

- **AC-02 reejecutado por el verificador**: renombró `TotalIngresado` → `Ingresos`, la verificación
  falló, revirtió, volvió a pasar, y `git status` quedó limpio. La mitigación R-06 del threat model
  —el riesgo más alto del ticket— se sostiene en manos de alguien que no la escribió.
- **F-VER-04 PASS**: los tres puntos de entrada tienen sad paths (3 en el parser, 5 en el
  comparador, 1 en las peticiones).
- **F-VER-05 PASS**: build con `-warnaserror` en 0 warnings y formato en 0, reejecutados.
- **AC-12 y AC-13 confirmados con `git diff`**, no afirmados: 0 tests existentes modificados, 0
  dependencias nuevas.
- **Los desvíos 2 y 3 juzgados legítimos**: el test de la unión de literales cubre un caso real que
  la tabla de compatibilidad del PRD exige, y la excepción de arquitectura está documentada con
  alcance explícito en `AGENTS.md` y en ADR-004, con la dirección inversa confirmada inexistente por
  grep.

## Acción

**Bucle correctivo de vuelta a CODE.** No se parchea código en VERIFY.

Lo que hay que construir en la ronda 2:

1. El test de la rama de tipo inexistente, y los otros dos sad paths del parser que el verificador
   identificó (FAIL-3).
2. El test de la corrida sin MySQL que el Block 5 exige (FAIL-2).
3. Una respuesta a FAIL-4: o una forma de medir cobertura sobre el código nuevo, o el registro
   explícito de por qué no aplica.

Y **FAIL-1 no se arregla con código**: AC-09 está escrito de una forma que no se puede verificar en
la fase que lo verifica. Requiere una decisión del usuario entre corregir el criterio —bucle
correctivo hasta DEFINE— o aceptar que se cierre en RELEASE con la medición real como condición
dura del cierre, y no como nota al pie.

---

## Ronda 2: PASSED

| Field | Value |
|-------|-------|
| Ronda | 2 (tras el bucle correctivo) |
| Fecha | 2026-08-21 |
| Resultado | **PASSED** — 0 FAIL, 4 WARN, 31 PASS |
| Verificador | `daw-module-verifier`, reejecutando cada medición por su cuenta |

```
FAILs: 0 | WARNs: 4 | PASSes: 31  →  PASSED
```

El verificador no aceptó ningún número del bucle correctivo: reejecutó la suite con el runsettings,
parseó el XML de cobertura a mano, corrió el script de la barrera, consultó la corrida de CI por
`gh api`, y leyó el parser línea por línea contra cada test nuevo en vez de confiar en los nombres.

### Los cuatro FAIL, cerrados con evidencia reproducida

| FAIL | Cómo se cerró | Confirmación independiente |
|---|---|---|
| **FAIL-1** AC-09 inverificable | El CI corre en `push` (decisión del usuario) | Corrida `32535963580`, disparada por `push` sin PR, `success`. El paso «Barrera del contrato» medido por el verificador: `23:13:26 → 23:13:50` = **24 s** contra 90 s de techo |
| **FAIL-2** test de corrida sin base | `Contrato_SinBaseDeDatos_FallaConElMensajeDelFixtureYNoConUnoDeContrato` | Existe, usa `CadenaHaciaUnPuertoCerrado`, y sus tres asserts son sustanciales |
| **FAIL-3** ramas del parser sin test | 3 sad paths nuevos | Cada uno dispara exactamente el `throw` que dice disparar, verificado leyendo el código |
| **FAIL-4** cobertura no medible | `backend/cobertura.runsettings` | Recalculada a mano: `ComparadorDeFormas` **100%**, código nuevo **95,5%**, mínimo `LectorDeTiposDelFrontend` **92,1%** |

Los 35 checkboxes de la spec verificados por sustancia, y confirmado con `git log` que
`spec-FEAT-003.md` no se tocó después de PLAN.

### Una corrección al cierre del bucle, que corresponde dejar escrita

El commit `ba159e1` afirmó que la corrida de CI **cerraba** W-1 y W-2 de la ronda 1. **No es
exacto**, y el verificador lo marcó bien:

- **W-1 sigue abierto.** AC-08 dice literalmente *"IF un **pull request** introduce un
  incumplimiento"*. La corrida fue por `push`. El mecanismo subyacente es idéntico y está probado,
  pero el criterio como está escrito todavía no tiene su corrida. Se cierra en RELEASE, con el PR.
- **W-2 sigue abierto, y es un hallazgo nuevo de sustancia.** `verificacionDeContrato()` en
  `backend/verificar-contrato.sh` redirige `dotnet test` a `/dev/null 2>&1`. Cuando la barrera falle
  de verdad en el CI, el log va a mostrar *"FALLA: el contrato ya no verifica sin haber tocado
  nada"* **sin decir qué campo ni qué endpoint**. El mensaje bueno existe y está cubierto por
  `Comparador_AlReportar_NombraCampoEndpointYDiferencia`, pero nunca llega al lugar donde alguien lo
  leería a las tres de la tarde de un martes.

  Es exactamente la clase de detalle que este ticket entero existe para no dejar pasar: una barrera
  que detecta el problema pero no sabe explicarlo. **Queda como deuda declarada**, no como algo
  cerrado.

### WARNINGs vigentes (4)

- **W-1 · AC-08 sin corrida de `pull_request`.** Se cierra en RELEASE. **Condición explícita del
  cierre, no nota al pie.**
- **W-2 · El script silencia la salida de `dotnet test`.** El log de una falla real no dirá qué
  campo. Deuda declarada; el arreglo es capturar la salida y volcarla cuando el paso falla.
- **W-3 · Dos bullets del Block 4 sin test automatizado.** El desarme se comprobó a mano en dos
  formas durante CODE y quedó en el commit `748d6bd`, pero la spec lo redacta como narración; y la
  corrida interrumpida del `trap` no se ejercita. Sin cambios desde la ronda 1, confirmado por diff.
- **W-4 · IDs de semilla hardcodeados** (`CategoriaComidaId = 1`, `CategoriaSueldoId = 8`). Mismo
  patrón que tickets anteriores; se acumula, no es regresión.

Se registra además una observación de proceso: el commit del bucle correctivo no declara evidencia
TDD explícita. Atenuante verificado: la ronda 2 no agregó **ningún** código de producción — los
tests nuevos ejercitan ramas que ya existían desde la ronda 1 y estaban sin cubrir. El verificador
hizo la auditoría equivalente leyendo el código y no encontró discrepancias.

### Acción

**Gate `verify` cumplido.** Listo para RELEASE, con W-1 como condición dura del cierre: el PR tiene
que correr y su paso «Barrera del contrato» tiene que pasar antes de que el ticket se cierre.
