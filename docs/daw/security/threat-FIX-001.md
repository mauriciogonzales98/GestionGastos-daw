# Threat model FIX-001: Linter del backend .NET

| Field | Value |
|-------|-------|
| Ticket | FIX-001 |
| Tier | FIX |
| Fix-plan | docs/daw/specs/fix-FIX-001.md |
| Date | 2026-08-20 |

## Alcance y aclaración previa

Este ticket **no agrega ni modifica endpoints, esquema, autenticación ni entrada de usuario**. No
toca un solo archivo de `GestionGastos.Api`. Sería fácil despachar el análisis con "es configuración
de build, no hay superficie" — y sería un error, porque la configuración de build **sí es código que
se ejecuta**, en la máquina de quien desarrolla y en el runner del CI, y porque el mecanismo que
este ticket introduce —un archivo donde se apagan reglas— es exactamente el lugar donde en el futuro
se puede apagar una regla de seguridad.

## Componentes y límites de confianza

| # | Componente | Novedad |
|---|---|---|
| C1 | `backend/Directory.Build.props` | nuevo — enciende los analizadores de Roslyn del SDK |
| C2 | `backend/.editorconfig` | nuevo — declara severidades y supresiones |
| C3 | Paso `Formato` en `.github/workflows/ci.yml` | nuevo — `dotnet format --verify-no-changes` |
| C4 | 26 correcciones en 8 archivos de `GestionGastos.Api.Tests` | modificación |

**Límites de confianza declarados:**

- **LC-1 · Repositorio → compilador local.** C1 y C2 son entrada de confianza para MSBuild y para
  Roslyn. Un analizador es código que el compilador ejecuta: quien controla estos archivos influye
  sobre qué se ejecuta al compilar. Cruzarlo requiere un commit revisado.
- **LC-2 · Repositorio → runner de CI.** C3 corre en el runner de GitHub Actions, en el mismo job
  que ya tiene acceso al service container de MySQL y a la variable `ConnectionStrings__Default`.
- **LC-3 · Código escrito a mano → código generado.** C2 declara que `Migrations/**` es código
  generado y por lo tanto **no analizado**. Es un límite nuevo, y todo lo que quede del lado
  "generado" deja de ser mirado por los analizadores, incluidos los de seguridad.

## Evaluación STRIDE

| Componente | S | T | R | I | D | E |
|---|---|---|---|---|---|---|
| **C1** props | No introduce identidad ni la verifica | **R-01**: modificar el archivo cambia qué analizadores corren | Queda en el historial de git, revisable en PR | No expone datos | **R-03**: análisis más lento en cada build | No otorga privilegios |
| **C2** editorconfig | No aplica | **R-01, R-02**: es el lugar donde se apagan reglas | NFR-03 exige motivo por supresión: cada apagado deja su razón escrita | No expone datos | Sin efecto | **R-02**: apagar una regla de seguridad amplía lo que pasa sin ser visto |
| **C3** CI | Usa el runner ya autenticado; no agrega credenciales | No escribe en el repo (`--verify-no-changes`) | El log del job queda en GitHub | **R-04**: el log imprime rutas, líneas y fragmentos | **R-03**: alarga el pipeline | No cambia permisos del workflow |
| **C4** tests | No aplica | Cambia tipos declarados y despacho de miembros | La suite es el registro | No expone datos | No aplica | No aplica |

## Riesgos

### R-01 — El archivo de configuración decide qué se ejecuta al compilar

| Campo | Valor |
|---|---|
| Categoría STRIDE | Tampering |
| Probabilidad | Baja |
| Impacto | Medio |

Los analizadores de Roslyn son ensamblados que el compilador carga y ejecuta. Encenderlos amplía lo
que corre en cada build, local y en CI.

**Mitigación (plegada al fix-plan, paso 1):** se usan **exclusivamente los analizadores que vienen
en el SDK de .NET 10.0.301** ya declarado en `AGENTS.md`. No se agrega ningún paquete NuGet de
análisis — está en Out of Scope del PRD, y la razón registrada allí era de señal/ruido; ésta es la
razón de seguridad: un paquete de analizadores es código de terceros ejecutándose en cada build y en
el runner, con acceso al árbol de fuentes. Sumarlo exige justificarlo en su propia spec.

### R-02 — La supresión de reglas se normaliza y termina apagando una regla de seguridad

| Campo | Valor |
|---|---|
| Categoría STRIDE | Elevation of Privilege / Tampering |
| Probabilidad | Media |
| Impacto | **Alto** |

Este es el riesgo real del ticket. `.editorconfig` nace con **cinco reglas apagadas**, todas con
buen motivo. Pero el archivo queda instalado como "el lugar donde se apagan reglas que molestan", y
la próxima persona que se tope con un `CA2100` (SQL construido por concatenación), un `CA3001`
(injection) o un `CA5350` (criptografía débil) tiene delante un archivo que ya normaliza esa
respuesta. Apagar una regla de seguridad se ve idéntico a apagar CA1707.

**Mitigaciones (a plegar al fix-plan, paso 2):**

1. **Supresiones por regla concreta, nunca por categoría.** Se prohíbe
   `dotnet_analyzer_diagnostic.category-Security.severity` y cualquier otra supresión por categoría o
   comodín: solo `dotnet_diagnostic.CAxxxx.severity` con el id exacto.
2. **Las cinco supresiones se agrupan bajo un encabezado que dice explícitamente que ninguna regla
   de las categorías Security ni Reliability puede sumarse a la lista** sin pasar por un threat model
   propio. La barrera es un comentario, sí — pero es el mismo mecanismo que NFR-03 ya exige, leído
   por la persona en el momento exacto en que va a tomar la decisión.
3. Las cinco supresiones actuales son de las categorías **Naming** (CA1707, CA1711, CA1725),
   **Design** (CA1050) y ninguna de seguridad. Queda registrado acá para que un cambio futuro se
   note en el diff contra este documento.

### R-03 — Análisis y verificación de formato alargan build y pipeline

| Campo | Valor |
|---|---|
| Categoría STRIDE | Denial of Service (auto-infligido) |
| Probabilidad | Alta |
| Impacto | Bajo |

Un CI que tarda de más se empieza a saltear, y una barrera que se saltea no es una barrera.

**Mitigación:** ya está en el PRD como NFR-04 y AC-11 — el paso de formato suma como máximo 60 s. El
fix-plan agrega en "Error handling" qué hacer si se excede: acotar el alcance del comando o
registrar el desvío, **nunca eliminar el paso**.

### R-04 — El log del CI publica rutas y fragmentos de código

| Campo | Valor |
|---|---|
| Categoría STRIDE | Information Disclosure |
| Probabilidad | Alta |
| Impacto | Bajo |

La salida de los analizadores y de `dotnet format` imprime archivo, línea, regla y a veces el
fragmento. En un repositorio público el log del job es público.

**Mitigación:** ninguna necesaria. Lo que se imprime es código fuente que ya es visible en el mismo
repositorio, y los analizadores no leen ni imprimen variables de entorno. Vale notar que el job de
backend **ya** tiene `ConnectionStrings__Default` en su entorno: apunta a un MySQL efímero del propio
runner, sin contraseña y a propósito, según el comentario del workflow. Este ticket no lo toca ni lo
expone de forma nueva.

### R-05 — El límite "código generado" se define demasiado ancho

| Campo | Valor |
|---|---|
| Categoría STRIDE | Tampering / Elevation of Privilege |
| Probabilidad | Baja |
| Impacto | **Alto** |

LC-3 apaga el análisis sobre lo que quede marcado como generado. Un patrón demasiado amplio
—`**/Migrations/**` sin prefijo de proyecto, o peor, `*.Designer.cs` combinado con algún comodín—
podría dejar código escrito a mano fuera de todo análisis, **incluidos los analizadores de
seguridad**, sin que nada lo indique: los archivos simplemente dejan de reportar.

**Mitigación (a plegar al fix-plan, paso 2):** el patrón se acota a
`GestionGastos.Api/Migrations/` de forma explícita, y AC-04 verifica el comportamiento buscado —que
una migración nueva de EF no produzca hallazgos— en vez del mecanismo. Se agrega una verificación
complementaria: **un archivo escrito a mano ubicado fuera de `Migrations/` tiene que seguir
produciendo hallazgos**, para que la exclusión no se pueda ensanchar sin que un test lo note.

## Clasificación de datos sensibles

| Dato | Clasificación | Cifrado en tránsito / reposo |
|---|---|---|
| Código fuente del backend | Público (repositorio en GitHub) | No aplica |
| Configuración de build (C1, C2) | Público | No aplica |
| `ConnectionStrings__Default` del job de CI | Credencial, **preexistente** | Base efímera sin contraseña en el propio runner; este ticket no la modifica |

**Este ticket no introduce ni maneja PII, credenciales ni datos financieros nuevos.** No corresponde
exigir cifrado en reposo ni en tránsito para nada que agregue.

## Riesgos aceptados

**Ninguno.** Los cinco riesgos identificados tienen mitigación, y las tres que no estaban en el
fix-plan se pliegan a él antes de aprobarlo (R-01 en el paso 1, R-02 y R-05 en el paso 2).

## Resultado

```
Superficies de ataque identificadas: 4 componentes
Límites de confianza declarados: 3 (LC-1, LC-2, LC-3)
Riesgos: C:0  H:2  M:1  L:2
Todos los HIGH con mitigación plegada al fix-plan → PASSED
```
