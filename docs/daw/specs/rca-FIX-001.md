# RCA FIX-001: el backend .NET nunca tuvo linter

| Field | Value |
|-------|-------|
| Ticket | FIX-001 |
| Tracker | ninguno |
| Date | 2026-08-20 |
| Componente afectado | `backend/` — configuración de build de la solución, no un módulo de código |
| PRD relacionado | ninguno; es deuda de infraestructura (D-1 en `docs/daw/discovery/concept-DISC-001.md`) |
| Gap en el PRD | no |

## Síntoma

El backend .NET no tiene linter. `AGENTS.md` lo dice de frente en su tabla Stack: el comando de
build (`dotnet build backend/GestionGastos.sln -warnaserror`) es *"hoy lo más cercano a un linter
que tiene el backend"*. El frontend sí tiene ESLint + Prettier, corriendo en cada PR desde el CI.

La asimetría no es cosmética. `-warnaserror` es el compilador: atrapa lo que impide compilar bien,
no lo que hace al código difícil de leer o de mantener. Un `dotnet build` verde no dice nada sobre
convenciones, y por eso ninguna de las tres features de FEAT-001 tuvo forma de verificarlas.

## Causa raíz

**El proyecto se scaffoldeó sin analizadores habilitados, y ningún ticket posterior era el dueño de
habilitarlos.**

La causa técnica es que los analizadores de Roslyn vienen en el SDK de .NET pero **están apagados
por defecto en lo que a estilo se refiere**: `EnforceCodeStyleInBuild` es `false` y `AnalysisMode`
es el mínimo, salvo que un `Directory.Build.props` o el `.csproj` digan otra cosa. No hay
`.editorconfig` en el repositorio, así que tampoco hay dónde declarar qué reglas se aplican. El
resultado es un backend que compila sin warnings porque nadie encendió las reglas que los
producirían.

La causa organizativa es la que hace que siga abierto hoy, y es la que vale registrar:

1. **2026-08-18** — se detecta la falta durante FEAT-001b. Se decide, correctamente, que no entre
   dentro de una feature: un `dotnet test` en rojo por una regla nueva no dejaría distinguir si
   falló la feature o la configuración. Se agenda como ticket propio.
2. **La ventana elegida fue "entre `b` y `c`"**, con un buen argumento: así `c` se escribiría desde
   el arranque bajo las reglas nuevas, en vez de corregir retroactivamente el código que `c` acababa
   de escribir.
3. **2026-08-19** — FEAT-001c se define, se planifica, se implementa, se verifica y se mergea a
   `main`. La ventana pasó. Nadie la abrió.

El patrón es el que este RCA existe para no repetir: **un ítem sin ticket, agendado contra un hueco
entre dos tickets, no tiene dueño y no ocurre.** El hueco no aparece en ningún estado, ninguna fase
lo reclama y ningún gate lo exige. Lo que lo desbloqueó no fue recordarlo mejor: fue que DISC-001 lo
convirtiera en un ítem numerado de un mapa con orden acordado.

## Cadena de eventos

```
scaffolding del backend
  └─ analizadores de estilo apagados por defecto, sin .editorconfig
      └─ FEAT-001a se escribe sin linter        (nadie lo nota: no hay señal)
          └─ FEAT-001b lo detecta                (2026-08-18, se agenda "entre b y c")
              └─ la ventana entre b y c no la toma nadie
                  └─ FEAT-001c se escribe, verifica y mergea sin linter
                      └─ DISC-001 lo numera como D-1 y le da lugar en el orden
                          └─ FIX-001 (este ticket)
```

## Medición

Repetida el **2026-08-20** sobre `main` (`7f7f1ec`), que ya incluye `a`, `b` y `c`:

```
dotnet build backend/GestionGastos.sln --no-incremental \
  -p:AnalysisMode=Recommended -p:EnforceCodeStyleInBuild=true -p:TreatWarningsAsErrors=false
```

El `--no-incremental` es imprescindible: sin él la segunda corrida no reporta nada.

**158 hallazgos únicos**, deduplicando por archivo, línea, columna y regla. El log crudo dice 316
—exactamente el doble— porque cada warning se emite una vez por proyecto que compila el archivo.

| Proyecto | Hallazgos |
|---|---|
| `GestionGastos.Api.Tests` | 143 |
| `GestionGastos.Api` (producción) | **15** |

### Los 15 de producción, de a uno

| Regla | # | Dónde | Qué es |
|---|---|---|---|
| CA1725 | 9 | `AppDbContext.OnModelCreating`, las 3 `*Configuracion.Configure`, `MontoJsonConverter.Read/Write` | Parámetros nombrados en español (`constructor`, `lector`, `escritor`) contra clases base que los declaran en inglés |
| CA1861 | 5 | `Migrations/20260817013901_MigracionInicial.cs` | Arrays constantes como argumento — **código generado por EF**, no escrito a mano |
| CA1050 | 1 | `Program.cs:49` | "Declare types in namespaces": artefacto de los top-level statements de la minimal API |

### Los 143 de tests

| Regla | # | Qué es |
|---|---|---|
| CA1707 | 117 | "Quitá los guiones bajos" — choca con `Sujeto_Escenario_Resultado`, la convención que las specs exigen nombre por nombre |
| CA1861 | 12 | Arrays constantes como argumento |
| CA1859 | 10 | Usar el tipo concreto en vez de la interfaz donde se puede |
| CA1822 | 3 | Miembros que podrían ser `static` |
| CA1711 | 1 | Sufijo de nombre desaconsejado |

## Correcciones a lo que estaba registrado

Dos, y las dos importan porque estaban en la memoria del proyecto y en el mapa de DISC-001:

1. **Los "258 hallazgos" del 2026-08-18 estaban contados dos veces.** El número real de entonces era
   129. La medición de hoy da 158, y el crecimiento es todo de tests nuevos.
2. **`Resumen/` no agregó ni un hallazgo.** Se esperaba que producción hubiera crecido por encima de
   los 30 anotados porque la medición vieja no incluía el código de FEAT-001c. Producción está en
   15, que es exactamente la mitad de 30: el código de `c` entró limpio, y el doble conteo queda
   confirmado por un segundo camino independiente.

## Conclusión que ordena el arreglo

**En producción no hay ni una corrección que hacer a mano.** Los 15 hallazgos se resuelven con tres
decisiones de configuración:

- **CA1725 se apaga**, con el motivo al lado: obedecerla es renombrar a inglés parámetros que el
  proyecto nombra en español por convención declarada.
- **El código generado sale del análisis.** Los 5 CA1861 están todos en una migración de EF. Sin
  esta exclusión, cada migración futura traería warnings que nadie escribió y que con
  `-warnaserror` romperían el build — que es exactamente cómo un linter se termina apagando a los
  dos meses.
- **CA1050 se silencia**, con el motivo al lado: es un artefacto de las minimal APIs.

**El trabajo real está en los tests**: apagar CA1707 (117, por la misma razón de convención
declarada) deja ~26 hallazgos mecánicos genuinos.

## Plan de rollback

Revertir el commit que agrega `Directory.Build.props` y `.editorconfig`, y devolver la línea
`Lint (backend)` de `AGENTS.md` a su estado anterior. No hay migración, no hay datos, no hay
contrato de API: el arreglo es configuración de build y su reverso es borrarla. Si el CI ya corre
el paso nuevo, se quita ese paso en el mismo revert.
