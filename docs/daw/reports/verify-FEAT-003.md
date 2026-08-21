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
