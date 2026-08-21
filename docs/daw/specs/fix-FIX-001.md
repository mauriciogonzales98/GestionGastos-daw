# Fix-plan FIX-001: Linter del backend .NET

| Field | Value |
|-------|-------|
| Ticket | FIX-001 |
| Tier | FIX |
| RCA | docs/daw/specs/rca-FIX-001.md |
| Date | 2026-08-20 |
| Spec loops | 0 |

## Problem

El backend .NET no tiene linter. `AGENTS.md` declara que el build con `-warnaserror` es *"lo más
cercano a un linter que tiene el backend"*, y eso es el compilador, no un analizador: atrapa lo que
impide compilar bien, no lo que hace al código difícil de leer o de mantener. El frontend sí corre
ESLint y Prettier en cada PR.

Se manifiesta como ausencia de señal: un `dotnet build` verde no dice nada sobre convenciones, así
que las tres features de FEAT-001 se escribieron sin ninguna forma de verificarlas.

## Root cause

Los analizadores de Roslyn vienen en el SDK de .NET pero **están apagados por defecto en lo que hace
a estilo**: `EnforceCodeStyleInBuild` es `false` y `AnalysisMode` es el mínimo salvo que un
`Directory.Build.props` o el `.csproj` digan otra cosa. No hay `.editorconfig` en el repositorio, así
que tampoco hay dónde declarar qué reglas se aplican. Ninguno de los dos archivos existe hoy.

La causa que lo mantuvo abierto un mes es organizativa y está desarrollada en el RCA: se agendó
"entre FEAT-001b y `c`", y un ítem agendado contra un hueco entre dos tickets no tiene dueño, no
aparece en ningún estado y no lo reclama ninguna fase.

**Dato que condiciona todo el arreglo:** `TreatWarningsAsErrors=true` ya está en los dos `.csproj`
(`GestionGastos.Api.csproj:8` y `GestionGastos.Api.Tests.csproj:9`). Encender los analizadores
convierte cada hallazgo en un **error de compilación**, no en un warning. Por eso el orden de los
pasos no es negociable: primero se configuran las supresiones y las exclusiones, después se corrigen
los hallazgos que quedan, y recién entonces el build queda verde.

## Solution — steps

### Paso 1 — `backend/Directory.Build.props` (nuevo)

Encender los analizadores para los dos proyectos de la solución.

- `EnforceCodeStyleInBuild` = `true`
- `AnalysisMode` = `Recommended`

**Solo analizadores del SDK** (mitigación R-01 del threat model). No se agrega ningún paquete NuGet
de análisis: un analizador es código de terceros que el compilador ejecuta en cada build, local y en
el runner, con acceso al árbol de fuentes. Está en Out of Scope del PRD por señal/ruido; ésta es la
razón de seguridad, y sumarlo exigiría su propia spec.

Va en `backend/` y **no en la raíz** del repositorio: MSBuild hereda este archivo hacia abajo desde
donde esté, y el Impact Scan confirmó que los únicos `.csproj` del repo son los dos de `backend/`.
Ponerlo en la raíz alcanzaría además a cualquier proyecto futuro fuera del backend, que es un
alcance que este ticket no decidió.

### Paso 2 — `backend/.editorconfig` (nuevo)

Declarar las tres supresiones, **cada una con su motivo en un comentario adyacente** (NFR-03), y
excluir el código generado.

**Dos reglas que gobiernan este archivo** (mitigaciones R-02 y R-05 del threat model), escritas
como encabezado del propio `.editorconfig` para que las lea quien vaya a agregar la sexta supresión:

- **Supresiones por id de regla, nunca por categoría.** Queda prohibido
  `dotnet_analyzer_diagnostic.category-*.severity` y cualquier comodín: solo
  `dotnet_diagnostic.CAxxxx.severity` con el id exacto. Las cinco supresiones de hoy son de
  **Naming** (CA1707, CA1711, CA1725) y **Design** (CA1050); **ninguna regla de las categorías
  Security o Reliability puede sumarse a esta lista** sin un threat model propio.
- **La exclusión de código generado se acota a `GestionGastos.Api/Migrations/`**, con prefijo de
  proyecto y sin comodines que puedan alcanzar código escrito a mano. Un patrón ancho apagaría los
  analizadores —incluidos los de seguridad— sobre archivos reales sin que nada lo indique: los
  hallazgos simplemente dejarían de aparecer.

1. **Sección global** — `generated_code = true` para `GestionGastos.Api/Migrations/**.cs`, de modo
   que los analizadores traten esos archivos como generados y no produzcan hallazgos. Cubre los 5
   CA1861 medidos, todos dentro de `20260817013901_MigracionInicial.cs`. Sin esta exclusión, cada
   migración futura de EF entraría con warnings que nadie escribió y que con
   `TreatWarningsAsErrors` romperían el build.
2. **`CA1725` → `none`** en producción. Motivo: pide renombrar a inglés parámetros que el proyecto
   nombra en español (`constructor`, `lector`, `escritor`) para coincidir literalmente con las
   clases base de EF y de `System.Text.Json`. 9 hallazgos en 6 miembros.
3. **`CA1050` → `none`** en producción. Motivo: "Declare types in namespaces" sobre `Program.cs:49`
   es un artefacto de los top-level statements de la minimal API.
4. **`CA1707` → `none`** en `GestionGastos.Api.Tests`. Motivo: pide quitar los guiones bajos, y
   `Modificar_DeOtroPropietario_Devuelve404` es la convención `Sujeto_Escenario_Resultado` que las
   specs de este proyecto exigen nombre por nombre. Obedecerla sería renombrar 117 tests para
   empeorarlos.
5. **`CA1711` → `none`** en `GestionGastos.Api.Tests`. Motivo: pide renombrar
   `BaseDeDatosCollection` por terminar en "Collection", que es exactamente la convención de xUnit
   para los fixtures de colección. Renombrarla tocaría **16 archivos** que la referencian vía
   `[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]`, en un ticket cuyo NFR-02 exige no
   cambiar comportamiento. Es el caso que AC-12 contempla.

### Paso 3 — corregir los 26 hallazgos activos, en 8 archivos

Todos en `GestionGastos.Api.Tests`. La lista es cerrada y sale de la medición del RCA:

| Archivo | Hallazgos |
|---|---|
| `Movimientos/ListarMovimientosTests.cs` | 6 |
| `Infra/BaseDeDatosFixture.cs` | 5 (4 tras suprimir CA1711) |
| `Resumen/ResumenTests.cs` | 5 |
| `Movimientos/FiltrarMovimientosTests.cs` | 4 |
| `Categorias/CategoriasEndpointsTests.cs` | 3 |
| `Data/ModeloDeDatosTests.cs` | 1 |
| `Common/UsuarioActualTests.cs` | 1 |
| `Resumen/RendimientoResumenTests.cs` | 1 |

Por regla: **CA1861 ×12** (arrays constantes como argumento → `static readonly`), **CA1859 ×10**
(usar el tipo concreto en vez de la interfaz), **CA1822 ×3** (miembros que pueden ser `static`),
**CA1711 ×1** (suprimida en el paso 2).

**CA1859 y CA1822 no son puramente cosméticas**: la primera cambia tipos declarados y la segunda el
despacho de miembros. Se aplican una por una verificando que la suite siga verde. Ante cualquier
cambio de comportamiento observable, **el hallazgo queda sin corregir con su motivo registrado**
(AC-12), antes que tocar el comportamiento.

### Paso 4 — `.github/workflows/ci.yml` (modificado)

El job `Backend — build y tests` ya corre `dotnet build backend/GestionGastos.sln --no-restore
-warnaserror` (línea 92). Con el paso 1, **ese paso existente pasa a ser el linter de calidad**: los
analizadores corren dentro del build y `-warnaserror` los convierte en fallo del PR. No hace falta
un paso nuevo para eso; sí hay que actualizar su comentario, que hoy dice que es lo más cercano a un
linter porque no hay analizador configurado.

Falta la otra familia. El job `frontend` separa `Lint` (eslint) de `Formato` (prettier), y el
backend replica ese patrón: **paso nuevo `Formato`** con
`dotnet format backend/GestionGastos.sln --verify-no-changes --no-restore`, entre `Build` y `Tests`
para no cortar la cadena `restore → build → test` que `--no-restore` y `--no-build` encadenan.

### Paso 5 — `AGENTS.md` (modificado)

En la tabla Stack:

- Fila nueva **`Lint (backend)`** → `dotnet format backend/GestionGastos.sln --verify-no-changes`.
- Fila **`Build (backend)`**: se le quita la coletilla *"hoy es lo más cercano a un linter que tiene
  el backend"* y se reemplaza por que el build corre los analizadores de Roslyn gobernados por
  `.editorconfig`.

## Dependencies between steps

El orden importa y es estricto: **1 → 2 → 3 → 4 → 5**.

El paso 1 enciende los analizadores, y como `TreatWarningsAsErrors` ya está activo, **entre el paso
1 y el final del paso 3 la solución no compila**. Por eso los pasos 1, 2 y 3 son un solo commit: no
hay estado intermedio verde entre ellos. Los pasos 4 y 5 dependen de que el 3 haya terminado —
poner el paso de CI antes dejaría el pipeline en rojo— y son commits propios.

El paso 5 depende del 4 solo en el sentido de que declara el comando que el 4 ejecuta; el texto sale
del mismo lugar.

## Error handling

- **El build falla tras el paso 1 con hallazgos no previstos por la medición** (por ejemplo, reglas
  que `Recommended` activa y que el `--no-incremental` de la medición no expuso): se evalúan de a
  uno. Si son mecánicos, se corrigen dentro del paso 3; si exigen cambiar comportamiento, se
  suprimen con motivo bajo AC-12 y se registran en el reporte del cierre de CODE.
- **`dotnet format --verify-no-changes` reporta diferencias de formato masivas**: es esperable, dado
  que nunca corrió. Se aplica `dotnet format` una vez y el resultado entra en el paso 3, no se
  suprime la verificación.
- **`generated_code = true` no alcanza para silenciar las migraciones**: alternativa es una sección
  `[Migrations/*.cs]` con las severidades correspondientes en `none`. La condición de éxito es
  AC-04, no el mecanismo.
- **El paso de formato excede los 60 s de NFR-04**: se acota el alcance del comando al proyecto en
  vez de a la solución, o se registra el desvío. No se elimina el paso.

## Tests

- [ ] **Test de regresión** — reproduce el defecto original: `dotnet build backend/GestionGastos.sln
      --no-restore -warnaserror` **falla** ante un incumplimiento deliberado de una regla activa
      (por ejemplo un `CA1861` introducido a propósito) y **pasa** una vez revertido. Antes del fix
      ese mismo incumplimiento compila sin decir nada, que es exactamente el defecto (AC-03).
- [ ] `dotnet format backend/GestionGastos.sln --verify-no-changes` termina con código 0 sobre la
      solución completa (AC-01, AC-02).
- [ ] El build de la solución termina con 0 hallazgos y código 0 (AC-02).
- [ ] Una migración de EF generada con `dotnet ef migrations add` no produce ningún hallazgo
      (AC-04).
- [ ] **Contra-prueba de la exclusión** (mitigación R-05): un archivo escrito a mano ubicado fuera de
      `Migrations/` con un incumplimiento deliberado **sí** produce hallazgo. Sin esta verificación,
      ensanchar el patrón de exclusión apagaría los analizadores en silencio.
- [ ] `.editorconfig` no contiene ninguna supresión por categoría ni con comodín, y ninguna de las
      categorías Security o Reliability (mitigación R-02).
- [ ] `dotnet build` sobre `GestionGastos.Api.Tests` no produce ningún CA1707 (AC-05).
- [ ] `dotnet build` sobre `GestionGastos.Api` no produce ningún CA1725 ni CA1050 (AC-06).
- [ ] La suite completa pasa: 247 casos, sin que ningún archivo de test haya cambiado su
      comportamiento esperado (AC-07, NFR-02).
- [ ] Cada regla desactivada en `.editorconfig` tiene un comentario adyacente con su motivo
      (AC-08, NFR-03).
- [ ] La tabla Stack de `AGENTS.md` declara `Lint (backend)` y ya no describe el build como lo más
      cercano a un linter (AC-09).
- [ ] El paso de formato del CI suma como máximo 60 s respecto de la corrida anterior (AC-11,
      NFR-04).

## Regression risk

**Medio.** No por el tamaño del cambio sino por dónde pega.

- **`TreatWarningsAsErrors` amplifica todo.** Cualquier hallazgo que la medición no haya previsto no
  es un warning: es un build roto, en local y en el CI. Es el riesgo principal y el motivo del orden
  estricto de los pasos.
- **CA1859 y CA1822 tocan tipos y despacho** en 8 archivos de test. Un cambio de tipo declarado
  puede alterar qué sobrecarga se resuelve. Mitigado por la suite y por AC-12.
- **`dotnet format` puede reformatear más de lo previsto**, incluidos archivos que nadie pensaba
  tocar en este ticket. Se revisa el diff antes de commitear.
- **La supresión de reglas queda normalizada como mecanismo.** Es el riesgo que el threat model
  marca como HIGH (R-02): el archivo nace con cinco reglas apagadas, todas con buen motivo, y eso
  vuelve indistinguible apagar CA1707 de apagar una regla de seguridad. Mitigado por las dos reglas
  del encabezado del paso 2 y por los dos tests que las verifican.
- **Riesgo bajo en producción:** el paso 3 no toca ni un archivo de `GestionGastos.Api`. Los 15
  hallazgos de producción se resuelven todos por configuración.

## Rollback plan *(mandatory)*

- **Pasos:** revertir el commit que agrega `backend/Directory.Build.props` y `backend/.editorconfig`
  — con eso los analizadores vuelven a estar apagados y el build vuelve al estado anterior. Después,
  revertir el commit del paso de `Formato` en `.github/workflows/ci.yml` y el de `AGENTS.md`. Las
  correcciones del paso 3 pueden quedarse: son válidas con o sin linter y no dependen de la
  configuración.
- **No hay** migración de base de datos, cambio de esquema, contrato de API ni dato que revertir. El
  arreglo es configuración de build y su reverso es borrarla.
- **Indicadores para aplicarlo:** el CI queda en rojo por hallazgos ajenos al PR que lo dispara; una
  migración de EF vuelve a romper el build pese a la exclusión del paso 2; o el paso de formato
  supera de forma sostenida el presupuesto de 60 s de NFR-04.
