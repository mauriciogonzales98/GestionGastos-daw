# Informe SAST — FIX-001: Linter del backend .NET

| Field | Value |
|-------|-------|
| Ticket | FIX-001 |
| Tier | FIX |
| Fecha | 2026-08-20 |
| Alcance | Los 11 archivos del diff `main..HEAD` sin contar `docs/` |
| Threat model | docs/daw/security/threat-FIX-001.md |

## Alcance analizado

```
.github/workflows/ci.yml
AGENTS.md
backend/.editorconfig
backend/Directory.Build.props
backend/GestionGastos.Api.Tests/  (7 archivos)
```

**Ningún archivo de `GestionGastos.Api` cambió.** El código de producción es idéntico al de `main`:
lo que este ticket agrega es configuración de build y correcciones en tests.

## Resultados

### Secretos (F-SAST-01) — ✅ limpio

- 0 coincidencias de contraseña, token, api key o cadena de conexión en las líneas agregadas.
- `.gitignore` cubre `.env` y `.env.*`, con el motivo escrito al lado (mitigación R-11 de un ticket
  anterior).
- El workflow no agrega secretos: el paso `Formato` nuevo no consume ninguna variable. El
  `ConnectionStrings__Default` del job de backend es preexistente y apunta a un MySQL efímero del
  propio runner, sin contraseña y a propósito.

### Inyección (F-SAST-02, 03, 05) — ✅ limpio

- 0 concatenación de strings en consultas: el código de producción no cambió y ya usa el ORM de
  punta a punta.
- 0 entrada de usuario alcanzando `exec`/`spawn`/`Process.Start`.
- 0 entrada de usuario en rutas de archivo. Los patrones de `.editorconfig` son literales fijos
  escritos en el repositorio, no entrada.

### XSS y funciones inseguras (F-SAST-04, 06, 17) — ✅ limpio

- No aplica XSS: no se tocó frontend.
- 0 `eval`, `Process.Start`, `Assembly.Load` ni `BinaryFormatter` en lo agregado.

### Criptografía (F-SAST-08) — ✅ no aplica

Este ticket no maneja hashing, cifrado ni firmas.

### Modo debug, logging sensible, upload, CSRF, SSRF (F-SAST-07, 09, 10, 11, 12) — ✅ no aplica

Ninguna superficie nueva: no hay endpoint, ni configuración de entorno, ni logging agregado.

### Validación de entrada y manejo de errores (F-SAST-14, 15) — ✅ limpio

- 0 entradas de usuario nuevas.
- 0 `catch` agregados; ninguno que filtre internals.

### Dependencias (F-SAST-13, F-SAST-16) — ✅ limpio

```
dotnet list backend/GestionGastos.sln package --vulnerable --include-transitive
  GestionGastos.Api        → no vulnerable packages
  GestionGastos.Api.Tests  → no vulnerable packages
```

**0 dependencias nuevas.** Los analizadores vienen en el SDK 10.0.301 ya declarado; no se agregó
ningún paquete NuGet de análisis, lo que era la mitigación de R-01 del threat model.

## Verificación específica de los riesgos del threat model

Los dos riesgos HIGH tienen ahora una comprobación mecánica, y este es el lugar donde corresponde
dejarla registrada:

### R-02 — Ninguna regla de seguridad suprimida

Las cinco reglas apagadas en `backend/.editorconfig`, con su categoría:

| Regla | Categoría | ¿Security o Reliability? |
|---|---|---|
| CA1707 | Naming | no |
| CA1711 | Naming | no |
| CA1725 | Naming | no |
| CA1050 | Design | no |
| CA1861 | Performance | no |

- **0 supresiones por categoría** (`dotnet_analyzer_diagnostic.category-*`) y **0 con comodín**.
- **0 reglas de las categorías Security o Reliability.**
- Las 3 supresiones restantes son `[SuppressMessage]` apuntados a un miembro o a una clase
  (CA1822 ×1, CA1859 ×2), todas de categoría Performance, cada una con su `Justification`.

Este cuadro es el que hay que volver a mirar cuando alguien agregue la sexta supresión: un diff
contra esta tabla muestra de inmediato si la lista se corrió hacia seguridad.

### R-05 — La exclusión de código generado no se ensanchó

Verificado en las dos direcciones con un archivo escrito a mano y una violación deliberada de
CA1822:

- En `GestionGastos.Api/Common/` → **`error CA1822`, Build FAILED**.
- El mismo archivo movido a `GestionGastos.Api/Migrations/` → **Build succeeded, 0 errores**.

La exclusión alcanza exactamente al directorio declarado y a nada más.

## Supresiones de hallazgos SAST

**Ninguna.** No hubo hallazgos que suprimir.

## Resultado

```
┌─────────────────────────────────────────────────────────────┐
│  /daw-security-sast — PASSED                                 │
├─────────────────────────────────────────────────────────────┤
│  Secretos:        ✅ F-SAST-01                               │
│  Inyección:       ✅ F-SAST-02, 03, 05                       │
│  XSS / inseguras: ✅ F-SAST-04, 06, 17                       │
│  Cripto:          ⬜ F-SAST-08 no aplica                     │
│  Otros:           ⬜ F-SAST-07, 09, 10, 11, 12 no aplican     │
│  Validación:      ✅ F-SAST-14, 15                           │
│  Dependencias:    ✅ F-SAST-13, 16 — 0 vulnerables, 0 nuevas │
│  Supresiones:     0                                          │
│  ────────────────────────────────────────────────────────── │
│  Total: 0 vulnerabilidades (0 Critical, 0 High, 0 Medium)    │
└─────────────────────────────────────────────────────────────┘
```

---

# Ronda 2 — tras el bucle correctivo

| Field | Value |
|-------|-------|
| Fecha | 2026-08-20 |
| Motivo | Verificación ronda 1 BLOCKED: el test de regresión no existía en forma repetible |
| Alcance nuevo | `backend/verificar-linter.sh` (nuevo), `backend/.editorconfig`, `.github/workflows/ci.yml` |

## Superficie nueva: un script que escribe en el repo y compila

`backend/verificar-linter.sh` es la única superficie realmente nueva de esta ronda, y merece
análisis propio porque hace dos cosas que un script de verificación normalmente no hace: **escribe
archivos dentro del árbol de fuentes** y **ejecuta compilaciones**.

| Categoría | Resultado |
|---|---|
| F-SAST-01 secretos | ✅ 0 credenciales; no lee variables de entorno |
| F-SAST-03 inyección de comandos | ✅ No toma argumentos externos. La única expansión es `$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)`, entrecomillada; el `$1` de `escribirViolacion` es un parámetro interno con dos valores literales |
| F-SAST-05 path traversal | ✅ Las 4 rutas son literales derivadas de la ubicación del script, todas entrecomilladas. Ninguna sale de entrada de usuario |
| F-SAST-04 funciones inseguras | ✅ 0 `eval`, `curl`, `wget`. El heredoc está entrecomillado (`<<'CS'`), así que no expande nada |
| Residuos | ✅ `set -euo pipefail` + `trap limpiar EXIT`: el archivo temporal se borra aunque el script se interrumpa. Verificado con `git status` limpio tras cada corrida |
| Escalada de privilegios en CI | ✅ Corre con los permisos que el job ya tiene; no agrega `permissions` ni consume secretos |

**El archivo temporal se llama `_PruebaDelLinter.cs` y lleva un comentario que dice qué es y que hay
que borrarlo si aparece commiteado.** Es la mitigación de que quede colgado: un `.cs` suelto dentro
de `GestionGastos.Api/` entra en la compilación por el globbing del SDK y rompería el build de todos.

## Revalidación de R-02 tras el cambio de alcance

El fix de W-1 movió CA1725 y CA1050 de la sección global `[*.cs]` a `[GestionGastos.Api/**.cs]`. La
tabla de categorías **no cambia** y sigue siendo la referencia contra la que diffear una supresión
futura:

| Regla | Sección | Categoría | ¿Security o Reliability? |
|---|---|---|---|
| CA1725 | `[GestionGastos.Api/**.cs]` | Naming | no |
| CA1050 | `[GestionGastos.Api/**.cs]` | Design | no |
| CA1707 | `[GestionGastos.Api.Tests/**.cs]` | Naming | no |
| CA1711 | `[GestionGastos.Api.Tests/**.cs]` | Naming | no |
| CA1861 | `[GestionGastos.Api.Tests/**.cs]` | Performance | no |

**0 supresiones bajo `[*.cs]`.** Cada una vive ahora en la sección del proyecto al que su motivo
aplica, que además reduce el alcance respecto de la ronda 1.

## Revalidación de R-05

Lo que en la ronda 1 era una comprobación manual descrita en prosa ahora es el paso 3 de
`verificar-linter.sh`, y corre en el CI. Además se verificó que el script **falla** cuando la
barrera se desarma: con `EnforceCodeStyleInBuild=false` sale con código 1 y nombra el archivo a
revisar.

## Resultado ronda 2

```
Total: 0 vulnerabilidades (0 Critical, 0 High, 0 Medium) · 0 supresiones
Dependencias: 0 nuevas
```

