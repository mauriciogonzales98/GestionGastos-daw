# Threat model — FEAT-001c: Resumen del mes con desglose por categoría

| Field | Value |
|-------|-------|
| Ticket | FEAT-001c |
| PRD | docs/daw/prd/prd-FEAT-001c.md |
| Spec | docs/daw/specs/spec-FEAT-001c.md |
| Date | 2026-08-19 |
| Reglas | `.daw/rules/validation/threat.md` con `.daw/rules/validation/common.md` (F-TM-01 … F-TM-07, W-TM-01, W-TM-02) |

**Continuidad de numeración.** Los componentes siguen desde C14 y los riesgos desde R-23, los últimos
que usó `threat-FEAT-001b.md`. Los riesgos aceptados **RA-01** (sin autenticación), **RA-02** (sin
cifrado), **RA-03** (sin auditoría) y **RA-04** (sin anti-CSRF) siguen vigentes y no se reabren acá;
este ticket no introduce nada que cambie sus condiciones de aceptación.

**Superficie de este ticket, en una línea:** un endpoint de **solo lectura**, **sin parámetros**, que
devuelve datos **agregados**. Es la superficie más chica de los tres sub-tickets — y precisamente por
eso el riesgo interesante no es el clásico de entrada no confiable, sino que un error de consulta
filtre datos ajenos **en una forma que nadie puede ver a simple vista**.

---

## 1. Componentes y superficies de ataque

| # | Componente | Superficie |
|---|---|---|
| C15 | `GET /api/resumen` | Lectura. **No acepta entrada**: ni ruta, ni query, ni cuerpo. No hay superficie de inyección por parámetros porque no hay parámetros |
| C16 | La agregación `GroupBy`/`Sum` sobre `IQueryable` | Fuga de datos y consumo de recursos. **Sin precedente en el repositorio** |
| C17 | `RangoDelMes` | Lógica de corte temporal. Un rango mal calculado incluye o excluye datos silenciosamente |
| C18 | Frontend `ResumenDelMes` | Presentación de datos financieros agregados |

## 2. Límites de confianza

| # | Cruce | Niveles |
|---|---|---|
| TB-1 | Navegador → API (C1 → C15) | No confiable → confiable. **Se angosta en este ticket**: es el primer endpoint que no recibe absolutamente nada del cliente. Nada que validar es nada que explotar por esa vía |
| TB-2 | API → MySQL | Confiable → confiable con credencial. Sin `FromSqlRaw`: la agregación se expresa en LINQ y la parametriza EF |
| TB-5 | **Handler → filtro global de EF** (C15, C16 → `AppDbContext`) | Sigue siendo el límite real de autorización, igual que en `b`. Acá es más delicado: del lado de allá salen **números sumados**, no filas — y un número contaminado no se distingue de uno correcto |

## 3. Clasificación de datos

| Dato | Clasificación | Novedad de este ticket |
|---|---|---|
| Totales del mes (ingresado, gastado, balance) | **Financiero** | Nuevo. Es un dato **derivado**: revela el volumen de actividad del usuario en un solo número |
| Desglose de gastos por categoría | **Financiero, con perfilado** | Nuevo. Más revelador que los montos sueltos: dice *en qué* gasta la persona, que es información de comportamiento, no solo de plata |
| Mes y año del período | Público | Nuevo, sin valor por sí mismo |
| Nombres de categorías | Público — el catálogo es global y sembrado | Sin cambios |

**Nota sobre el desglose.** Vale registrarlo aunque no cambie ninguna decisión: un desglose por
categoría es, en agregado, **más sensible** que la misma información dispersa en un listado. "Gastó
$40.000 en Salud este mes" es un dato de perfilado que el listado obligaba a reconstruir a mano. No
introduce un control nuevo en este ticket —la protección es la misma, el filtro de propietario— pero
sube el impacto de cualquier fuga, y por eso R-24 se clasifica como HIGH y no como MEDIUM.

**Cifrado (F-TM-07):** sigue vigente **RA-02** de `threat-FEAT-001a.md` — sin TLS ni cifrado en
reposo, sobre HTTP en `127.0.0.1` con datos de desarrollo. Este ticket no introduce credenciales ni
PII nuevas: los datos agregados derivan de movimientos que ya estaban cubiertos por esa aceptación.

## 4. Análisis STRIDE por componente

### C15 — `GET /api/resumen`

| Categoría | Análisis |
|---|---|
| **Spoofing** | El propietario sale de `IUsuarioActual`, nunca de la petición. Sin autenticación sigue valiendo RA-01; este endpoint no la empeora ni la mejora |
| **Tampering** | No hay entrada que manipular. Es la propiedad de seguridad más fuerte de este endpoint y es deliberada: FR-03 queda garantizado por el contrato, no por la buena conducta del cliente → **R-27** |
| **Repudiation** | Es una lectura: no hay acción que negar. Sin logging de accesos, coherente con RA-03 |
| **Information Disclosure** | El riesgo central del ticket → **R-24**, **R-26** |
| **Denial of Service** | Sin techo de filas y sin caché → **R-25** |
| **Elevation of Privilege** | No hay roles en el sistema. Sin superficie |

### C16 — La agregación `GroupBy`/`Sum`

| Categoría | Análisis |
|---|---|
| **Tampering** | La consulta se construye en LINQ, sin concatenación ni `FromSqlRaw`. EF la parametriza |
| **Information Disclosure** | Si la consulta esquivara el filtro global —con `IgnoreQueryFilters()` o partiendo de un contexto sin filtro— los totales incluirían plata ajena **sin que se note**: no aparece una fila de más, aparece un número más grande → **R-24** |
| **Denial of Service** | Si EF no logra traducir la agregación, la evalúa en memoria: trae **todas** las filas del propietario por cada carga de pantalla. El endpoint no tiene un `TechoDeItems` como el listado, así que no hay nada que acote esa materialización → **R-25** |

### C17 — `RangoDelMes`

| Categoría | Análisis |
|---|---|
| **Tampering** | No recibe entrada del usuario: recibe la fecha del servidor. Un cliente no puede desplazar el período |
| **Information Disclosure** | Un rango mal calculado —diciembre corriéndose al año siguiente, febrero bisiesto— incluiría o excluiría movimientos en silencio. No es una fuga hacia otro usuario, pero sí un número incorrecto presentado como correcto → **R-28** |

### C18 — Frontend `ResumenDelMes`

| Categoría | Análisis |
|---|---|
| **Tampering** | No acepta entrada: no tiene controles. Solo muestra |
| **Information Disclosure** | Renderiza nombres de categorías, que vienen del catálogo global sembrado y no de texto libre del usuario. **No hay superficie de XSS nueva**: a diferencia de la nota (R-06, R-23), acá no se muestra ningún texto que el usuario haya escrito. Aun así el nombre se interpola como texto JSX, nunca con `dangerouslySetInnerHTML`, que sigue prohibido por la regla de ESLint que `b` dejó activa para todo el proyecto |
| **Denial of Service** | Si el resumen falla, no debe tumbar el listado: son dos peticiones independientes → **R-29** |

## 5. Riesgos y mitigaciones

| # | Riesgo | STRIDE | Prob. | Impacto | Mitigación |
|---|---|---|---|---|---|
| R-24 | La agregación esquiva el filtro global y suma movimientos ajenos. **La fuga es invisible**: no aparece una fila de más, solo un número mayor | I | Baja | **Alto** | La consulta parte de `datos.Movimientos` sin cláusula de propietario, apoyándose en el filtro global (`AppDbContext.cs:32`), igual que `ListarAsync`. Prohibido `IgnoreQueryFilters()`. Fijado por `Resumen_DeOtroPropietario_NoEntraEnLosTotales`, y el reporte TDD debe registrar la **mutación**: agregar `IgnoreQueryFilters()` tiene que poner ese test en rojo |
| R-25 | La agregación se evalúa en memoria y materializa todas las filas del propietario en cada carga de pantalla, sin techo que lo acote | D | **Media** | Medio | El test estructural `Resumen_EmiteLaAgregacionEnSql` con `ObservadorDeSql` **es un control de seguridad, no solo de rendimiento**: es lo único que distingue una agregación traducida de una evaluada en cliente, porque ambas devuelven los mismos números. Reforzado por `Resumen_ConMilMovimientos_NoMaterializaFilasDeMovimientos` |
| R-26 | El 500 por fallo de base filtra el mensaje del proveedor, la cadena de conexión o el stack trace | I | Baja | Medio | `AddProblemDetails` + `UseExceptionHandler` globales, ya en pie desde FEAT-001a. Fijado por `Resumen_ConLaBaseCaida_Devuelve500SinDetalleInterno`, que asserta que el cuerpo no trae `stackTrace`, `   at ` ni el mensaje interno |
| R-27 | Un parámetro de período agregado "por comodidad" en el futuro convierte el endpoint en un resumen de rango arbitrario y rompe la garantía de FR-03 | T | Baja | Medio | El contrato declara explícitamente **cero parámetros**, con el motivo escrito en la spec. Es una decisión registrada, no un olvido: quien quiera agregar un período tiene que discutir FR-03 primero |
| R-28 | El corte del mes se equivoca en los bordes —diciembre, año bisiesto, primer y último día— e incluye o excluye montos en silencio | T | Media | Medio | `RangoDelMes` se aísla en su propio archivo para probarlo sin base, con tres tests de borde (`RangoDelMes_EnDiciembre_NoSeCorreAlAnioSiguiente`, `..._EnFebreroDeAnioBisiesto_TerminaEl29`, `..._DevuelveElPrimeroYElUltimoDiaDelMes`), más `Resumen_ConMovimientosElPrimeroYElUltimoDiaDelMes_LosIncluye` de punta a punta |
| R-29 | El fallo del resumen tumba la pantalla entera y el usuario pierde también el listado | D | Media | Bajo | Las dos peticiones son independientes y sus errores también. Fijado por `Resumen_ConErrorDelServidor_NoTumbaElListado` |
| R-30 | El resumen se cablea a `filtros` en vez de a `version` y pasa a reflejar el rango filtrado, violando FR-03/AC-06 sin que nadie lo note mirando la pantalla una vez | T | **Media** | Medio | La spec fija el disparador explícitamente en el Block 4. Fijado en las dos direcciones: `Resumen_AlFiltrarElListado_NoCambia` y `Resumen_TrasUnAlta_SeActualiza`, ambos montando `App` |

**Ningún riesgo CRITICAL.** El más alto es R-24, con impacto alto y probabilidad baja: la mitigación
es el mismo mecanismo que ya protege el listado, la edición y el borrado, y el ticket no inventa una
segunda forma de filtrar por propietario. Lo que sube el impacto respecto de `b` no es la
probabilidad de la falla sino **la dificultad de detectarla**, y por eso la mitigación exige la
mutación registrada y no solo el test verde.

## 6. Riesgos aceptados (F-TM-04)

**Ninguno nuevo.** Los cuatro vigentes —RA-01 sin autenticación, RA-02 sin cifrado, RA-03 sin
auditoría, RA-04 sin anti-CSRF— siguen como estaban. RA-04 en particular **no se extiende** a este
ticket: CSRF requiere una operación con efectos, y `GET /api/resumen` es de solo lectura y sin
efectos secundarios.

## 7. Mitigaciones a incorporar a la spec

1. La consulta del resumen parte de `datos.Movimientos` apoyándose en el filtro global; `IgnoreQueryFilters()` queda prohibido (R-24).
2. El reporte TDD del Block 1 registra la mutación de `IgnoreQueryFilters()` y el test que la mata (R-24).
3. `Resumen_EmiteLaAgregacionEnSql` es requisito de cierre del Block 1, como control de seguridad además de rendimiento (R-25).
4. `Resumen_ConLaBaseCaida_Devuelve500SinDetalleInterno` verifica que el 500 no filtre internals (R-26).
5. El endpoint no acepta parámetros, y el motivo queda escrito en el contrato (R-27).
6. `RangoDelMes` vive aislado y se prueba sin base, con los tres bordes (R-28).
7. Un fallo del resumen no tumba el listado (R-29).
8. El resumen se suscribe a `version` y no a `filtros`, verificado en las dos direcciones montando `App` (R-30).
9. Los nombres de categoría se interpolan como texto JSX; `dangerouslySetInnerHTML` sigue prohibido por ESLint (C18).

Las nueve están ya incorporadas a `spec-FEAT-001c.md`.

## 8. Resumen

```
┌─────────────────────────────────────────────────────────┐
│  /daw-threat-modeling — PASSED                           │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Superficies de ataque identificadas: 4 (C15–C18)        │
│  Límites de confianza declarados: 3 (TB-1, TB-2, TB-5)   │
│                                                          │
│  Riesgos:                                                │
│    🟠 HIGH:   R-24 — fuga invisible por agregación sin   │
│                      filtro de propietario               │
│    🟡 MEDIUM: R-25 — agregación evaluada en memoria      │
│               R-26 — 500 con detalle interno             │
│               R-27 — parámetro de período a futuro       │
│               R-28 — bordes del corte del mes            │
│               R-30 — resumen cableado a `filtros`        │
│    🟢 LOW:    R-29 — el fallo del resumen tumba la lista │
│                                                          │
│  Mitigaciones incorporadas a la spec: 9                  │
│  Riesgos aceptados nuevos: 0                             │
│                                                          │
│  ─────────────────────────────────────────────────────   │
│  Riesgos: C:0 H:1 M:5 L:1                                │
│  Report: docs/daw/security/threat-FEAT-001c.md           │
└─────────────────────────────────────────────────────────┘
```
