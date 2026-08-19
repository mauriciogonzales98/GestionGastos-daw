# Informe SAST — FEAT-001b

- **Ticket:** FEAT-001b — Filtros del listado, edición y eliminación de movimientos
- **Fase:** CODE (cierre) · **Gate bloqueante**
- **Fecha:** 2026-08-19
- **Alcance:** el delta completo del ticket contra `main` (`git diff main...HEAD`), 29 archivos —
  backend (filtros del listado, `PUT` y `DELETE` de movimientos) y frontend (cliente HTTP,
  controles de filtro, edición y eliminación con confirmación).
- **Reglas aplicadas:** `.daw/rules/validation/sast.md` (F-SAST-01 … F-SAST-19, W-SAST-01) junto con
  `.daw/rules/validation/common.md`.
- **Línea de base:** `sast-FEAT-001a.md`, cuyas 24 verificaciones limpias y 2 informativos siguen
  vigentes. Acá se re-verifica lo que este ticket tocó.

---

## 1. Secretos (F-SAST-01)

| ID | Verificación | Resultado |
|---|---|---|
| F-SAST-01 | Patrones de contraseña, API key, token y cadena de conexión sobre el delta completo del ticket | ✅ ninguno. Las dos únicas coincidencias son `CancellationToken`, que la expresión atrapa por el literal `token` |
| F-SAST-01 | `.gitignore` cubre `.env` y `.env.*` (líneas 33-34) | ✅ |
| F-SAST-01 | Archivos sensibles trackeados (`.env`, `.pem`, `.pfx`, `secrets.json`, `appsettings.{Development,Production}.json`) | ✅ ninguno |
| F-SAST-01 | La cadena de conexión sigue en user-secrets, fuera de `appsettings` (convención de `AGENTS.md`) | ✅ sin cambios en este ticket |

## 2. Inyección (F-SAST-02, F-SAST-03, F-SAST-05)

| ID | Verificación | Resultado |
|---|---|---|
| F-SAST-02 | `FromSql*`, `ExecuteSql*`, `RawSql`, `CommandText` e interpolación `$"SELECT/INSERT/UPDATE/DELETE"` en `backend/GestionGastos.Api/` | ✅ ninguna aparición |
| F-SAST-02 | Los tres filtros nuevos se aplican como predicados LINQ sobre el `IQueryable` (`MovimientosEndpoints.cs:135-152`): `m.CategoriaId == categoria`, `m.Fecha >= inicio`, `m.Fecha <= fin`. EF Core los parametriza; ningún valor del usuario se concatena al SQL | ✅ |
| F-SAST-02 | Los filtros entran al `IQueryable` **antes** del `CountAsync` y del `Take`, de modo que se resuelven en la base (mitigación R-13) | ✅ |
| F-SAST-03 | `Process.Start`, `child_process`, `exec`, `spawn` en backend y frontend | ✅ ninguna aparición |
| F-SAST-05 | Ninguna ruta de archivo se construye con entrada del usuario; este ticket no toca el sistema de archivos | ✅ |

## 3. XSS y funciones peligrosas (F-SAST-06, F-SAST-04, F-SAST-17)

| ID | Verificación | Resultado |
|---|---|---|
| F-SAST-06 | `dangerouslySetInnerHTML`, `innerHTML`, `outerHTML`, `document.write` en `frontend/src/` | ✅ ninguna aparición **real**. La única coincidencia es `ConfirmarEliminacion.tsx:94`, un comentario que documenta que está prohibido (mitigación R-23) |
| F-SAST-06 | La nota se interpola como hijo de texto JSX en el diálogo de confirmación y viaja por un `<textarea>` en la edición: React la escapa. Fijado por test — `Eliminar_LaNotaSeMuestraComoTextoPlano` y `Edicion_LaNotaConHtml_SeCargaComoTextoPlano` afirman que no aparece ningún `<img>` en el DOM | ✅ |
| F-SAST-04 / F-SAST-17 | `eval`, `new Function`, `BinaryFormatter`, `Activator.CreateInstance`, deserialización insegura | ✅ ninguna aparición en código propio. `EnableUnsafeBinaryFormatterSerialization` figura en `false` en el `runtimeconfig.json` generado por el build |
| F-SAST-08 | Criptografía | N/A — este ticket no introduce ninguna. El hash de contraseñas (RNF-03) llega con el ticket de autenticación |

## 4. Autorización, SSRF y superficie web (F-SAST-07, F-SAST-09, F-SAST-10, F-SAST-11, F-SAST-12)

| ID | Verificación | Resultado |
|---|---|---|
| — | **IDOR en los dos verbos nuevos.** `PUT` y `DELETE` localizan la fila con una lectura sobre `DbSet<Movimiento>`, donde el filtro global de propietario ya aplica (`MovimientosEndpoints.cs:219`, `:281`). No hay `ExecuteUpdate`/`ExecuteDelete`, que escribirían sobre filas ajenas devolviendo igual un 204 (mitigación R-15) | ✅ |
| — | **Enumeración.** "No existe" y "es de otro propietario" devuelven el mismo 404 con el mismo título (`TituloNoEncontrado`): distinguirlos confirmaría la existencia de una fila ajena | ✅ |
| — | **Overposting en el `PUT`.** Se asignan exactamente cuatro campos (`CategoriaId`, `Monto`, `Fecha`, `Nota`). El propietario, el tipo, la moneda y `CreadoEn` no se tocan aunque el cuerpo los traiga (mitigación R-15). El tipo nuevo se compara contra el **de la fila persistida**, no contra el del cuerpo | ✅ |
| F-SAST-07 | El cliente HTTP conserva la base relativa `const BASE = '/api'` (`cliente.ts:63`); ninguna URL sale de datos del usuario | ✅ |
| F-SAST-07 | La query string se arma con `URLSearchParams` (`cliente.ts:107-125`), que codifica los valores: un filtro no puede inyectar parámetros extra ni romper la ruta | ✅ |
| F-SAST-09 | Sin `EnableSensitiveDataLogging`, `EnableDetailedErrors` ni `DeveloperExceptionPage` | ✅ sin cambios |
| F-SAST-10 | El logging del frontend (`registrarEnConsola`, tres copias) escribe el mensaje del error y el `traceId`, nunca el cuerpo de la respuesta ni datos del movimiento | ✅ |
| F-SAST-11 | Subida de archivos | N/A — fuera de alcance |
| F-SAST-12 | Los dos verbos nuevos cambian estado, pero no hay autenticación por cookie ni sesión: el propietario sale de `UsuarioSemillaActual`. Sin cookies no hay vector CSRF. Sigue vigente el informativo I-01 de `sast-FEAT-001a.md`: cuando llegue el ticket de autenticación, `PUT` y `DELETE` entran en el alcance de la protección CSRF que se elija | ✅ |

## 5. Validación de entrada y manejo de errores (F-SAST-14, F-SAST-15)

| ID | Verificación | Resultado |
|---|---|---|
| F-SAST-14 | `FiltrosDeListado.Parsear` valida los tres parámetros de query: `categoriaId` rechaza `<= 0` como malformado (una categoría inexistente no es error: devuelve listado vacío); `desde`/`hasta` exigen el formato exacto `yyyy-MM-dd` con `TryParseExact` y `CultureInfo.InvariantCulture` (mitigación R-20), más el rango `FechaMinima`..`FechaMaxima`; el rango invertido se rechaza **antes de tocar la base** | ✅ |
| F-SAST-14 | Ausente o en blanco es "sin ese filtro", no un rechazo, y no degrada a "fecha mínima" ni a "categoría cero" | ✅ |
| F-SAST-14 | El `PUT` reusa `ValidadorMovimiento.Validar` — la **misma** implementación que el alta, a través de `IEntradaDeMovimiento`. No hay una segunda copia de las reglas que pueda divergir | ✅ |
| F-SAST-15 | Los mensajes de error de los filtros están redactados a mano; nunca se expone el detalle de la excepción del parseo (mitigación R-21) | ✅ |
| F-SAST-15 | `AddProblemDetails` + `UseExceptionHandler` global siguen en pie (`Program.cs:18,22`). Sin cambios en este ticket; los tests que afirman que el 500 no filtra `stackTrace` ni el mensaje interno siguen verdes | ✅ |

## 6. Dependencias (F-SAST-13, F-SAST-16)

| ID | Verificación | Resultado |
|---|---|---|
| F-SAST-13 / F-SAST-16 | `pnpm audit --audit-level moderate` → *No known vulnerabilities found* | ✅ |
| F-SAST-13 / F-SAST-16 | `dotnet list package --vulnerable --include-transitive` → sin paquetes vulnerables en `GestionGastos.Api` ni en `GestionGastos.Api.Tests` | ✅ |
| F-SAST-13 | Dependencias nuevas: **ninguna**. `git diff main...HEAD` sobre `frontend/package.json` y los `.csproj` no arroja cambios, como la spec previó | ✅ |

## Supresiones

Ninguna. No hubo hallazgos Medium que suprimir.

---

```
┌─────────────────────────────────────────────────────────────┐
│  /daw-security-sast — PASSED                                 │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Secretos:                                                   │
│    ✅ F-SAST-01: sin credenciales en el delta; .gitignore OK │
│                                                              │
│  Inyección:                                                  │
│    ✅ F-SAST-02: filtros como predicados LINQ, EF parametriza│
│    ✅ F-SAST-03 / F-SAST-05: sin superficie                  │
│                                                              │
│  XSS y funciones peligrosas:                                 │
│    ✅ F-SAST-06: la nota se escapa; R-23 fijada por test     │
│    ✅ F-SAST-04 / F-SAST-17: sin eval ni deserialización     │
│                                                              │
│  Autorización y superficie web:                              │
│    ✅ IDOR: PUT y DELETE bajo el filtro global (R-15)        │
│    ✅ F-SAST-07 / 09 / 10 / 12: sin hallazgos                │
│                                                              │
│  Validación y errores:                                       │
│    ✅ F-SAST-14: los tres filtros validados antes de la base │
│    ✅ F-SAST-15: mensajes propios, sin detalle de excepción  │
│                                                              │
│  Dependencias:                                               │
│    ✅ F-SAST-13 / F-SAST-16: pnpm audit y dotnet limpios     │
│                                                              │
│  Supresiones: 0                                              │
│                                                              │
│  ────────────────────────────────────────────────────────────│
│  Total: 26 verificaciones limpias, 0 vulnerabilidades         │
│         (0 Critical, 0 High, 0 Medium)                        │
│  Informativos: 0 nuevos (I-01 de FEAT-001a sigue vigente)     │
│  Report: docs/daw/security/sast-FEAT-001b.md                  │
│  Next: gates.sast = true, continuar al cierre (intento 1/3)   │
└─────────────────────────────────────────────────────────────┘
```
