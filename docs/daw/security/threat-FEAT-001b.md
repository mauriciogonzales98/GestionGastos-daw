# Threat Model — FEAT-001b: Filtros del listado, edición y eliminación de movimientos

| Campo | Valor |
|---|---|
| Ticket | FEAT-001b |
| Tier | FEATURE |
| Fecha | 2026-08-18 |
| PRD | `docs/daw/prd/prd-FEAT-001b.md` |
| Spec | `docs/daw/specs/spec-FEAT-001b.md` |
| Threat model previo | `docs/daw/security/threat-FEAT-001a.md` (R-01 a R-12, RA-01 y RA-02) |

Análisis sobre la arquitectura concreta de este sub-ticket, no sobre la aplicación entera: lo que
FEAT-001a ya analizó y mitigó no se repite salvo cuando este ticket lo modifica. La numeración de
riesgos continúa la del threat model de `a` — el primero nuevo es **R-13** — para que una referencia
a `R-04` signifique lo mismo en los dos documentos.

**Lo que cambia respecto de `a`, y por qué importa en seguridad:** hasta hoy la API solo sabía crear
y leer. Este ticket agrega las dos primeras operaciones que **modifican y destruyen** filas
existentes, y las agrega en una aplicación que —por RA-01, riesgo aceptado— todavía no tiene
autenticación. Todo el peso de la autorización recae sobre el filtro global de propietario y sobre
`IUsuarioActual`.

---

## 1. Componentes y superficies de ataque

| # | Componente | Superficie |
|---|---|---|
| C9 | `GET /api/movimientos?categoriaId=&desde=&hasta=` | **Nueva entrada de usuario en un endpoint de lectura.** Tres parámetros que llegan como texto y terminan en una consulta a la base |
| C10 | `PUT /api/movimientos/{id}` | **Nuevo.** Modifica una fila existente a partir de un identificador que elige el cliente |
| C11 | `DELETE /api/movimientos/{id}` | **Nuevo.** Destruye una fila de forma definitiva —el PRD descarta baja lógica, historial y papelera— |
| C12 | `FiltrosDeListado` | Parseo y validación de los tres parámetros de C9. Frontera entre texto hostil y tipos del dominio |
| C13 | `ValidadorMovimiento` (modificado) | Pasa a servir a dos endpoints. Si el PUT no lo atraviesa, se abre una puerta trasera a las validaciones del alta |
| C14 | `FiltrosMovimientos.tsx` + `ListadoMovimientos.tsx` (modificados) | Construyen la query string y disparan la eliminación desde el navegador |

Componentes heredados que este ticket **no** modifica y cuyo análisis sigue vigente en
`threat-FEAT-001a.md`: `IUsuarioActual` (C5), `AppDbContext` (C6), MySQL (C7) y la cadena de
conexión (C8).

## 2. Límites de confianza

| # | Cruce | Niveles |
|---|---|---|
| TB-1 | Navegador → API (C1 → C9, C10, C11) | No confiable → confiable. **Se ensancha en este ticket**: además del cuerpo del alta, ahora cruzan tres parámetros de query y un `{id}` de ruta que designa una fila a modificar o destruir |
| TB-2 | API → MySQL | Confiable → confiable con credencial. Sin cambios: se sigue sin `FromSqlRaw` |
| TB-5 | **Nuevo — Handler → filtro global de EF** (C10, C11 → `AppDbContext`) | El límite real de autorización de este ticket. Del lado de acá, un `{id}` arbitrario; del lado de allá, solo las filas del propietario. Que ese cruce se haga con una lectura filtrada y no con un `ExecuteDelete` a ciegas es lo que separa un 404 correcto de un borrado ajeno |

## 3. Clasificación de datos

| Dato | Clasificación | Novedad de este ticket |
|---|---|---|
| Monto, fecha, categoría de un movimiento | **Financiero** | Ahora es **modificable y destruible**, no solo creable |
| Nota de texto libre | **PII potencial** — el usuario puede escribir lo que quiera, incluido un nombre o un dato personal | Ahora es modificable; sigue guardándose tal cual y escapándose al renderizar (R-06) |
| `categoriaId`, `desde`, `hasta` | Público — parámetros de consulta, sin valor por sí mismos | Nuevo |
| `{id}` de movimiento | **Referencia a objeto** — su existencia o no es en sí misma información sobre datos ajenos | Nuevo como parámetro de escritura |

**Cifrado (F-TM-07):** sigue vigente **RA-02** de `threat-FEAT-001a.md` — sin TLS ni cifrado en
reposo, sobre HTTP en `127.0.0.1` con datos de desarrollo. Este ticket no introduce credenciales ni
PII nuevas que cambien esa aceptación: la nota ya existía y ya estaba cubierta.

## 4. Análisis STRIDE por componente

### C9 — `GET /api/movimientos` con filtros

| STRIDE | Análisis |
|---|---|
| **S** Spoofing | No introduce identidad nueva. El propietario lo sigue fijando `IUsuarioActual`, nunca un parámetro (R-15) |
| **T** Tampering | Los tres parámetros no escriben nada. Riesgo de manipulación del *criterio* de lectura, no del dato |
| **R** Repudiation | Lectura: nada que repudiar |
| **I** Information Disclosure | **R-13**: si algún filtro se resolviera en memoria tras traer todas las filas, o si el filtro global no se aplicara, un `categoriaId` ajeno expondría datos de otro propietario |
| **D** Denial of Service | **R-14**: un rango absurdo (`desde=1000-01-01&hasta=9999-12-31`) fuerza el recorrido más caro que la tabla admite. Acotado por `TechoDeItems = 500`, que este ticket conserva |
| **E** Elevation of Privilege | Sin roles: nada que escalar |

### C10 — `PUT /api/movimientos/{id}`

| STRIDE | Análisis |
|---|---|
| **S** Spoofing | **R-15**: un cuerpo que incluya `usuarioId` podría reasignar el movimiento a otro propietario si el DTO lo aceptara |
| **T** Tampering | **R-16**: si el PUT no atraviesa `ValidadorMovimiento`, entran por la puerta de atrás montos negativos, notas de 500 caracteres y fechas fuera del `DATE` de MySQL — exactamente lo que el PRD nombra como riesgo y AC-04 verifica |
| **R** Repudiation | **R-18** (compartido con C11): no hay auditoría; un movimiento modificado no deja rastro de su valor anterior |
| **I** Information Disclosure | **R-17**: responder 403 ante un movimiento ajeno y 404 ante uno inexistente confirma qué identificadores existen. El PRD lo fija en AC-06: 404 para ambos |
| **D** Denial of Service | Sin superficie propia más allá del techo general de la API |
| **E** Elevation of Privilege | **R-15**: escribir sobre una fila ajena es, sin roles, la única escalada posible en este sistema |

### C11 — `DELETE /api/movimientos/{id}`

| STRIDE | Análisis |
|---|---|
| **S** Spoofing | Igual que C10: el propietario no viaja en la petición |
| **T** Tampering | La destrucción es la forma extrema de manipulación. **R-19**: el PRD descarta baja lógica, así que no hay deshacer |
| **R** Repudiation | **R-18**: nadie puede demostrar después qué se borró ni cuándo |
| **I** Information Disclosure | **R-17**: mismo 404 indistinguible que el PUT |
| **D** Denial of Service | **R-19**: un borrado accidental o masivo destruye datos sin vuelta atrás. Mitigado en la interfaz con confirmación explícita, no en el servidor |
| **E** Elevation of Privilege | **R-15**: borrar una fila ajena |

### C12 — `FiltrosDeListado`

| STRIDE | Análisis |
|---|---|
| **S/E** | No participa de identidad ni privilegios |
| **T** Tampering | **R-20**: es el único punto donde texto no confiable se convierte en `DateOnly` e `int`. Un parseo laxo —o un `TryParse` dependiente de la cultura del proceso— hace que `01/02` signifique cosas distintas según el entorno |
| **R** Repudiation | N/A |
| **I** Information Disclosure | **R-21**: un mensaje de error que devuelva el detalle de la excepción del parseo filtraría internals. El proyecto ya responde `ProblemDetails` con `errors` por campo |
| **D** Denial of Service | Contribuye a R-14: es quien puede rechazar un rango absurdo antes de que llegue a la base |

### C13 — `ValidadorMovimiento` compartido por POST y PUT

| STRIDE | Análisis |
|---|---|
| **T** Tampering | **R-16**: si se duplica la validación en vez de compartirla, las dos copias divergen y la del PUT queda atrás. Es el riesgo que el PRD nombra primero |
| **I/S/R/D/E** | Sin superficie propia: es código de validación puro, sin estado ni acceso a datos |

### C14 — Frontend: filtros, edición y eliminación

| STRIDE | Análisis |
|---|---|
| **S** Spoofing | **R-22**: `PUT` y `DELETE` desde el navegador, sin autenticación y sin token anti-CSRF. Un sitio hostil abierto en la misma máquina podría dispararlos contra `127.0.0.1` |
| **T** Tampering | La confirmación de borrado es una barrera de usabilidad, no de seguridad: no protege contra una petición fabricada |
| **R** Repudiation | Hereda R-18 |
| **I** Information Disclosure | **R-23**: la nota, ahora también editable, sigue sin poder renderizarse con `dangerouslySetInnerHTML` (R-06 de `a`, que este ticket extiende al formulario de edición) |
| **D** Denial of Service | N/A en el cliente |
| **E** Elevation of Privilege | N/A |

## 5. Riesgos y mitigaciones

| # | Riesgo | STRIDE | Probab. | Impacto | Mitigación |
|---|---|---|---|---|---|
| **R-13** | Filtro resuelto en memoria o sin filtro global: fuga de movimientos ajenos o de fuera del rango | I | Baja | **Alto** | Los tres filtros se aplican a la consulta EF **antes** del `CountAsync` y del `Take`, nunca sobre la lista materializada. NFR-02 y AC-14 lo exigen; el test asierta sobre el SQL emitido vía `Tests/Infra/ObservadorDeSql.cs`, porque un test solo conductual pasa igual con el filtro hecho en memoria |
| **R-14** | Rango de fechas absurdo como vector de degradación | D | Media | Bajo | Se conserva `TechoDeItems = 500` con su marca `recortado`, y el `total` pasa a contarse **sobre el universo filtrado**: si contara el sin filtrar, `recortado` mentiría. El índice `ix_movimientos_usuario_fecha_id` cubre el prefijo `(usuario_id, fecha)` |
| **R-15** | Overposting o IDOR de escritura: modificar o borrar una fila ajena, o reasignar el propietario | S, E, T | Media | **Crítico** | `ModificarMovimientoRequest` **no lleva** `id`, `usuarioId`, `tipo`, `moneda` ni `creadoEn` (extiende R-04 al PUT). La fila se localiza con una lectura por `DbSet<Movimiento>` —donde el filtro global ya aplica— y recién entonces se modifica o borra; nunca con un `ExecuteUpdate`/`ExecuteDelete` que no haya cruzado ese filtro. Test de overposting sobre el PUT y test de acceso cruzado sobre PUT y DELETE |
| **R-16** | El PUT como puerta trasera a las validaciones del alta | T | **Alta** | **Alto** | Una única implementación de validación compartida por los dos endpoints, no dos copias. AC-04 verifica las cuatro condiciones de rechazo sobre el endpoint de modificación, no sobre el de alta |
| **R-17** | 403 vs 404 revelando qué identificadores existen | I | Media | Bajo | 404 con el mismo `TituloNoEncontrado` para "no existe" y "es de otro", igual que `ObtenerPorIdAsync` hoy. AC-06 lo fija |
| **R-18** | Sin auditoría: una modificación o un borrado no dejan rastro | R | Alta | Bajo | **Riesgo aceptado RA-03** (ver §6) |
| **R-19** | Eliminación definitiva sin deshacer | T, D | Media | Medio | Confirmación explícita en la interfaz antes de ejecutar, tal como pide la mitigación del PRD. Se documenta como límite conocido: sin baja lógica, la red de seguridad real es el respaldo de la base |
| **R-20** | Parseo laxo o dependiente de cultura en los filtros de fecha | T | Media | Medio | `DateOnly.TryParseExact` con `FormatoDeFecha` e `InvariantCulture`, la misma constante y la misma cultura que ya usa `ValidadorMovimiento`. Rango válido acotado a `FechaMinima`–`FechaMaxima`, el rango que el `DATE` de MySQL admite |
| **R-21** | Mensaje de error del parseo filtrando internals | I | Baja | Bajo | `ProblemDetails` con `errors` por campo y mensajes redactados a mano, nunca `exception.Message`. El manejador global de `a` ya convierte cualquier excepción en un 500 con `traceId` y sin detalle |
| **R-22** | `PUT`/`DELETE` sin autenticación ni token anti-CSRF | S | Baja | **Alto** | **Riesgo aceptado RA-04** (ver §6) |
| **R-23** | Nota editable renderizada como HTML | I | Baja | Medio | Se extiende R-06 al formulario de edición: prohibido `dangerouslySetInnerHTML` en todo el frontend; React escapa por defecto. La nota se guarda tal cual y se escapa al renderizar |

## 6. Riesgos aceptados (F-TM-04)

Los dos riesgos aceptados de FEAT-001a —**RA-01** (sin autenticación) y **RA-02** (sin cifrado)—
siguen vigentes sin cambios y con las mismas condiciones de revisión. Este ticket agrega dos.

### RA-03 — Sin auditoría de modificaciones ni de borrados (R-18)

| Campo | Valor |
|---|---|
| Quién lo acepta | **mauricio gonzales** (propietario del proyecto), el 2026-08-18, de forma explícita durante la fase PLAN de FEAT-001b. Coherente con su PRD, que ya declara fuera de alcance la "baja lógica de movimientos, historial de cambios o papelera" |
| Justificación | La aplicación tiene un solo usuario, que es también su propietario y su único operador: no hay nadie de quien registrar qué hizo, ni nadie ante quien repudiarlo. Una tabla de auditoría hoy protegería al usuario únicamente de sí mismo, y el PRD ya elige la confirmación explícita como esa protección. El costo de agregarla después es bajo: los movimientos ya llevan `creado_en` y propietario |
| Condiciones de revisión | Se revisa (a) al incorporar autenticación con más de un usuario —a partir de ahí sí hay identidades que distinguir y acciones que atribuir—, y (b) si alguna vez se despliega fuera de `127.0.0.1`. Lo que ocurra primero |

### RA-04 — `PUT` y `DELETE` sin protección anti-CSRF (R-22)

| Campo | Valor |
|---|---|
| Quién lo acepta | **mauricio gonzales** (propietario del proyecto), el 2026-08-18, de forma explícita durante la fase PLAN de FEAT-001b |
| Justificación | Un token anti-CSRF protege una sesión autenticada, y aquí no hay sesión: por RA-01 el servidor no distingue quién llama, así que un token no agregaría una garantía que el sistema no tiene. La exposición real está acotada por el binding en `127.0.0.1` (R-02): el atacante necesita que la víctima abra un sitio hostil **en la misma máquina** que corre la API, y el peor desenlace es la pérdida de datos de desarrollo propios, no su exfiltración —la respuesta de un `fetch` de origen cruzado no la puede leer— |
| Condiciones de revisión | Se revisa **en el mismo ticket que incorpore autenticación**, y no después: en el momento en que exista una cookie de sesión, la ausencia de anti-CSRF pasa de ser irrelevante a ser explotable. También ante cualquier despliegue fuera de `127.0.0.1` |

## 7. Mitigaciones a incorporar a la spec

1. Los tres filtros se aplican a la consulta EF antes del `CountAsync` y del `Take`; `total` y `recortado` describen el universo **filtrado** (R-13, R-14).
2. Test que asierta sobre el SQL emitido —vía `Tests/Infra/ObservadorDeSql.cs`— para probar que el filtrado ocurre en la base y no en memoria (R-13).
3. `ModificarMovimientoRequest` sin `id`, `usuarioId`, `tipo`, `moneda` ni `creadoEn`, con test de overposting sobre el PUT (R-15).
4. PUT y DELETE localizan la fila con una lectura sobre `DbSet<Movimiento>`, para que el filtro global de propietario aplique; nunca con `ExecuteUpdate`/`ExecuteDelete` sin esa lectura previa (R-15).
5. Una sola implementación de validación compartida por POST y PUT, no dos copias (R-16).
6. 404 con el mismo `TituloNoEncontrado` para "no existe" y "es de otro", en PUT y en DELETE, con test de acceso cruzado en ambos (R-17).
7. Confirmación explícita en la interfaz antes de eliminar (R-19).
8. Parseo de fechas con `TryParseExact`, `FormatoDeFecha` e `InvariantCulture`, acotado a `FechaMinima`–`FechaMaxima` (R-20).
9. Mensajes de validación redactados, nunca `exception.Message` (R-21).
10. Prohibición de `dangerouslySetInnerHTML` extendida al formulario de edición (R-23).

## 8. Resumen

| Categoría | Cantidad |
|---|---|
| Componentes nuevos o modificados analizados | 6 (C9 a C14) |
| Límites de confianza | 3 relevantes (TB-1 ensanchado, TB-2 sin cambios, **TB-5 nuevo**) |
| Riesgos identificados | 11 (R-13 a R-23) |
| 🔴 Críticos | 1 (R-15) — mitigado |
| 🟠 Altos | 3 (R-13, R-16, R-22) — dos mitigados, uno aceptado (RA-04) |
| 🟡 Medios | 3 (R-19, R-20, R-23) — mitigados |
| 🟢 Bajos | 4 (R-14, R-17, R-18, R-21) — tres mitigados, uno aceptado (RA-03) |
| Riesgos aceptados nuevos | 2 (RA-03, RA-04) |

**Dependencias nuevas (W-TM-01):** ninguna. Este ticket no agrega paquetes de NuGet ni de npm —
los filtros, el PUT y el DELETE se construyen con lo que el proyecto ya tiene—, de modo que no
introduce superficie de cadena de suministro.
