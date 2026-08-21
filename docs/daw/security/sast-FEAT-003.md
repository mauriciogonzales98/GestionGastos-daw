# SAST FEAT-003 — Alineación verificada del contrato frontend-backend

| Field | Value |
|-------|-------|
| Ticket | FEAT-003 |
| Tier | FEATURE |
| Fecha | 2026-08-21 |
| Resultado | **PASSED** — 0 vulnerabilidades, 0 supresiones |
| Threat model | `docs/daw/security/threat-FEAT-003.md` |

## Superficie analizada

15 archivos, 2269 líneas. Y el dato que ordena todo el análisis:

> **Ningún archivo de producción cambió.** Verificado, no afirmado:
> `git diff --name-only main...HEAD` filtrado por `backend/GestionGastos.Api/` y `frontend/src/`
> devuelve **vacío**.

| Qué | Dónde |
|---|---|
| 7 archivos de test | `backend/GestionGastos.Api.Tests/Contrato/` |
| 1 script | `backend/verificar-contrato.sh` |
| CI | `.github/workflows/ci.yml`, un paso nuevo |
| Documentación | `AGENTS.md`, ADR-004, PRD, spec, threat model |

## Resultado por regla

```
Secretos
  ✅ F-SAST-01: sin credenciales en el diff. Las únicas coincidencias de
     "token"/"secreto" son prosa del threat model. La cadena de conexión del
     paso nuevo de CI es la misma que ya usa el paso Tests: base efímera del
     runner, sin contraseña, sin nada que guardar en el repositorio

Inyección
  ✅ F-SAST-02: sin SQL nuevo. Las consultas son las que ya existían
  ✅ F-SAST-03: el script no toma argumentos ni lee stdin; sus rutas son
     constantes derivadas de BASH_SOURCE
  ✅ F-SAST-05: sin path traversal. El lector busca la raíz del repositorio
     subiendo hasta encontrar «.git» y compone una ruta relativa fija; no
     acepta ruta por parámetro ni por variable de entorno en producción

XSS y funciones inseguras
  ✅ F-SAST-04 / F-SAST-06 / F-SAST-17: sin eval, sin deserialización de datos
     no confiables, sin render de HTML. El frontend no se toca
  ✅ F-SAST-08: sin criptografía

Resto de categorías
  ✅ F-SAST-07 SSRF: las peticiones van a la API en memoria de
     WebApplicationFactory, no a la red
  ✅ F-SAST-09 debug en producción · F-SAST-10 logging de datos sensibles:
     los mensajes llevan nombres de campo y endpoints, nunca valores
  ✅ F-SAST-11 upload · F-SAST-12 CSRF · F-SAST-14 validación de entrada ·
     F-SAST-15 errores que filtran internals: sin superficie

Dependencias
  ✅ F-SAST-13 / F-SAST-16: 0 dependencias nuevas. git diff sobre
     frontend/package.json, pnpm-lock.yaml y los .csproj devuelve vacío.
     NFR-03 pedía como máximo 1 en el frontend y 0 en el backend; se entregó
     0 y 0, así que no hay superficie de cadena de suministro nueva
```

## Los dos riesgos HIGH del threat model, verificados sobre el código real

### R-02 — El comparador da por verificado lo que no comparó

**Cerrado, y comprobado ejecutándolo.** No alcanza con que el diseño lo prevea: se probó
desarmando la barrera de dos formas distintas y el script se puso en rojo en las dos, por vías
diferentes.

| Desarme aplicado | Quién lo atrapó |
|---|---|
| El comparador deja de reportar diferencias | El **paso 1** del script: los tests unitarios del comparador afirman que las diferencias *se reportan*, así que se caen solos |
| Un test de endpoint deja de afirmar | El **paso 2**: el rename deliberado del DTO ya no rompe nada, que es exactamente lo que ese paso busca |

Sumado a las defensas de diseño: el parser lanza ante lo que no entiende, un tipo nuevo sin
verificar ni excluir rompe un test, una colección vacía no se lee como éxito, y un endpoint que no
devuelve 2xx falla distinguiéndose de "el contrato coincide".

### R-06 — El script deja el backend modificado

**Cerrado.** El script guarda el original antes de tocarlo, lo restaura con `trap … EXIT` en toda
salida —incluida la interrumpida—, y al final comprueba con `git diff` que el archivo quedó como
estaba, saliendo distinto de 0 si no. Verificado en las tres corridas de esta sesión: `git status`
quedó limpio después de cada una, incluida la que terminó en rojo.

## Los dos riesgos MEDIUM, medidos

### R-01 — ReDoS en el parser

Los cuatro patrones, medidos contra 80 000 caracteres del peor caso:

| Patrón | Tiempo |
|---|---|
| `^export interface ([A-Za-z0-9]+) \{$` | 0,0000 s |
| `^export type ([A-Za-z0-9]+) = (.+);$` | 0,0000 s |
| `^([a-zA-Z0-9]+)(\?)?: (.+);$` | 0,0025 s |
| `^'([a-z]+)'$` | 0,0000 s |

Ninguno tiene cuantificadores anidados, y el parseo va línea por línea, así que la entrada de cada
match está acotada al largo de una línea y no al del archivo. Mitigación aplicada como el threat
model la pedía.

### R-04 — Recursión infinita por tipos mutuamente referenciados

Implementado y con test propio: el recorrido lleva los tipos visitados en la rama actual y lanza
nombrando el ciclo (`Comparador_ConTiposMutuamenteReferenciados_LanzaNombrandoElCiclo`).

## El cruce entre `backend/` y `frontend/`, desde la seguridad

`daw-validate-arch` marcó que los tests de `Contrato/` leen `frontend/src/api/tipos.ts`, estrenando
un cruce que no existía. Desde el ángulo de seguridad:

- Es **lectura**, en una sola dirección, de un archivo versionado y revisado por PR. No se importa
  código, no se comparte build, no se ejecuta nada del frontend.
- El archivo no contiene ni debe contener secretos: es una definición de forma, no de valores.
- La dirección inversa —el frontend leyendo algo del backend— sigue sin existir.

No agrega superficie de ataque. Queda declarado en `AGENTS.md` y en ADR-004 para que sea una
decisión y no un descuido.

## Supresiones

Ninguna.

## Veredicto

**PASSED.** 0 Critical, 0 High, 0 Medium, 0 Low. Gate `sast` cumplido.

---

## Ronda 2 — tras el bucle correctivo

| Field | Value |
|-------|-------|
| Fecha | 2026-08-21 |
| Resultado | **PASSED** — 0 vulnerabilidades, 0 supresiones |
| Motivo de la ronda | La verificación ronda 1 salió BLOCKED con 4 FAIL; el bucle correctivo agregó 10 tests, un archivo de configuración de cobertura y **un cambio en los disparadores del CI** |

### Superficie nueva

10 tests más, `backend/cobertura.runsettings`, y el cambio que sí merece análisis: el workflow pasa
a dispararse también con `push`.

### El cambio de disparador del CI, analizado

Es lo único de esta ronda con consecuencias de seguridad, así que va con detalle.

| Pregunta | Respuesta |
|---|---|
| ¿Un fork puede disparar el workflow ahora? | **No.** `push` sólo se dispara con commits empujados a ramas **de este repositorio**; un fork corre en su propio repositorio, con sus propios secretos (ninguno) y sin acceso a los de éste. La superficie de fork no cambia — la gobierna `pull_request`, que sigue igual |
| ¿Pide permisos nuevos? | **No.** `permissions: contents: read` sigue intacto, y ningún paso pide más |
| ¿Expone secretos a más gente? | **No.** El único valor sensible es la cadena de conexión de la base efímera del runner, sin contraseña, que ya viajaba en el archivo desde FEAT-002 y no es un secreto: es una base que vive lo que dura el job |
| ¿Amplía quién puede ejecutar código en el runner? | **No.** Quien puede pushear a este repositorio ya podía abrir un PR y ejecutar exactamente el mismo workflow. Cambia *cuándo* corre, no *quién* lo hace correr |
| ¿Duplica corridas y con eso el gasto? | Mitigado: el grupo de concurrencia pasó de `github.ref` a `github.head_ref \|\| github.ref_name`, así que el `push` y el `pull_request` del mismo commit caen en el mismo grupo y `cancel-in-progress` deduplica |

`backend/cobertura.runsettings` es configuración de instrumentación: dos opciones de coverlet, sin
rutas de red, sin credenciales, sin código ejecutable.

### Resultado por regla

Las mismas que la ronda 1, revalidadas sobre la superficie nueva:

```
  ✅ F-SAST-01 secretos · ✅ F-SAST-02/03/05 inyección · ✅ F-SAST-04/06/08/17
  ✅ F-SAST-07/09/10/11/12/14/15 · ✅ F-SAST-13/16 dependencias (siguen en 0)
```

**Y sigue siendo cierto lo que ordenaba el análisis de la ronda 1:** ningún archivo de producción
cambió en toda la rama. Reverificado con `git diff --name-only main...HEAD` filtrado por
`backend/GestionGastos.Api/` y `frontend/src/` — vacío.

### Supresiones

Ninguna.

### Veredicto

**PASSED.** 0 Critical, 0 High, 0 Medium, 0 Low. Gate `sast` recuperado.
