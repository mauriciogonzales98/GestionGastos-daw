# SAST — FEAT-001a: Alta de movimientos y listado simple

- **Ticket:** FEAT-001a
- **Fase:** CODE (secuencia de cierre, paso 3)
- **Fecha:** 2026-08-17
- **Alcance:** los cinco bloques completos — `backend/GestionGastos.Api/`,
  `backend/GestionGastos.Api.Tests/`, `frontend/src/` — más la configuración del repo
  (`appsettings*.json`, `.gitignore`, `backend/db/`).
- **Resultado:** **PASSED** — 0 Critical, 0 High, 0 Medium abiertos. 2 informativos, ninguno bloquea.
- **Reglas aplicadas:** `.daw/rules/validation-rules.instructions.md` §4 (F-SAST-01 … F-SAST-19,
  W-SAST-01).

---

## Secretos

| Regla | Verificación | Resultado |
|-------|--------------|-----------|
| F-SAST-01 | Patrones de contraseña, API key, token y cadena de conexión sobre `git ls-files` completo | ✅ |
| F-SAST-01 | `appsettings.json` sin sección `ConnectionStrings` — fijado por test (`ConfiguracionTests.Configuracion_AppsettingsNoContieneConnectionStrings`) | ✅ |
| F-SAST-01 | `.gitignore` cubre `.env`, `.env.*`, `appsettings.Development.json`, `appsettings.Local.json` | ✅ |
| F-SAST-01 | `backend/db/README.md:41` documenta la cadena con el marcador `<CONTRASEÑA>`, no un valor | ✅ |

**Corregido en este cierre:** `Infra/ManejoDeErroresTests.cs` conservaba
`User ID=gestiongastos;Password=;` en una cadena literal. Contraseña vacía y hacia un puerto cerrado
—no era una credencial utilizable—, pero era el patrón que la regla prohíbe y ya existía
`ApiFactory.CadenaHaciaUnPuertoCerrado`, sin usuario ni contraseña, para exactamente ese uso. El test
ahora la reutiliza.

Las apariciones de `ConnectionStrings` que quedan en el árbol son **nombres de clave de
configuración**, nunca valores: `Program.cs`, `ApiFactory.cs:49` y `ConfiguracionTests.cs`. Las de
`.claude/` son los ejemplos didácticos de las skills de DAW, fuera del código del producto.

## Inyección

| Regla | Verificación | Resultado |
|-------|--------------|-----------|
| F-SAST-02 | `FromSqlRaw` / `ExecuteSqlRaw` / `*Interpolated` en producción: **ninguna aparición** | ✅ |
| F-SAST-02 | Todo el acceso a datos de producción pasa por LINQ sobre EF Core, que parametriza | ✅ |
| F-SAST-02 | `MySqlCommand` aparece solo en tests. SQL constante en `Infra/MovimientosEnLaBase.cs`; en `Infra/BaseDeDatosFixture.cs:112,124` se interpolan **nombres de parámetro** (`@id0`, `@nombre0`), y los valores entran por `AddWithValue` | ✅ |
| F-SAST-03 | Sin `Process.Start`, `exec`, `spawn` ni `system` en ninguna capa | ✅ |
| F-SAST-05 | Ninguna ruta de archivo se construye con entrada del usuario | ✅ |

Mitigación R-05 del threat model verificada en el código, no solo en la spec.

## XSS y funciones inseguras

| Regla | Verificación | Resultado |
|-------|--------------|-----------|
| F-SAST-06 | `dangerouslySetInnerHTML`, `innerHTML`, `outerHTML`, `insertAdjacentHTML` en `frontend/src/`: **ninguna aparición** | ✅ |
| F-SAST-06 | La nota se renderiza como hijo de texto de JSX, que React escapa. Fijado por test: `ListadoMovimientos.test.tsx` → `Listado_NotaConHtml_SeMuestraComoTextoPlano` (mitigación R-06) | ✅ |
| F-SAST-04 | Sin `eval`, `new Function`, `setTimeout` con string ni deserialización insegura | ✅ |
| F-SAST-08 | No hay criptografía en este ticket: el hash de contraseñas (RNF-03) llega con el ticket de autenticación | N/A |

## SSRF, modo debug, logging y subidas

| Regla | Verificación | Resultado |
|-------|--------------|-----------|
| F-SAST-07 | El cliente HTTP del frontend usa la base relativa `const BASE = '/api'`; ninguna URL sale de datos del usuario | ✅ |
| F-SAST-09 | Sin `EnableSensitiveDataLogging`, `EnableDetailedErrors` ni `DeveloperExceptionPage`. `Kestrel` escucha en `127.0.0.1:5175` y `AllowedHosts` es `localhost;127.0.0.1` | ✅ |
| F-SAST-10 | `LogLevel.Default: Information`; sin logging sensible habilitado, EF no registra valores de parámetros | ✅ |
| F-SAST-11 | No hay subida de archivos en el alcance | N/A |
| F-SAST-12 | No hay autenticación por cookie ni sesión: el propietario sale de `UsuarioSemillaActual`. Sin cookies no hay vector CSRF. Ver informativo I-01 | ✅ |

## Validación de entrada y manejo de errores

| Regla | Verificación | Resultado |
|-------|--------------|-----------|
| F-SAST-14 | `ValidadorMovimiento` valida los cinco campos del alta: categoría (ausente / nula / 0 / negativa), monto (obligatorio, > 0, ≤ 2 decimales, techo de `decimal(15,2)`, no numérico), fecha (formato **y rango**), nota (largo ≤ 120), `tipoEsperado` (enumerado) | ✅ |
| F-SAST-14 | El tipo se deriva de la categoría en el servidor y `usuarioId` / `moneda` del cuerpo se ignoran (mitigación R-04, overposting) | ✅ |
| F-SAST-15 | `AddProblemDetails` + `UseExceptionHandler` global. Tests que afirman que el cuerpo del 500 **no** contiene `stackTrace`, `   at `, `MySql` ni el mensaje interno, y que sí trae `traceId` | ✅ |
| F-SAST-15 | El 400 por JSON malformado no filtra `JsonReaderException`, `LineNumber` ni `BytePositionInLine` | ✅ |

**Corregido en este cierre (F-SAST-14):** `ValidadorMovimiento.ValidarFecha` validaba el formato pero
no el rango. `"0001-01-01"` parsea como `DateOnly` válido y el tipo `DATE` de MySQL arranca en
`1000-01-01`: la solicitud llegaba al INSERT y el error del proveedor habría salido como 500 donde el
contrato promete 400 — el mismo modo de fallo que el endpoint ya evitaba para la categoría
inexistente. Se agregaron `FechaMinima` / `FechaMaxima` y cuatro casos de test (dos de rechazo, dos en
los bordes inclusivos).

## Dependencias

| Regla | Verificación | Resultado |
|-------|--------------|-----------|
| F-SAST-13 / F-SAST-16 | `pnpm audit --audit-level moderate` → *No known vulnerabilities found* | ✅ |
| F-SAST-13 / F-SAST-16 | `dotnet list package --vulnerable --include-transitive` → sin paquetes vulnerables en `GestionGastos.Api` ni en `GestionGastos.Api.Tests` | ✅ |
| F-SAST-13 | Sin dependencias nuevas respecto de la spec | ✅ |

## Supresiones

**Ninguna.** No hubo hallazgos Medium que suprimir: los dos que aparecieron (la credencial residual y
el rango de fecha sin validar) se corrigieron, que es lo que §4.4 prefiere sobre documentar una
excepción.

## Informativos (W-SAST-01 — no bloquean)

- **I-01 — CSRF y CORS se deciden en el ticket de autenticación.** Hoy no hay cookies, sesión ni
  cabecera de autorización: el propietario lo resuelve `UsuarioSemillaActual` contra la fila semilla,
  así que no existe credencial ambiental que un sitio de terceros pueda hacer viajar. Tampoco hay
  CORS configurado, y esa omisión juega a favor: el navegador bloquea por defecto cualquier origen
  cruzado. En cuanto la autenticación introduzca cookie de sesión, hacen falta las dos cosas
  —`SameSite` y antiforgery— y esta línea deja de ser informativa.
- **I-02 — `movimientos.tipo` es un dato denormalizado sin garantía en la base.** Una FK compuesta
  `(categoria_id, tipo) → categorias(id, tipo)` lo cerraría. No es un hallazgo de seguridad: por la
  API no existe camino de divergencia, porque el tipo se deriva siempre de la categoría en el
  servidor. Anotado por integridad de datos, ya registrado como deuda del ticket.

---

## Resumen

```
Total: 24 verificaciones limpias, 0 vulnerabilidades (0 Critical, 0 High, 0 Medium)
Corregidos durante el cierre: 2 (F-SAST-01 credencial residual en test, F-SAST-14 rango de fecha)
Supresiones: 0
Informativos: 2
```

**Gate:** `gates.sast` = `true`.

---

## Ronda 2 — 2026-08-17 — re-escaneo tras el bucle correctivo de VERIFY

VERIFY volvió BLOCKED (ver `docs/daw/reports/verify-FEAT-001a.md`) y el bucle correctivo cambió
código, así que el gate se vuelve a pagar. **No es un re-sello del anterior:** se reescaneó el delta
completo y se repitieron las categorías obligatorias.

**Delta auditado** respecto de `e337cd0`, el commit que cerró la ronda 1:

| Archivo | Cambio |
|---------|--------|
| `backend/GestionGastos.Api/Common/ResultadoValidacion.cs` | −2: borrada la propiedad muerta `Errores` |
| `frontend/src/App.test.tsx` | nuevo: 2 tests de la costura alta → listado |
| `frontend/src/test/infra.ts` | nuevo: helper `json` compartido |
| `frontend/src/movimientos/FormularioMovimiento.test.tsx` | +2 tests de claves de error cruzadas |
| `frontend/src/movimientos/ListadoMovimientos.test.tsx` | consume el `json` compartido |

**Superficie de ataque: sin cambios.** El único archivo de producción que se tocó fue para **quitar**
un miembro público sin llamadores; todo lo demás es código de test. No se agregó ningún endpoint,
ninguna entrada de usuario, ninguna consulta ni ninguna dependencia.

| Regla | Verificación | Resultado |
|-------|--------------|-----------|
| F-SAST-01 | Patrones de credencial en el delta completo (`git diff e337cd0`) | ✅ ninguno |
| F-SAST-02 | `FromSqlRaw` / `ExecuteSqlRaw` / `*Interpolated` en producción | ✅ ninguna aparición |
| F-SAST-04 / F-SAST-06 | `dangerouslySetInnerHTML`, `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `eval`, `new Function` en `frontend/src/` | ✅ ninguna aparición |
| F-SAST-14 | Sin cambios en la validación de entrada: `ValidadorMovimiento` intacto desde la ronda 1 | ✅ |
| F-SAST-15 | Sin cambios en el manejo de errores. `ResultadoValidacion` conserva `ComoDiccionario()` como única salida y `_errores` sigue encapsulado | ✅ |
| F-SAST-13 / F-SAST-16 | `pnpm audit --audit-level moderate` → *No known vulnerabilities found*; `dotnet list package --vulnerable --include-transitive` → sin paquetes vulnerables | ✅ |
| — | 0 `any`, 0 `console.log` en `frontend/src/` | ✅ |

**Nota sobre el helper nuevo.** `frontend/src/test/infra.ts` construye respuestas HTTP falsas para los
tests con `new Response(JSON.stringify(...))`. No entra en el bundle de producción —`vite build` solo
sigue el grafo desde `src/main.tsx`— y no ejecuta nada que venga de una fuente externa.

```
Ronda 2: 7 verificaciones limpias, 0 vulnerabilidades (0 Critical, 0 High, 0 Medium)
Supresiones: 0 · Informativos nuevos: 0 (los dos de la ronda 1 siguen vigentes)
```

**Gate:** `gates.sast` = `true` (reganado).
