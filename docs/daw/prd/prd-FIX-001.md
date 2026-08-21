# PRD FIX-001: Linter del backend .NET

| Field | Value |
|-------|-------|
| Ticket | FIX-001 |
| Tracker | ninguno |
| Date | 2026-08-20 |
| PRD loops | 0 |

> D-1 del mapa de DISC-001 (`docs/daw/discovery/concept-DISC-001.md`), primero de los cuatro ítems
> de infraestructura que el usuario decidió poner antes que cualquier feature de producto.
> Análisis de causa raíz y medición completa en `docs/daw/specs/rca-FIX-001.md`.

## Context and Problem

El backend .NET no tiene linter. La tabla Stack de `AGENTS.md` lo declara: el build con
`-warnaserror` es *"lo más cercano a un linter que tiene el backend"*. Pero `-warnaserror` es el
compilador, no un linter: atrapa lo que impide compilar bien, no lo que hace al código difícil de
leer o de mantener. El frontend sí tiene ESLint + Prettier corriendo en cada PR.

La causa raíz está en el RCA: los analizadores de Roslyn vienen en el SDK pero están apagados por
defecto en lo que hace a estilo, y no hay `.editorconfig` donde declarar qué reglas se aplican. Se
detectó el 2026-08-18, se agendó "entre FEAT-001b y `c`", y esa ventana pasó sin que nadie la
tomara — `c` se escribió, se verificó y se mergeó sin linter.

La medición rehecha el 2026-08-20 sobre `main` da **158 hallazgos únicos**: 143 en tests y **15 en
producción**. Los 15 de producción no son correcciones pendientes: 9 son parámetros nombrados en
español contra clases base en inglés, 5 están dentro de una migración **generada por EF** y 1 es un
artefacto de los top-level statements de la minimal API. Es decir, **en producción no hay ninguna
corrección que hacer a mano**; hay tres decisiones de configuración que tomar y dejar escritas.

Eso ordena el ticket: lo que se entrega no es un código más prolijo, es **una barrera que hoy no
existe**, configurada de modo que siga encendida dentro de seis meses. El riesgo real no es que sea
difícil: es que quede configurada de una forma que moleste sin aportar y que alguien la termine
apagando.

## Goals

- Que el backend tenga un linter real, con un comando propio, al mismo nivel que el del frontend.
- Que cada regla desactivada tenga su motivo escrito al lado, para que sea una excepción y no un
  descuido.
- Que el código generado no pueda romper el build con reglas que nadie escribió.
- Que la barrera corra donde importa y no dependa de que alguien se acuerde de correrla.

## Functional Requirements

- FR-01: El proyecto debe habilitar los analizadores de estilo y de calidad del SDK de .NET sobre la solución `backend/GestionGastos.sln`, mediante configuración versionada en el repositorio. Origen: causa raíz del RCA.
- FR-02: El proyecto debe ofrecer un comando de lint del backend que verifique sin modificar archivos, como espejo del `prettier --check` del frontend. Origen: simetría con el frontend declarada en `AGENTS.md`.
- FR-03: El proyecto debe excluir del análisis el código generado —las migraciones de Entity Framework y los archivos que el SDK genera—, de modo que un archivo no escrito a mano no pueda producir hallazgos. Origen: los 5 CA1861 de `MigracionInicial.cs` medidos en el RCA.
- FR-04: El proyecto debe desactivar CA1707 en el proyecto de tests, dejando en la configuración el motivo de la desactivación. Origen: 117 hallazgos que chocan con la convención `Sujeto_Escenario_Resultado` que las specs exigen.
- FR-05: El proyecto debe desactivar CA1725 y CA1050 en el proyecto de producción, dejando en la configuración el motivo de cada desactivación. Origen: 9 y 1 hallazgos medidos en el RCA.
- FR-06: El proyecto debe corregir los hallazgos que quedan activos tras las desactivaciones de FR-04 y FR-05, sin alterar el comportamiento de ningún componente. Origen: ~26 hallazgos mecánicos en tests.
- FR-07: El proyecto debe declarar el comando de lint del backend en la sección Stack de `AGENTS.md`, reemplazando la línea que hoy describe al build como lo más cercano a un linter. Origen: `AGENTS.md` es la única fuente del stack.
- FR-08: El proyecto debe ejecutar el lint del backend en el pipeline de integración continua, en el mismo workflow donde ya corren el lint y el formato del frontend. Origen: FEAT-002; una barrera que solo corre localmente no bloquea nada.

## Non-Functional Requirements

- NFR-01: El comando de lint del backend debe terminar con 0 hallazgos sobre la solución completa al cerrar el ticket. Origen: un linter que se entrega en rojo es un linter apagado.
- NFR-02: El proyecto debe conservar los 247 tests de la suite del backend y del frontend en verde, sin que ninguno haya sido modificado para acomodar un cambio de estilo. Origen: FR-06 exige no alterar comportamiento.
- NFR-03: El proyecto debe dejar 0 reglas desactivadas sin un comentario adyacente que explique el motivo. Origen: una excepción sin motivo es indistinguible de un descuido.
- NFR-04: El lint del backend debe agregar como máximo 60 s al tiempo del pipeline de integración continua. Origen: FEAT-002; un CI que tarda de más se empieza a saltear.

## Acceptance Criteria

- AC-01 (FR-01, FR-02): WHEN se ejecuta el comando de lint del backend sobre la solución, THE sistema SHALL analizar los proyectos de la solución y SHALL no modificar ningún archivo del repositorio.
- AC-02 (FR-01, NFR-01): WHEN se ejecuta el comando de lint del backend al cerrar el ticket, THE sistema SHALL terminar con 0 hallazgos y con código de salida 0.
- AC-03 (FR-02): IF un archivo del backend no cumple una regla activa, THEN THE comando de lint SHALL terminar con código de salida distinto de 0 e SHALL indicar el archivo, la línea y la regla incumplida.
- AC-04 (FR-03): WHEN se agrega una migración generada por Entity Framework que contendría hallazgos de las reglas activas, THE sistema SHALL terminar el lint con 0 hallazgos y SHALL no atribuir ninguno a esa migración.
- AC-05 (FR-04): WHEN se ejecuta el lint sobre el proyecto de tests con sus nombres en la convención `Sujeto_Escenario_Resultado`, THE sistema SHALL no producir ningún hallazgo de CA1707.
- AC-06 (FR-05): WHEN se ejecuta el lint sobre el proyecto de producción, THE sistema SHALL no producir ningún hallazgo de CA1725 ni de CA1050.
- AC-07 (FR-06, NFR-02): WHEN se ejecuta la suite completa después de aplicar las correcciones, THE sistema SHALL pasar los 247 casos, y ningún archivo de test SHALL haber cambiado su comportamiento esperado.
- AC-08 (NFR-03): WHEN se inspecciona la configuración del linter, THE sistema SHALL exhibir junto a cada regla desactivada un comentario con el motivo de su desactivación.
- AC-09 (FR-07): WHEN se lee la sección Stack de `AGENTS.md`, THE sistema SHALL declarar el comando de lint del backend, y SHALL no describir el build como lo más cercano a un linter.
- AC-10 (FR-08): IF un pull request introduce un incumplimiento de una regla activa en el backend, THEN THE pipeline de integración continua SHALL fallar identificando la regla incumplida.
- AC-11 (FR-08, NFR-04): WHEN se mide la duración del pipeline con el paso de lint del backend y sin él, THE sistema SHALL exhibir una diferencia de a lo sumo 60 s.
- AC-12 (FR-06): IF una corrección de estilo cambiara el comportamiento observable de un componente, THEN THE ticket SHALL dejar el hallazgo sin corregir y SHALL registrar el motivo, antes que alterar el comportamiento.

## Out of Scope

- **Agregar paquetes NuGet de análisis** (SonarAnalyzer, StyleCop, Roslynator). Descartado —no aplazado— el 2026-08-18 y confirmado por la medición: con 15 hallazgos en producción, y ninguno que exija una corrección a mano, el problema no es falta de reglas. Agregar cientos más antes de ordenar las que ya hay es cambiar ruido por señal. Además sería una dependencia nueva, que `AGENTS.md` obliga a justificar en la spec, y hoy no hay justificación.
- **Subir `AnalysisMode` por encima de `Recommended`** o activar categorías que la medición no cubrió.
- **Reformatear el backend** (llaves, sangrías, ordenamiento de `using`) más allá de lo que exijan las reglas activas.
- **Renombrar los 117 tests** para satisfacer CA1707: FR-04 apaga la regla precisamente para no empeorar los nombres que las specs exigen.
- **Traducir al inglés los parámetros en español** para satisfacer CA1725: FR-05 apaga la regla por la misma razón.
- **Refactorizar el código señalado por CA1859 o CA1822** más allá de la corrección mecánica que la regla pide.
- **Linter o formateo del frontend**: ya existen y no se tocan.
- **Los otros tres ítems de infraestructura** — D-2 (Vitest sin `typecheck`), D-3 (fixture que vence en 2027) y D-4 (falso positivo de W-PRD-02) — son tickets propios.

## Risks and Mitigations

- **Riesgo: el linter queda configurado de forma molesta y alguien lo apaga.** Es el riesgo principal, y el que decide si este ticket sirvió: una regla que obliga a renombrar 117 tests o que rompe el build con cada migración de EF se desactiva entera en dos meses. → Mitigación: FR-03, FR-04 y FR-05 sacan de en medio exactamente esos tres casos **antes** de encender la barrera, y NFR-03 exige que cada desactivación lleve su motivo, para que la próxima persona sepa si sigue valiendo.
- **Riesgo: una corrección "mecánica" cambia comportamiento.** CA1859 (usar el tipo concreto en vez de la interfaz) y CA1822 (hacer un miembro `static`) tocan firmas y despacho de métodos. → Mitigación: NFR-02 y AC-07 exigen la suite verde sin tests modificados, y AC-12 fija que ante la duda **gana el comportamiento**: el hallazgo queda sin corregir y con su motivo registrado.
- **Riesgo: encender el linter en el CI rompe PRs que no tienen nada que ver.** → Mitigación: NFR-01 exige 0 hallazgos al cerrar el ticket, así que el paso nuevo arranca en verde. Un PR posterior que falle, falla por lo que ese PR trajo.
- **Riesgo: la medición se hizo con `AnalysisMode=Recommended` pasado por línea de comandos**, y la configuración versionada podría no dar el mismo conjunto de reglas. → Mitigación: AC-02 se verifica con el comando real, ya con la configuración en el repositorio, no con la línea de la medición.
- **Riesgo: el CI se alarga.** El backend ya compila y testea en el pipeline; un paso más de análisis suma tiempo. → Mitigación: NFR-04 y AC-11 lo acotan a 60 s.
- **Riesgo: `dotnet format --verify-no-changes` y los analizadores de compilación no cubren lo mismo.** Uno mira formato, los otros calidad, y el comando elegido tiene que cubrir ambos o el ticket entrega media barrera. → Mitigación: es una decisión del PLAN, y AC-03 la ata a un comportamiento observable — el comando tiene que fallar ante un incumplimiento de una regla activa, sea de la familia que sea.

## Dependencies

- El SDK de .NET 10.0.301 declarado en `AGENTS.md`, que trae los analizadores de Roslyn; no se agrega ninguna dependencia externa.
- La solución `backend/GestionGastos.sln` y sus dos proyectos, `GestionGastos.Api` y `GestionGastos.Api.Tests`.
- `docs/daw/specs/rca-FIX-001.md`, de donde salen la causa raíz, la medición de 158 hallazgos y las tres decisiones de configuración.
- El workflow de integración continua `.github/workflows/ci.yml` de FEAT-002, donde FR-08 agrega el paso.
- La sección Stack de `AGENTS.md`, única fuente del stack, que FR-07 actualiza.
- La suite de 247 tests de `main`, que NFR-02 usa como referencia de que el comportamiento no cambió.
