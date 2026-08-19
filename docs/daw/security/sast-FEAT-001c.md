# Informe SAST — FEAT-001c

- **Ticket:** FEAT-001c — Resumen del mes con desglose por categoría
- **Fase:** CODE (cierre) · **Gate bloqueante**
- **Fecha:** 2026-08-19
- **Alcance:** el delta completo del ticket contra `main` (`git diff main...HEAD`), 22 archivos —
  backend (endpoint de resumen con agregación en SQL, corte del mes, medición de rendimiento) y
  frontend (cliente HTTP, tipos del contrato, componente del resumen y su montaje en `App`).
- **Reglas aplicadas:** `.daw/rules/validation/sast.md` (F-SAST-01 … F-SAST-19, W-SAST-01) junto con
  `.daw/rules/validation/common.md`.
- **Línea de base:** `sast-FEAT-001a.md` y `sast-FEAT-001b.md`, cuyas verificaciones limpias siguen
  vigentes. Acá se re-verifica lo que este ticket tocó.
- **Threat model de referencia:** `threat-FEAT-001c.md`, riesgos R-24 … R-29.

---

## 1. Secretos (F-SAST-01)

| ID | Verificación | Resultado |
|---|---|---|
| F-SAST-01 | Patrones de contraseña, API key, token y cadena de conexión sobre el delta completo | ✅ ninguno. Las coincidencias del patrón son léxicas: `CancellationToken` por el literal `token`, y `tokens.css` por el nombre del archivo de estilos |
| F-SAST-01 | `.gitignore` cubre `.env` y `.env.*` (líneas 33-34) | ✅ |
| F-SAST-01 | Archivos sensibles trackeados (`.env`, `.pem`, `.pfx`, `secrets.json`, `appsettings.{Development,Production}.json`) | ✅ ninguno |
| F-SAST-01 | La cadena de conexión sigue en user-secrets, fuera de `appsettings` (convención de `AGENTS.md`) | ✅ sin cambios en este ticket |

## 2. Inyección (F-SAST-02, F-SAST-03, F-SAST-05)

| ID | Verificación | Resultado |
|---|---|---|
| F-SAST-02 | `FromSql*`, `ExecuteSql*`, `RawSql`, `CommandText` e interpolación `$"SELECT/INSERT/UPDATE/DELETE"` en `backend/GestionGastos.Api/` | ✅ ninguna aparición |
| F-SAST-02 | **El endpoint no recibe ningún parámetro**: no hay entrada del usuario que pueda llegar a la consulta. El rango lo calcula `RangoDelMes.De` a partir del reloj del servidor, y las dos agregaciones son LINQ sobre el `IQueryable`, que EF Core traduce y parametriza | ✅ |
| F-SAST-03 | `Process.Start`, `child_process`, `exec`, `spawn` en backend y frontend | ✅ ninguna aparición |
| F-SAST-05 | Ninguna ruta de archivo se construye con entrada del usuario; este ticket no toca el sistema de archivos | ✅ |

## 3. XSS y funciones peligrosas (F-SAST-06, F-SAST-04, F-SAST-17)

| ID | Verificación | Resultado |
|---|---|---|
| F-SAST-06 | `dangerouslySetInnerHTML`, `innerHTML`, `outerHTML`, `document.write` en `frontend/src/` | ✅ ninguna aparición **real**. La única coincidencia sigue siendo `ConfirmarEliminacion.tsx:94`, un comentario que documenta la prohibición (mitigación R-23 de FEAT-001b) |
| F-SAST-06 | Los nombres de categoría del desglose se interpolan como hijos de texto JSX, que React escapa. A diferencia de la nota del movimiento, acá **no se renderiza ningún texto escrito por el usuario**: el catálogo de categorías es global y sembrado (superficie menor que en `a` y `b`) | ✅ |
| F-SAST-06 | La prohibición de `dangerouslySetInnerHTML` está fijada por regla de ESLint para todo el proyecto desde FEAT-001b, no por disciplina de quien escribe el componente (mitigación 9 del threat model) | ✅ |
| F-SAST-04 / F-SAST-17 | `eval`, `new Function`, `BinaryFormatter`, deserialización insegura | ✅ ninguna aparición en código propio |
| F-SAST-08 | Criptografía | N/A — este ticket no introduce ninguna |

## 4. Autorización, SSRF y superficie web (F-SAST-07, F-SAST-09, F-SAST-10, F-SAST-11, F-SAST-12)

| ID | Verificación | Resultado |
|---|---|---|
| — | **R-24 — fuga por agregación (el riesgo Alto del threat model).** La consulta parte de `datos.Movimientos` sin cláusula de propietario, apoyándose en el filtro global de `AppDbContext`, igual que `ListarAsync`. `grep IgnoreQueryFilters` sobre `backend/GestionGastos.Api/` no arroja **ninguna** aparición | ✅ |
| — | La mitigación está fijada por `Resumen_DeOtroPropietario_NoEntraEnLosTotales`, y la **mutación está registrada**: agregar `IgnoreQueryFilters()` pone ese test —y solo ese— en rojo, con `Expected: 10 / Actual: 1009.00`. Corrida dos veces, por el implementador y de forma independiente por el verificador | ✅ |
| — | La firma de la fuga es la que el threat model anticipaba: **no aparece una fila de más, aparece un número más grande**. Por eso el control es un test y no una inspección visual | ✅ |
| — | **R-25 — agregación evaluada en memoria.** `Resumen_EmiteLaAgregacionEnSql` (con `ObservadorDeSql`) es un control de seguridad y no solo de rendimiento: es lo único que distingue una agregación traducida de una evaluada en cliente, porque **ambas devuelven los mismos números**. La mutación que reemplaza el `GroupBy` en SQL por `.Include(...).ToListAsync()` + `GroupBy` en memoria, preservando el resultado, deja los otros 15 tests verdes y solo mata el estructural | ✅ |
| — | Reforzado a escala por `Resumen_ConMilMovimientos_NoMaterializaFilasDeMovimientos`: con 1000 movimientos la respuesta sigue trayendo a lo sumo una fila por categoría | ✅ |
| F-SAST-07 | El cliente HTTP conserva la base relativa `const BASE = '/api'`; `obtenerResumen()` **no construye query string** ni toma parámetros, así que ninguna URL sale de datos del usuario | ✅ |
| F-SAST-09 | Sin `EnableSensitiveDataLogging`, `EnableDetailedErrors` ni `DeveloperExceptionPage` (grep sin resultados) | ✅ |
| F-SAST-10 | `registrarEnConsola` en `ResumenDelMes.tsx:167` escribe un mensaje propio y el objeto de error, nunca el cuerpo de la respuesta ni importes del usuario. Mismo patrón que las tres copias ya auditadas en FEAT-001b | ✅ |
| F-SAST-11 | Subida de archivos | N/A — fuera de alcance |
| F-SAST-12 | El endpoint nuevo es de **solo lectura** (`GET`) y no cambia estado: no agrega superficie CSRF. Sigue vigente el informativo I-01 de `sast-FEAT-001a.md` para cuando llegue la autenticación | ✅ |

## 5. Validación de entrada y manejo de errores (F-SAST-14, F-SAST-15)

| ID | Verificación | Resultado |
|---|---|---|
| F-SAST-14 | **No hay entrada que validar, y es una decisión registrada, no un olvido** (mitigación R-27): el contrato declara cero parámetros porque el período lo fija el servidor, lo que garantiza FR-03 en el endpoint y no en la buena conducta del cliente. Agregar un parámetro de período en el futuro exige discutir FR-03 primero |  ✅ |
| F-SAST-14 | **R-28 — bordes del corte del mes.** `RangoDelMes` se aísla en su propio archivo y deriva el último día de `primerDia.AddMonths(1).AddDays(-1)`, sin casos especiales. Tres tests de borde (diciembre, febrero bisiesto, primer/último día) más `Resumen_ConMovimientosElPrimeroYElUltimoDiaDelMes_LosIncluye` de punta a punta. Un rango mal calculado no falla: incluye o excluye montos en silencio | ✅ |
| F-SAST-15 | **R-26 — el 500 no filtra internos.** `AddProblemDetails` + `UseExceptionHandler` globales siguen en pie; el endpoint devuelve un `Ok` pelado y deja que el fallo de base se propague al manejador global. Fijado por `Resumen_ConLaBaseCaida_Devuelve500SinDetalleInterno`, que asserta que el cuerpo no trae `stackTrace`, `   at ` ni el mensaje interno | ✅ |
| F-SAST-15 | En el frontend, `describirFallo` devuelve mensajes redactados a mano (`'No pudimos cargar el resumen del mes…'`) y nunca interpola el mensaje de la excepción en la pantalla | ✅ |
| — | **R-29 — el fallo del resumen no tumba el listado.** Las dos peticiones son independientes y sus errores también. Fijado por `Resumen_ConErrorDelServidor_NoTumbaElListado` | ✅ |

## 6. Dependencias (F-SAST-13, F-SAST-16)

| ID | Verificación | Resultado |
|---|---|---|
| F-SAST-13 / F-SAST-16 | `pnpm audit` → *No known vulnerabilities found* | ✅ |
| F-SAST-13 / F-SAST-16 | `dotnet list package --vulnerable --include-transitive` → sin paquetes vulnerables en `GestionGastos.Api` ni en `GestionGastos.Api.Tests` | ✅ |
| F-SAST-13 | Dependencias nuevas: **ninguna**. `frontend/package.json` y los `.csproj` sin cambios en el delta, como la spec previó | ✅ |

## Supresiones

Ninguna. No hubo hallazgos Medium que suprimir.

---

## Nota sobre los riesgos del threat model

Los seis riesgos que `threat-FEAT-001c.md` levantó para este ticket (R-24 … R-29) tienen control
implementado y test que lo fija. Los dos que el modelo marcaba como **invisibles** —R-24, la fuga que
agranda un número en vez de agregar una fila; y R-25, la agregación en memoria que devuelve el
resultado correcto— fueron verificados por **mutación**, corrida dos veces cada una y por dos agentes
distintos. Es la única forma de comprobar un control cuya falla no se ve en la respuesta.

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
│    ✅ F-SAST-02: el endpoint no toma parámetros; LINQ en SQL │
│    ✅ F-SAST-03 / F-SAST-05: sin superficie                  │
│                                                              │
│  XSS y funciones peligrosas:                                 │
│    ✅ F-SAST-06: categorías como texto JSX; regla ESLint     │
│    ✅ F-SAST-04 / F-SAST-17: sin eval ni deserialización     │
│                                                              │
│  Autorización y superficie web:                              │
│    ✅ R-24: sin IgnoreQueryFilters; mutación registrada      │
│    ✅ R-25: agregación en SQL fijada por test estructural    │
│    ✅ F-SAST-07 / 09 / 10 / 12: sin hallazgos                │
│                                                              │
│  Validación y errores:                                       │
│    ✅ F-SAST-14: cero parámetros por contrato (R-27, R-28)   │
│    ✅ F-SAST-15: 500 sin detalle interno (R-26)              │
│                                                              │
│  Dependencias:                                               │
│    ✅ F-SAST-13 / F-SAST-16: pnpm audit y dotnet limpios     │
│                                                              │
│  Supresiones: 0                                              │
│                                                              │
│  ────────────────────────────────────────────────────────────│
│  Total: 25 verificaciones limpias, 0 vulnerabilidades         │
│         (0 Critical, 0 High, 0 Medium)                        │
│  Informativos: 0 nuevos (I-01 de FEAT-001a sigue vigente)     │
│  Report: docs/daw/security/sast-FEAT-001c.md                  │
│  Next: gates.sast = true, continuar al cierre (intento 1/3)   │
└─────────────────────────────────────────────────────────────┘
```
