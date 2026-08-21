# SAST FIX-002 — Bit de ejecución de verificar-linter.sh

| Field | Value |
|-------|-------|
| Ticket | FIX-002 |
| Tier | QUICK-FIX |
| Fecha | 2026-08-21 |
| Resultado | **PASSED** — 0 vulnerabilidades, 0 supresiones |

## Superficie analizada

El diff completo del ticket contra su base (`fix/FIX-001-linter-backend`):

| Archivo | Cambio |
|---|---|
| `backend/verificar-linter.sh` | modo `100644` → `100755`. **0 líneas de contenido**: el blob es el mismo (`faaeb83`), verificado byte a byte con `cmp` |
| `AGENTS.md` | 1 fila nueva en la tabla Stack, `Barrera del linter (backend)` |
| `docs/daw/prd/fix-FIX-002.md` + `.validation.md` | artefactos de DEFINE |

No hay código de producción tocado, ni esquema, ni endpoints, ni validación de entrada, ni
dependencias (`package.json`, `pnpm-lock.yaml` y los `.csproj` sin cambios).

## Resultado por regla

```
Secretos
  ✅ F-SAST-01: sin coincidencias de password/secret/token/api-key/connection string en el
     diff. `.env` y `.env.*` están en .gitignore (líneas 33-34)

Inyección
  ✅ F-SAST-02: sin SQL — no se tocó código de acceso a datos
  ✅ F-SAST-03: sin entrada de usuario alcanzando exec/spawn/system. El script no toma
     argumentos ni lee stdin; sus únicas rutas son constantes derivadas de BASH_SOURCE
  ✅ F-SAST-05: sin path traversal — las tres rutas del script son literales bajo $raiz

XSS y funciones inseguras
  ✅ F-SAST-04 / F-SAST-06 / F-SAST-17: no aplica, no hay frontend ni deserialización en el diff
  ✅ F-SAST-08: sin criptografía en el diff

Resto de categorías
  ✅ F-SAST-07 SSRF · F-SAST-09 debug en producción · F-SAST-10 logging de datos sensibles ·
     F-SAST-11 upload · F-SAST-12 CSRF · F-SAST-14 validación de entrada ·
     F-SAST-15 errores que filtran internals: sin superficie en este diff

Dependencias
  ✅ F-SAST-13 / F-SAST-16: ninguna dependencia agregada, quitada ni actualizada
```

## Lo único que este ticket cambia en la postura de seguridad

**Un archivo del repositorio pasa a ser ejecutable.** Vale decir por qué no agrega superficie:

- El contenido del script no cambió y ya estaba revisado en FIX-001 (informe
  `docs/daw/security/sast-FIX-001.md`). El blob es idéntico.
- El bit no le da al script ninguna capacidad que no tuviera. El CI ya lo invocaba —
  `run: ./backend/verificar-linter.sh` —, y cualquiera podía ejecutarlo con `bash backend/…`.
  Lo que faltaba era que la invocación existente funcionara.
- Es el **único** archivo con modo `100755` en el repositorio, así que la lista de ejecutables
  versionados sigue siendo revisable de un vistazo. Conviene que siga siéndolo.

El script escribe y borra dos archivos temporales dentro del árbol
(`GestionGastos.Api/Common/_PruebaDelLinter.cs` y el homónimo en `Migrations/`), con rutas
constantes y un `trap … EXIT` que los limpia en toda salida. No hay ruta derivada de entrada
externa, así que no hay escritura arbitraria que analizar.

## Supresiones

Ninguna.

## Veredicto

**PASSED.** 0 Critical, 0 High, 0 Medium, 0 Low. Gate `sast` cumplido.
