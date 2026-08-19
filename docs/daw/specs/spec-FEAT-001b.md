# Spec FEAT-001b: Filtros del listado, edición y eliminación de movimientos

| Field | Value |
|-------|-------|
| Ticket | FEAT-001b |
| PRD | docs/daw/prd/prd-FEAT-001b.md |
| Tier | FEATURE |
| Date | 2026-08-18 |
| Spec loops | 0 |

## Summary

Se agregan tres parámetros de query a `GET /api/movimientos` (`categoriaId`, `desde`, `hasta`),
resueltos íntegramente en la consulta EF, y dos endpoints nuevos sobre `/api/movimientos/{id}`: `PUT`
para modificar y `DELETE` para eliminar. La validación del alta se comparte con la modificación a
través de una interfaz común, para que el PUT no sea una puerta trasera a las reglas del POST. En el
frontend se agregan los controles de filtro —con el mes calendario en curso como valor por defecto,
aplicado por el cliente— y las acciones de editar y eliminar sobre cada fila del listado.

**Decisión de diseño registrada:** el valor por defecto "mes actual" lo aplica **el frontend**, no el
backend. `GET /api/movimientos` sin parámetros sigue devolviendo todo lo del propietario, como en
FEAT-001a. El motivo está en la evidencia del impact scan: mover el default al servidor rompería
siete tests de `ListarMovimientosTests.cs` que siembran fechas fijas de 2025 y marzo de 2026, y
volvería mentiroso el contrato del endpoint para cualquier consumidor que no sea la SPA. AC-09 habla
de "cuando el usuario **abre el listado**", que es un evento de interfaz; y la mitigación del PRD
—"el rango vigente se muestra siempre visible junto al listado"— confirma que el default es una
decisión de presentación. AC-14 y NFR-02 se cumplen igual, porque el cliente manda siempre el rango
explícito.

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 2, Block 6 |
| FR-02 | Block 3, Block 6 |
| FR-03 | Block 2, Block 3 |
| FR-04 | Block 1, Block 4, Block 5 |
| FR-05 | Block 1, Block 4, Block 5 |
| FR-06 | Block 1, Block 5 |
| NFR-01 | Estrategia: el filtro por rango se apoya en el índice `ix_movimientos_usuario_fecha_id` sobre `(usuario_id, fecha DESC, id DESC)`, que FEAT-001a ya creó y cuyo prefijo `(usuario_id, fecha)` sirve al `BETWEEN`. Se mide en Block 1 con el mismo método que `RendimientoAltaTests.cs` usa hoy: 100 ejecuciones sobre 1000 movimientos, percentil 95 por debajo de 1 s |
| NFR-02 | Estrategia: los tres filtros se incorporan a `IQueryable` antes del `CountAsync` y del `Take`, de modo que nunca se materialice una fila fuera del rango. Verificado en dos capas en Block 1: conductual (la respuesta no trae los excluidos, AC-14) y estructural (el SQL emitido lleva el `WHERE`, vía `Tests/Infra/ObservadorDeSql.cs`) |

**Sobre el índice y NFR-02.** No se agrega migración. El índice existente cubre el prefijo
`(usuario_id, fecha)`, que es lo que NFR-02 pide literalmente —"un índice sobre propietario y
fecha"—. El filtro por categoría queda como predicado residual sobre las filas ya acotadas por
usuario y rango: a la escala que fija NFR-01 (1000 movimientos, y como mucho 500 por el techo de la
respuesta) el costo es despreciable, y un índice `(usuario_id, categoria_id, fecha)` adicional
encarecería toda escritura para ganar nada medible. Si AC-13 fallara en Block 1, esa medición es la
que justificaría agregarlo, y no una suposición previa.

## Dependencies between blocks

Orden de ejecución: **1 → 2 → 3 → 4 → 5 → 6**.

| Block | Depende de | Motivo |
|---|---|---|
| 1 — Backend: filtros | — | Arranca sobre lo que FEAT-001a dejó en `main` |
| 2 — Backend: `PUT` | — | Independiente de 1; se ordena después por conveniencia de revisión, no por acoplamiento |
| 3 — Backend: `DELETE` | 2 | Reutiliza la lectura filtrada y el 404 que introduce el Block 2 |
| 4 — Frontend: cliente y tipos | 1, 2, 3 | Construye la query string y las llamadas contra contratos que deben existir |
| 5 — Frontend: controles de filtro | 4 | Consume `obtenerMovimientos` con su firma nueva |
| 6 — Frontend: editar y eliminar | 4, 5 | Las acciones viven en las filas del listado ya montado con filtros |

---

## Block 1 — Backend: filtros del listado

**Files**

- `backend/GestionGastos.Api/Movimientos/FiltrosDeListado.cs` (new) — parseo y validación de los tres parámetros; devuelve un tipo del dominio ya convertido.
- `backend/GestionGastos.Api/Movimientos/MovimientosEndpoints.cs` (modified) — `ListarAsync` acepta los tres parámetros y los incorpora a la consulta.
- `backend/GestionGastos.Api.Tests/Movimientos/FiltrarMovimientosTests.cs` (new)
- `backend/GestionGastos.Api.Tests/Movimientos/ListarMovimientosTests.cs` (modified) — se reescribe `Listar_ConParametrosDesconocidos_LosIgnora`, cuyo contrato cambia.
- `backend/GestionGastos.Api.Tests/Movimientos/RendimientoListadoTests.cs` (new) — la medición de AC-13.

**Logic**

`FiltrosDeListado.Parsear(int? categoriaId, string? desde, string? hasta, out FiltrosValidados? filtros)`
devuelve un `ResultadoValidacion`, siguiendo el mismo patrón que `ValidadorMovimiento.Validar`. Las
fechas se parsean con `DateOnly.TryParseExact`, `ValidadorMovimiento.FormatoDeFecha` e
`InvariantCulture`, y se acotan a `FechaMinima`–`FechaMaxima` (mitigación R-20). Un `categoriaId`
menor o igual a cero se rechaza. Si `desde > hasta` se rechaza con la clave `desde` (FR-06).

`ListarAsync` incorpora los filtros a `IQueryable` **antes** del `CountAsync` y del `Take`, de modo
que `total` y `recortado` describan el universo filtrado y no el completo (mitigaciones R-13 y
R-14). Ausencia de un parámetro significa "sin ese filtro": sin parámetros, el endpoint sigue
devolviendo todo lo del propietario, exactamente como en FEAT-001a.

**Cambio de firma que la auditoría de arquitectura exige declarar:** `ListarAsync` pasa de
`Task<Ok<ListadoMovimientosResponse>>` a
`Task<Results<Ok<ListadoMovimientosResponse>, ValidationProblem>>`, porque el contrato gana un
segundo desenlace propio. El comentario de `MovimientosEndpoints.cs:108-112`, que hoy justifica lo
contrario —"el contrato tiene un solo desenlace propio"—, se reescribe; y el de la línea 104-106
("no recibe parámetros: los filtros llegan en FEAT-001b") y el de la 120-121 ("el total se cuenta
sobre TODO lo del propietario") quedan obsoletos y se corrigen.

**API contract**

- Método + ruta: `GET /api/movimientos?categoriaId={int}&desde={yyyy-MM-dd}&hasta={yyyy-MM-dd}`
- Request: los tres parámetros son **opcionales e independientes**. Ausente = sin ese filtro.
- Response 200: sin cambios de forma — `{ items: MovimientoDto[], recortado: bool, total: int }`, con `total` y `recortado` referidos ya al universo filtrado.
- Error codes: `400` `ValidationProblem` con `errors` por campo (`categoriaId`, `desde`, `hasta`).
- Auth: ninguna (RA-01). El propietario lo impone el filtro global de `AppDbContext`, no un parámetro.

**Input validation**

- `categoriaId`: entero mayor a cero. Cero o negativo → `errors.categoriaId`. Una categoría inexistente **no es un error**: devuelve un listado vacío, porque filtrar por algo que no existe no es una petición malformada.
- `desde`, `hasta`: formato `yyyy-MM-dd` exacto, cultura invariante, dentro de `FechaMinima`–`FechaMaxima`.
- `desde > hasta` → `errors.desde` (FR-06, AC-12).
- Parámetros desconocidos en la query string se siguen ignorando.

**Error handling**

- Formato de fecha inválido → 400 con el campo y el formato esperado; nunca `exception.Message` (mitigación R-21).
- Rango invertido → 400, y el listado no se ejecuta.
- Fallo de base → se propaga al manejador global, que responde 500 con `traceId`, como hoy.

**Required tests**

- [ ] `Filtrar_PorCategoria_DevuelveSoloEsaCategoria` — valida AC-07
- [ ] `Filtrar_SinCategoria_DevuelveTodasLasCategorias` — valida AC-08
- [ ] `Filtrar_PorRango_IncluyeAmbosExtremos` — valida AC-10; siembra movimientos con fecha igual a `desde` y a `hasta`, que es el error clásico del rango cerrado
- [ ] `Filtrar_PorRango_ExcluyeLoDeAfuera` — valida AC-10 y AC-14
- [ ] `Filtrar_PorCategoriaYRango_AplicaLasDosCondiciones` — valida AC-11
- [ ] `Filtrar_ConDesdePosteriorAHasta_Devuelve400` — sad path, valida AC-12 y FR-06
- [ ] `Filtrar_ConFechaMalFormada_Devuelve400ConElCampo` — sad path
- [ ] `Filtrar_ConCategoriaInexistente_DevuelveListadoVacio` — sad path: no es un 400
- [ ] `Filtrar_ElTotalYElRecorte_SeCuentanSobreLoFiltrado` — evita que `recortado` mienta con filtros angostos
- [ ] `Filtrar_EmiteElWhereEnSql` — **capa estructural** con `ObservadorDeSql`: el SQL emitido lleva el `WHERE` de fecha y categoría. Sin esta capa, un filtrado hecho en memoria daría verde igual (mitigación R-13)
- [ ] `Listar_ConParametrosDesconocidos_LosIgnora` (reescrito) — `?foo=bar` se sigue ignorando, pero `?desde=basura` ahora devuelve 400: el contrato viejo dejó de valer
- [ ] `Listado_ConMilMovimientos_RespondeBajoUnSegundo` — valida AC-13, percentil 95 sobre 100 ejecuciones
- [ ] `Listar_ConFalloDeBase_DevuelveProblemDetailsCon500` — sad path del último error documentado. `ManejoDeErroresTests.cs` de FEAT-001a ya cubre el manejador global; acá se comprueba que la rama nueva de filtros no lo esquiva

**Completion criterion**

Los 13 tests listados pasan; `GET /api/movimientos` con los tres filtros combinados devuelve solo
las filas que cumplen las tres condiciones; el SQL emitido contiene el `WHERE`; y `total` refleja el
universo filtrado.

---

## Block 2 — Backend: modificación (`PUT /api/movimientos/{id}`)

**Files**

- `backend/GestionGastos.Api/Movimientos/IEntradaDeMovimiento.cs` (new) — la interfaz común de validación.
- `backend/GestionGastos.Api/Movimientos/ModificarMovimientoRequest.cs` (new)
- `backend/GestionGastos.Api/Movimientos/CrearMovimientoRequest.cs` (modified) — pasa a implementar la interfaz.
- `backend/GestionGastos.Api/Movimientos/ValidadorMovimiento.cs` (modified) — `Validar` recibe la interfaz; `TipoEsperado` sale de la validación común.
- `backend/GestionGastos.Api/Movimientos/MovimientosEndpoints.cs` (modified) — agrega `PUT`.
- `backend/GestionGastos.Api.Tests/Movimientos/ModificarMovimientoTests.cs` (new)

**Logic**

`IEntradaDeMovimiento` declara los cuatro campos que POST y PUT validan igual: `CategoriaId`,
`Monto`, `Fecha`, `Nota`. `ValidadorMovimiento.Validar(IEntradaDeMovimiento, out DatosDeMovimiento?)`
pasa a recibir la interfaz, y **`TipoEsperado` deja de formar parte de esa validación común**: se
valida aparte, con `ValidadorMovimiento.ValidarTipoEsperado`, invocado solo por `CrearAsync`.

Esto resuelve la ambigüedad que señaló la auditoría de arquitectura. En el POST, `tipoEsperado` es
una entrada **no confiable** del cliente que solo sirve para detectar el cruce de AC-10. En el PUT no
hace falta ninguna entrada del cliente: el tipo del movimiento ya está persistido y es confiable, así
que el handler compara la categoría nueva contra `movimiento.Tipo` leído de la base. Meter los dos
casos bajo el mismo miembro de interfaz mezclaría un dato confiable con uno que no lo es.

`ModificarMovimientoRequest` **repite el atributo**
`[property: JsonConverter(typeof(MontoJsonConverter))]` sobre `Monto`: los atributos no se heredan de
una interfaz en C#, y sin él un `"monto": "abc"` dejaría de producir un 400 con `errors.monto`.

El handler: valida → lee el movimiento con `SingleOrDefaultAsync` sobre `DbSet<Movimiento>`, donde el
filtro global de propietario ya aplica (mitigación R-15) → 404 si no aparece → lee la categoría
nueva → si su tipo difiere del tipo persistido del movimiento, 400 → asigna los cuatro campos y
`SaveChangesAsync`. **Nunca `ExecuteUpdateAsync` sin esa lectura previa**, y nunca
`IgnoreQueryFilters` (ADR-003).

**API contract**

- Método + ruta: `PUT /api/movimientos/{id:int}`
- Request: `{ categoriaId: int, monto: decimal, fecha: string "yyyy-MM-dd", nota: string | null }`. **No lleva** `id`, `usuarioId`, `tipo`, `moneda`, `creadoEn` ni `tipoEsperado` (mitigación R-15, que extiende R-04 al PUT).
- Response 200: `MovimientoDto`, la misma forma que devuelven el alta y el listado.
- Error codes: `400` `ValidationProblem`; `404` `ProblemDetails` con `TituloNoEncontrado`.
- Auth: ninguna (RA-01). La autorización es el filtro global.

**Input validation**

Idéntica a la del alta, por construcción: misma implementación, no una copia (mitigación R-16).
Monto mayor a cero con hasta 2 decimales y tope `MontoMaximo`; categoría obligatoria; fecha
`yyyy-MM-dd` dentro del rango del `DATE` de MySQL; nota de hasta 120 caracteres, en blanco se
normaliza a `null`. Además: la categoría nueva debe existir y ser del **mismo tipo** que el
movimiento — cambiar un gasto en ingreso está fuera de alcance por PRD.

**Error handling**

- Movimiento inexistente o de otro propietario → **404 con el mismo `TituloNoEncontrado`** en los dos casos, indistinguibles (mitigación R-17, AC-06).
- Categoría inexistente → 400 `errors.categoriaId`.
- Categoría de otro tipo → 400 `errors.categoriaId` nombrando los dos tipos.
- Cualquier rechazo deja el movimiento **con todos sus valores anteriores** (AC-04): la validación ocurre antes de tocar la entidad.

**Required tests**

- [ ] `Modificar_ElMonto_LoRefleja` — valida AC-01
- [ ] `Modificar_CategoriaYFecha_PersisteAmbas` — valida AC-02
- [ ] `Modificar_LaNota_LaRefleja` — valida AC-03
- [ ] `Modificar_BorrandoLaNota_GuardaNull` — valida AC-03, rama de borrado
- [ ] `Modificar_ConMontoInvalido_Devuelve400YNoAltera` — sad path, valida AC-04
- [ ] `Modificar_SinCategoria_Devuelve400YNoAltera` — sad path, valida AC-04
- [ ] `Modificar_ConCategoriaDeOtroTipo_Devuelve400YNoAltera` — sad path, valida AC-04
- [ ] `Modificar_ConCategoriaInexistente_Devuelve400YNoAltera` — sad path del error documentado "categoría inexistente"
- [ ] `Modificar_ConNotaDemasiadoLarga_Devuelve400YNoAltera` — sad path, valida AC-04
- [ ] `Modificar_Inexistente_Devuelve404` — valida AC-06
- [ ] `Modificar_DeOtroPropietario_Devuelve404` — valida AC-06 y la mitigación R-15
- [ ] `Modificar_ConCuerpoQueTraeUsuarioId_LoIgnora` — test de overposting (mitigación R-15)
- [ ] `Modificar_ConMontoNoNumerico_Devuelve400ConElCampo` — prueba que el `JsonConverter` se repitió (WARN de la auditoría)

**Completion criterion**

Los 13 tests listados pasan; `PUT` con cuerpo válido devuelve 200 con el movimiento actualizado; los
casos de rechazo listados devuelven 400 o 404 y dejan la fila intacta; y `ValidadorMovimiento` tiene
**una sola** implementación de cada regla, compartida por POST y PUT.

---

## Block 3 — Backend: eliminación (`DELETE /api/movimientos/{id}`)

**Files**

- `backend/GestionGastos.Api/Movimientos/MovimientosEndpoints.cs` (modified) — agrega `DELETE`.
- `backend/GestionGastos.Api.Tests/Movimientos/EliminarMovimientoTests.cs` (new)

**Logic**

Lectura con `SingleOrDefaultAsync` sobre `DbSet<Movimiento>` —el filtro global aplica—, 404 si no
aparece, `Remove` y `SaveChangesAsync`. La eliminación es **definitiva**: el PRD descarta baja
lógica, historial y papelera (riesgo R-19, con la confirmación de la interfaz como única red).

**API contract**

- Método + ruta: `DELETE /api/movimientos/{id:int}`
- Request: sin cuerpo.
- Response: `204 No Content`.
- Error codes: `404` `ProblemDetails` con `TituloNoEncontrado`.
- Auth: ninguna (RA-01). La autorización es el filtro global.

**Input validation**

- `{id}`: entero, impuesto por la restricción de ruta `{id:int}`. Un id no numérico no llega al handler: la ruta no matchea y ASP.NET responde 404 antes, que es el mismo desenlace que un id inexistente y no filtra información adicional (mitigación R-17).
- No hay cuerpo: un `DELETE` con cuerpo se ignora.

**Error handling**

- Inexistente o de otro propietario → 404 indistinguible (mitigación R-17, AC-06).
- Un segundo `DELETE` sobre el mismo id → 404, no 500.

**Required tests**

- [ ] `Eliminar_UnMovimientoPropio_Devuelve204` — valida AC-05
- [ ] `Eliminar_UnMovimientoPropio_DejaDeAparecerEnElListado` — valida AC-05, la mitad que importa
- [ ] `Eliminar_DosVeces_DevuelveNotFoundLaSegunda` — sad path
- [ ] `Eliminar_Inexistente_Devuelve404` — valida AC-06
- [ ] `Eliminar_DeOtroPropietario_Devuelve404YNoLoBorra` — valida AC-06 y la mitigación R-15; comprueba además que la fila ajena **sigue existiendo**

**Completion criterion**

Los 5 tests listados pasan; el movimiento eliminado desaparece del listado y de `GET /{id}`; y una
fila de otro propietario sobrevive intacta al intento.

---

## Block 4 — Frontend: cliente HTTP y tipos

**Files**

- `frontend/src/api/tipos.ts` (modified) — `FiltrosDeMovimientos` y `ModificarMovimientoRequest`.
- `frontend/src/api/cliente.ts` (modified) — `obtenerMovimientos` recibe filtros; nuevas `modificarMovimiento` y `eliminarMovimiento`; nuevo error tipado `ErrorNoEncontrado`.
- `frontend/src/api/cliente.test.ts` (modified) — cobertura de la firma nueva y de los dos verbos nuevos.

**Logic**

`obtenerMovimientos(filtros?: FiltrosDeMovimientos)` construye la query string
con `URLSearchParams`, **omitiendo** las claves ausentes: un filtro sin valor no viaja como cadena
vacía. `modificarMovimiento(id, entrada)` hace el `PUT`; `eliminarMovimiento(id)` hace el `DELETE` y
no espera cuerpo — un 204 no se puede pasar por `response.json()`.

**Nuevo tipo de error, exigido por la auditoría de arquitectura:** `ErrorNoEncontrado` para el 404.
Hoy cualquier estado distinto de 400 cae en `ErrorDelServidor`, documentado como "fallo del lado del
servidor". Un 404 de PUT o DELETE no es un fallo del servidor: es un desenlace de dominio normal —el
movimiento ya no está— y la vista necesita distinguirlo para decir "refrescá la lista" en vez de
"algo se rompió". Sin este tipo, distinguirlo obligaría a mirar el `title` a mano, que es un
catch-por-mensaje encubierto de los que `AGENTS.md` pide evitar.

**Error handling**

- Red caída → `ErrorDeRed`, como hoy.
- 400 con `errors` → `ErrorDeValidacion`, como hoy.
- **404 → `ErrorNoEncontrado`** (nuevo).
- Cualquier otro estado → `ErrorDelServidor` con `traceId`.

**Required tests**

- [ ] `obtenerMovimientos_ConFiltros_ArmaLaQueryString` — comprueba la URL pedida, no solo el resultado
- [ ] `obtenerMovimientos_SinFiltros_NoMandaParametrosVacios` — sad path: nada de `?desde=&hasta=`
- [ ] `modificarMovimiento_MandaPutConElCuerpo`
- [ ] `eliminarMovimiento_MandaDeleteYNoLeeCuerpo` — un 204 sin cuerpo no debe romper el cliente
- [ ] `cliente_Ante404_LanzaErrorNoEncontrado` — sad path, distingue el 404 del 500
- [ ] `cliente_Ante500_SigueLanzandoErrorDelServidor` — evita que el tipo nuevo se coma los fallos reales
- [ ] `cliente_AnteRedCaida_LanzaErrorDeRed` — sad path del error documentado, sobre los dos verbos nuevos
- [ ] `cliente_Ante400ConErrors_LanzaErrorDeValidacion` — sad path del error documentado, sobre el `PUT`

**Completion criterion**

Los 8 tests listados pasan; ningún llamador de `obtenerMovimientos` quedó sin actualizar; y un 404
llega a la vista como `ErrorNoEncontrado` y no como `ErrorDelServidor`.

---

## Block 5 — Frontend: controles de filtro

**Files**

- `frontend/src/movimientos/FiltrosMovimientos.tsx` (new) — selector de categoría y los dos campos de fecha.
- `frontend/src/movimientos/FiltrosMovimientos.test.tsx` (new)
- `frontend/src/movimientos/mesActual.ts` (new) — cálculo del primer y último día del mes en curso.
- `frontend/src/movimientos/mesActual.test.ts` (new)
- `frontend/src/movimientos/ListadoMovimientos.tsx` (modified) — recibe los filtros y los pasa al cliente.
- `frontend/src/movimientos/ListadoMovimientos.test.tsx` (modified) — sus tests hoy no inspeccionan la URL pedida; pasan a hacerlo.
- `frontend/src/App.tsx` (modified) — sostiene el estado de los filtros y lo baja a los dos componentes.
- `frontend/src/App.test.tsx` (modified) — su `fetch` simulado hoy ignora la query string; pasa a contemplarla.

**Logic**

`mesActual()` devuelve `{ desde, hasta }` con el primer y el último día del mes calendario en curso,
en formato `yyyy-MM-dd` y **en hora local del navegador** — que es la que responde la pregunta "cómo
vengo este mes" para quien la hace. `App` inicializa el estado de filtros con ese valor, de modo que
la primera carga ya pide el mes actual (AC-09) sin que el backend tenga que suponer nada.

El rango vigente se muestra **siempre visible** junto al listado, no como estado implícito: es la
mitigación que el PRD pide para que nadie crea que perdió los movimientos de meses anteriores.

La validación `desde > hasta` se hace también en el cliente para dar el mensaje sin ida y vuelta
(AC-12), y el listado **mantiene el rango anterior** hasta que el nuevo sea válido. El servidor la
repite igual: la validación del cliente es comodidad, no control.

**Input validation**

- Los dos campos de fecha usan `<input type="date">`, que ya emite `yyyy-MM-dd`.
- `desde > hasta` → mensaje junto al control, sin disparar la petición.
- Categoría vacía = "todas" (AC-08).

**Error handling**

- `ErrorDeValidacion` del servidor → mensaje por campo, junto al control correspondiente.
- `ErrorDeRed` → mensaje con opción de reintentar, como en el listado de FEAT-001a.

**Required tests**

- [ ] `Filtros_AlAbrir_PideElMesActual` — valida AC-09; ancla la fecha del sistema con temporizadores falsos en vez de depender de qué día se corra la suite
- [ ] `mesActual_DevuelveElPrimeroYElUltimoDiaDelMes` — incluye un mes de 31 días, uno de 30 y febrero
- [ ] `Filtros_AlElegirCategoria_LaPasaAlCliente` — valida AC-07
- [ ] `Filtros_AlAplicarUnRango_LoPasaAlCliente` — valida AC-10
- [ ] `Filtros_AlAplicarCategoriaYRango_PasaLosDos` — valida AC-11
- [ ] `Filtros_ConDesdePosteriorAHasta_MuestraElMotivoYNoPide` — sad path, valida AC-12
- [ ] `Filtros_ConRangoInvalido_MantieneElListadoAnterior` — valida AC-12, la mitad que se suele olvidar
- [ ] `Listado_MuestraElRangoVigente` — la mitigación del PRD contra creer que se perdieron datos
- [ ] `Filtros_ConRedCaida_MuestraElErrorConReintento` — sad path del `ErrorDeRed` documentado
- [ ] `Filtros_ConRechazoDelServidor_MuestraElMensajePorCampo` — sad path del `ErrorDeValidacion` documentado: el servidor repite la validación aunque el cliente ya la haya hecho

**Completion criterion**

Los 10 tests listados pasan; al abrir la aplicación el listado pide el mes en curso; y los tests de
`ListadoMovimientos` y de `App` asertan sobre la URL pedida, no solo sobre el orden de las llamadas.

---

## Block 6 — Frontend: editar y eliminar

**Files**

- `frontend/src/movimientos/FormularioMovimiento.tsx` (modified) — modo edición: recibe un movimiento opcional y cambia el verbo que dispara.
- `frontend/src/movimientos/FormularioMovimiento.test.tsx` (modified)
- `frontend/src/movimientos/ConfirmarEliminacion.tsx` (new) — el diálogo de confirmación.
- `frontend/src/movimientos/ConfirmarEliminacion.test.tsx` (new)
- `frontend/src/movimientos/ListadoMovimientos.tsx` (modified) — botones de editar y eliminar por fila.
- `frontend/src/movimientos/ListadoMovimientos.test.tsx` (modified)
- `frontend/src/App.tsx` (modified) — orquesta edición y eliminación, y refresca el listado.
- `frontend/src/App.test.tsx` (modified) — las dos costuras nuevas, ejercidas de verdad y no con `rerender()`.

**Logic**

Editar abre el formulario con los valores del movimiento; guardar llama a `modificarMovimiento` y
refresca el listado. Eliminar abre `ConfirmarEliminacion` y solo tras la confirmación explícita llama
a `eliminarMovimiento` (mitigación R-19); cancelar no dispara nada.

**La nota se sigue renderizando como texto.** Queda prohibido `dangerouslySetInnerHTML` también en el
formulario de edición: la nota se guarda tal cual el usuario la escribió y se escapa al renderizar
(mitigación R-23, que extiende R-06 de FEAT-001a).

**Input validation**

- El formulario en modo edición aplica **las mismas reglas que en modo alta**, porque es el mismo componente: monto mayor a cero con hasta 2 decimales, categoría obligatoria, fecha `yyyy-MM-dd`, nota de hasta 120 caracteres. No hay una segunda implementación que pueda divergir.
- El selector de categoría se acota a las del **mismo tipo** que el movimiento: cambiar un gasto en ingreso está fuera de alcance por PRD, y ofrecerlo en la interfaz para que el servidor lo rechace después sería un callejón.
- La confirmación de eliminación no acepta entrada: solo confirmar o cancelar.

**Error handling**

- `ErrorDeValidacion` al guardar → mensajes por campo; el movimiento **no cambia** en el listado (AC-04).
- `ErrorNoEncontrado` al guardar o eliminar → mensaje de que el movimiento ya no existe y refresco del listado, que es exactamente el caso que el tipo de error nuevo del Block 4 hace distinguible.
- `ErrorDeRed` → mensaje con reintento.

**Required tests**

- [ ] `Editar_GuardaYActualizaLaFila` — valida AC-01 y AC-03 **ejerciendo la costura real**, montando `App`, sin `rerender()`
- [ ] `Editar_ConDatosInvalidos_MuestraElErrorYNoCambiaLaFila` — sad path, valida AC-04
- [ ] `Editar_CambiandoLaFechaFueraDelRango_QuitaLaFilaDelListado` — valida AC-02, la parte que el filtro hace visible
- [ ] `Eliminar_PideConfirmacionAntesDeLlamar` — la mitigación R-19: sin confirmar, no hay petición
- [ ] `Eliminar_AlCancelar_NoLlamaAlServidor` — sad path
- [ ] `Eliminar_AlConfirmar_QuitaLaFila` — valida AC-05, montando `App`
- [ ] `Eliminar_ConMovimientoYaBorrado_MuestraElMensajeYRefresca` — sad path del `ErrorNoEncontrado`
- [ ] `Editar_ConRedCaida_MuestraElErrorConReintento` — sad path del `ErrorDeRed` documentado

**Completion criterion**

Los 8 tests listados pasan; editar y eliminar funcionan desde el listado con confirmación previa en
el borrado; y las dos costuras nuevas se ejercen montando `App`, no simulándolas con `rerender()` —
que es el defecto que costó una ronda de verificación en FEAT-001a.

---

## Final verification

- Los 6 FR y los 2 NFR del PRD tienen cobertura, y los 14 AC tienen al menos un test que los nombra.
- La suite completa pasa. El total observado al cerrar CODE fue **217 tests** (124 backend + 93
  frontend). La redacción original de esta línea decía "los 129 que FEAT-001a dejó verdes, más los
  57 de este ticket" = 186, y era falsa: el delta real fue de 88 tests. Se verificó que la
  diferencia es cobertura de más y no un conteo doble —cada test extra tiene su fila con su motivo
  en `docs/daw/reports/tdd-FEAT-001b.md`—. Corregido en el PLAN de FEAT-001c.
- Ningún test existente quedó desactualizado en silencio: `Listar_ConParametrosDesconocidos_LosIgnora` se reescribió, y los tests de frontend que ignoraban la query string pasaron a contemplarla.
- El SQL emitido por el listado filtrado contiene el `WHERE`, verificado con `ObservadorDeSql`.
- `ValidadorMovimiento` tiene una sola implementación de cada regla, compartida por POST y PUT.
- Las diez mitigaciones de `docs/daw/security/threat-FEAT-001b.md` §7 están incorporadas.
- Lint y format limpios en frontend; `dotnet build` sin warnings nuevos.
