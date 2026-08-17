# Spec FEAT-001a: Alta de movimientos y listado simple

| Field | Value |
|-------|-------|
| Ticket | FEAT-001a |
| PRD | docs/daw/prd/prd-FEAT-001a.md |
| Tier | FEATURE |
| Date | 2026-08-17 |
| Spec loops | 0 |
| Threat model | docs/daw/security/threat-FEAT-001a.md |

## Summary

Backend .NET 10 con Minimal APIs y EF Core 9.0.18 + Pomelo 9.0.0 contra MySQL 8.4, en un único
proyecto `backend/GestionGastos.Api` organizado por carpetas de feature. Tres tablas —`usuarios`,
`categorias`, `movimientos`— creadas por una migración inicial que siembra las 10 categorías
predefinidas y el usuario de desarrollo. La pertenencia al usuario se aplica con un filtro global de
consulta en el `DbContext`, no consulta por consulta, y se obtiene de una única abstracción
`IUsuarioActual` que el ticket de autenticación reemplazará. Cuatro endpoints HTTP que validan en el
servidor y responden los rechazos como `ProblemDetails` (RFC 9457). Frontend React 19 + Vite +
TypeScript con un formulario de alta accesible por teclado y una tabla de listado. Tests de
integración xUnit contra una base MySQL real y tests de componente Vitest + Testing Library.

## Decisiones de diseño registradas

Cada una tiene su ADR, porque son las que se heredan sin volver a discutirse:

| Decisión | ADR |
|---|---|
| `backend/` + `frontend/`, proyecto único con carpetas por feature, Minimal APIs | `docs/adr/ADR-001-estructura-y-minimal-apis.md` |
| Tests de integración contra MySQL real en vez de proveedor en memoria | `docs/adr/ADR-002-tests-contra-mysql-real.md` |
| `IUsuarioActual` como forma de construir sin autenticación | `docs/adr/ADR-003-abstraccion-usuario-actual.md` |

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 1 (columna, FK, filtro global, `IUsuarioActual`), Block 3 (listado restringido) |
| FR-02 | Block 1 (semilla de 10 categorías), Block 2 (`GET /api/categorias`) |
| FR-03 | Block 2 (`POST /api/movimientos`, tipo gasto), Block 4 (formulario) |
| FR-04 | Block 2 (`POST /api/movimientos`, tipo ingreso), Block 4 (formulario) |
| FR-05 | Block 4 (fecha de hoy por defecto en el formulario) |
| FR-06 | Block 2 (validación de monto en servidor), Block 4 (validación en formulario) |
| FR-07 | Block 2 (categoría obligatoria y coherencia de tipo), Block 4 (selector filtrado por tipo) |
| FR-08 | Block 2 (nota opcional, máximo 120), Block 4 (campo nota), Block 5 (nota en la fila) |
| FR-09 | Block 1 (columna `moneda` con default y constante de dominio), Block 2 (el servidor la fija) |
| FR-10 | Block 3 (`GET /api/movimientos` ordenado), Block 5 (tabla) |
| FR-11 | Block 3 (forma de la respuesta), Block 5 (las cinco columnas de la fila) |
| NFR-01 | **Estrategia:** el alta es un `INSERT` único sin consultas previas más allá de la validación de categoría, que va por PK. Se mide con un test de rendimiento sobre 100 ejecuciones (Block 2) |
| NFR-02 | **Estrategia:** etiquetas `<label for>` asociadas a cada control, orden de tabulación natural sin `tabindex` positivos, `:focus-visible` con contraste 3:1, paleta verificada a 4.5:1 en texto. Se verifica con `user-event.tab()` (Block 4) |
| NFR-03 | **Estrategia:** `HasPrecision(15, 2)` explícito en la configuración de la entidad — el default de Pomelo para `decimal` es `decimal(65,30)`, así que omitirlo incumple el requisito sin que un test de suma lo note. `decimal` de C# de punta a punta, nunca `double`/`float`. Se verifica leyendo el tipo de columna del esquema y con la suma exacta (Block 1) |

## Dependencies between blocks

- **Block 1** no depende de nada. Es la base: sin su esquema y su `DbContext` no hay nada que probar.
- **Block 2** depende de Block 1 (entidades, `DbContext`, `IUsuarioActual`, semilla).
- **Block 3** depende de Block 1 y de Block 2 (reutiliza el DTO de salida que Block 2 define para el 201).
- **Block 4** depende de Block 2 (consume `GET /api/categorias` y `POST /api/movimientos`).
- **Block 5** depende de Block 3 (consume `GET /api/movimientos`) y de Block 4 (scaffolding y cliente HTTP).

Orden de ejecución: **1 → 2 → 3 → 4 → 5**.

---

## Block 1 — Fundaciones, modelo de datos y migración inicial

**Files**

- `backend/GestionGastos.sln` (nuevo) — solución con los dos proyectos.
- `backend/GestionGastos.Api/GestionGastos.Api.csproj` (nuevo) — proyecto web, `UserSecretsId`, versiones fijadas.
- `backend/GestionGastos.Api/Program.cs` (nuevo) — DI, `DbContext`, `AddProblemDetails()`, `UseExceptionHandler`, binding a `127.0.0.1`.
- `backend/GestionGastos.Api/Common/Moneda.cs` (nuevo) — constante de dominio `Moneda.Predeterminada = "ARS"`.
- `backend/GestionGastos.Api/Common/IUsuarioActual.cs` (nuevo) — abstracción, con miembro asíncrono.
- `backend/GestionGastos.Api/Common/UsuarioSemillaActual.cs` (nuevo) — implementación de este ticket.
- `backend/GestionGastos.Api/Data/AppDbContext.cs` (nuevo) — tres `DbSet`, filtro global sobre `Movimiento`.
- `backend/GestionGastos.Api/Data/Entidades/Usuario.cs` (nuevo)
- `backend/GestionGastos.Api/Data/Entidades/Categoria.cs` (nuevo)
- `backend/GestionGastos.Api/Data/Entidades/Movimiento.cs` (nuevo)
- `backend/GestionGastos.Api/Data/Entidades/TipoMovimiento.cs` (nuevo) — enum `Gasto = 1`, `Ingreso = 2`.
- `backend/GestionGastos.Api/Data/Configuraciones/*.cs` (nuevos, 3) — `IEntityTypeConfiguration` por entidad, con precisión, largos, índices y `HasData`.
- `backend/GestionGastos.Api/Migrations/*` (nuevos) — migración inicial generada.
- `backend/GestionGastos.Api/appsettings.json` (nuevo) — **sin sección `ConnectionStrings`**.
- `backend/GestionGastos.Api.Tests/GestionGastos.Api.Tests.csproj` (nuevo) — xUnit, `Microsoft.AspNetCore.Mvc.Testing`, `coverlet.collector`.
- `backend/GestionGastos.Api.Tests/Infra/BaseDeDatosFixture.cs` (nuevo) — crea y migra `gestiongastos_test`, limpia entre tests.
- `backend/GestionGastos.Api.Tests/Infra/ApiFactory.cs` (nuevo) — `WebApplicationFactory` con la cadena de test.
- `backend/db/README.md` (nuevo) — cómo crear la base y el usuario acotado.
- `.gitignore` (modificado) — agrega las entradas de .NET, Node y secretos.

**Logic**

Define las tres entidades y su configuración explícita, genera la migración inicial con la semilla, y
deja montada la infraestructura de tests. Registra `IUsuarioActual` en el contenedor con ciclo de
vida *scoped* y aplica el filtro global de propietario sobre `Movimiento` en `OnModelCreating`, de
modo que ninguna consulta posterior pueda olvidarlo (mitigación R-03 del threat model).

La cadena de conexión de la API se lee de user-secrets; **no existe sección `ConnectionStrings` en
ningún `appsettings*.json`**, y `Program.cs` falla al arrancar con un mensaje explícito si no la
encuentra, en lugar de caer más tarde con un error de conexión que invita a escribirla en el archivo
equivocado. La cadena de los tests llega por la variable de entorno
`ConnectionStrings__Default`, con un valor por defecto apuntando a `gestiongastos_test` en
`localhost`: con `dotnet test` los user-secrets se resuelven contra el assembly de entrada, que es el
proyecto de tests y no la API, así que apoyarse en ellos dejaría la credencial sin domicilio
(mitigación R-11).

La API escucha exclusivamente en `127.0.0.1` (mitigación R-02): sin autenticación, el binding es el
único control de acceso que existe.

**Data model**

`usuarios`
| Columna | Tipo | Restricciones |
|---|---|---|
| `id` | int | PK, autoincremental |
| `email` | varchar(320) | NOT NULL, UNIQUE |
| `creado_en` | datetime(6) | NOT NULL, UTC |

`categorias`
| Columna | Tipo | Restricciones |
|---|---|---|
| `id` | int | PK, autoincremental |
| `nombre` | varchar(60) | NOT NULL |
| `tipo` | tinyint unsigned | NOT NULL, 1=Gasto, 2=Ingreso |
| | | UNIQUE (`nombre`, `tipo`) — "Otros" existe en los dos tipos |

`movimientos`
| Columna | Tipo | Restricciones |
|---|---|---|
| `id` | int | PK, autoincremental |
| `usuario_id` | int | NOT NULL, FK → `usuarios.id`, ON DELETE RESTRICT |
| `tipo` | tinyint unsigned | NOT NULL, 1=Gasto, 2=Ingreso |
| `categoria_id` | int | NOT NULL, FK → `categorias.id`, ON DELETE RESTRICT |
| `monto` | decimal(15,2) | NOT NULL, `HasPrecision(15, 2)` explícito |
| `moneda` | char(3) | NOT NULL, DEFAULT `'ARS'` |
| `fecha` | date | NOT NULL, `DateOnly` en C#, sin hora ni zona horaria |
| `nota` | varchar(120) | NULL |
| `creado_en` | datetime(6) | NOT NULL, UTC |
| | | INDEX (`usuario_id`, `fecha` DESC, `id` DESC) |

`tipo` se duplica en `movimientos` y en `categorias` de forma deliberada: `movimientos.tipo` es el
dato del movimiento y `categorias.tipo` el del catálogo. La coherencia entre ambos **no** se delega a
la base, se valida en el servidor (Block 2, AC-10), porque MySQL no tiene CHECK sobre una tabla
ajena. Para que no diverjan, `movimientos.tipo` se **deriva siempre** de la categoría elegida al
crear y al modificar: el cliente nunca lo envía.

**Semilla** (`HasData`, ids fijos)
- Usuario id 1: `dev@gestiongastos.local` — TLD reservado, email deliberadamente inexistente (mitigación R-10).
- Categorías id 1–7, tipo Gasto: Comida, Transporte, Vivienda, Servicios, Salud, Ocio, Otros.
- Categorías id 8–10, tipo Ingreso: Sueldo, Ingreso extra, Otros.

**Input validation**

Este bloque no acepta entrada de usuario final. La única entrada es de configuración:
- `ConnectionStrings:Default` — string no vacío. Ausente → la aplicación no arranca.

**Error handling**

| Error | Manejo |
|---|---|
| Falta la cadena de conexión | `InvalidOperationException` al arrancar, con mensaje que nombra user-secrets y la variable de entorno. Nunca un valor por defecto silencioso |
| MySQL no disponible | La excepción de conexión se propaga con el mensaje del proveedor; no se atrapa ni se degrada a un modo sin base |
| Excepción no controlada en cualquier endpoint | `UseExceptionHandler` + `AddProblemDetails()` la convierten en `ProblemDetails` 500 con `traceId`, sin stack trace fuera de Development. Nunca un cuerpo HTML ni vacío |

> Una migración que falle a mitad no se maneja: es la migración inicial, y la recuperación es borrar
> y recrear la base. `backend/db/README.md` lo documenta como procedimiento operativo, no como camino
> de código.

**Required tests**

- [ ] `Migracion_CreaLasTresTablas` — la base migrada tiene `usuarios`, `categorias` y `movimientos`.
- [ ] `Migracion_SiembraDiezCategorias` — 7 de tipo Gasto y 3 de tipo Ingreso — valida AC-03 y AC-04 a nivel de datos.
- [ ] `Migracion_SiembraUsuarioDeDesarrollo` — existe el usuario con email `dev@gestiongastos.local`.
- [ ] `Monto_UsaDecimalDeQuinceComaDos` — el tipo de columna de `movimientos.monto` en `information_schema` es `decimal(15,2)`, no `decimal(65,30)` — valida NFR-03.
- [ ] `Monto_SumaExactaSinRedondeo` — persistir 0.10 y 0.20 y leer 0.30 — valida AC-19.
- [ ] `Fecha_PersisteSinDesplazamiento` — guardar 2026-03-01 y leer 2026-03-01 — valida el riesgo de zona horaria.
- [ ] `Movimiento_PersisteElPropietario` — el movimiento creado queda con el `usuario_id` de `IUsuarioActual` — valida AC-01.
- [ ] `FiltroGlobal_ExcluyeMovimientosDeOtroPropietario` — sembrar un segundo usuario con un movimiento y verificar que no aparece — valida AC-02 y la mitigación R-03.
- [ ] `Categoria_RechazaNombreYTipoDuplicados` — sad path: insertar dos veces (Comida, Gasto) viola el UNIQUE.
- [ ] `Configuracion_SinCadenaDeConexion_NoArranca` — sad path: sin la cadena, el arranque lanza `InvalidOperationException` con mensaje explícito.
- [ ] `Configuracion_AppsettingsNoContieneConnectionStrings` — sad path: ningún `appsettings*.json` del proyecto declara la sección — valida la mitigación R-11.
- [ ] `Kestrel_EscuchaSoloEnLoopback` — la configuración de binding es `127.0.0.1` y no `0.0.0.0` — valida la mitigación R-02.
- [ ] `MysqlNoDisponible_PropagaElErrorDeConexion` — sad path: con la cadena apuntando a un puerto cerrado, la operación falla con el error del proveedor y no se degrada silenciosamente.
- [ ] `ExcepcionNoControlada_DevuelveProblemDetailsSinStackTrace` — sad path: un endpoint de prueba que lanza devuelve `application/problem+json` con `traceId` y sin stack trace.

**Completion criterion**

`dotnet build` compila sin advertencias; `dotnet ef database update` crea el esquema en
`gestiongastos_test`; los 12 tests de arriba pasan; `information_schema` reporta
`decimal(15,2)` para `movimientos.monto` y `date` para `movimientos.fecha`; `git status` no muestra
`bin/`, `obj/` ni `appsettings.Development.json` como archivos sin seguimiento.

---

## Block 2 — API de categorías y alta de movimiento

**Files**

- `backend/GestionGastos.Api/Categorias/CategoriasEndpoints.cs` (nuevo)
- `backend/GestionGastos.Api/Categorias/CategoriaDto.cs` (nuevo)
- `backend/GestionGastos.Api/Movimientos/MovimientosEndpoints.cs` (nuevo) — `POST` y `GET /{id}`.
- `backend/GestionGastos.Api/Movimientos/CrearMovimientoRequest.cs` (nuevo) — DTO de entrada.
- `backend/GestionGastos.Api/Movimientos/MovimientoDto.cs` (nuevo) — DTO de salida, compartido con Block 3.
- `backend/GestionGastos.Api/Movimientos/ValidadorMovimiento.cs` (nuevo) — validación a mano.
- `backend/GestionGastos.Api/Common/ResultadoValidacion.cs` (nuevo) — tipo de resultado, errores tipados.
- `backend/GestionGastos.Api/Program.cs` (modificado) — registra los endpoints.
- `backend/GestionGastos.Api.Tests/Movimientos/CrearMovimientoTests.cs` (nuevo)
- `backend/GestionGastos.Api.Tests/Movimientos/RendimientoAltaTests.cs` (nuevo)
- `backend/GestionGastos.Api.Tests/Categorias/CategoriasEndpointsTests.cs` (nuevo)

**Logic**

Expone el catálogo y el alta. La validación no lanza excepciones para el flujo esperado: devuelve un
`ResultadoValidacion` con la lista de errores por campo, que el endpoint traduce a
`ValidationProblem`. El `tipo` del movimiento se **deriva** de la categoría elegida, nunca se toma
del cliente. La moneda la fija el servidor desde `Moneda.Predeterminada`, no como literal inline
suelto en el handler (FR-09).

El DTO de entrada **no tiene** campos `id`, `usuarioId`, `moneda`, `tipo` ni `creadoEn`: un cuerpo
que los incluya los ve ignorados, no aplicados (mitigación R-04).

Todo acceso a datos va por LINQ sobre EF Core. `FromSqlRaw`/`ExecuteSqlRaw` con interpolación de
strings queda prohibido en todo el proyecto (mitigación R-05).

**API contract**

`GET /api/categorias`
- Request: sin parámetros.
- Response 200: `[{ id: int, nombre: string, tipo: "gasto" | "ingreso" }]`, ordenado por `tipo` y `nombre`.
- Errores: ninguno propio. 500 `ProblemDetails` ante fallo de base.
- Auth: ninguna (RA-01, riesgo aceptado). El propietario no participa: el catálogo es global.

`POST /api/movimientos`
- Request: `{ categoriaId: int, monto: decimal, fecha: string "yyyy-MM-dd", nota: string | null }`
- Response 201: `Location: /api/movimientos/{id}` + `MovimientoDto`
- `MovimientoDto`: `{ id: int, tipo: "gasto"|"ingreso", categoria: { id: int, nombre: string }, monto: decimal, moneda: string, fecha: "yyyy-MM-dd", nota: string | null }`
- Errores: `400` `ProblemDetails` RFC 9457 con extensión `errors` (diccionario campo → mensajes).
- Auth: ninguna (RA-01). El propietario sale de `IUsuarioActual`, nunca del cuerpo.

`GET /api/movimientos/{id}`
- Request: `id` en la ruta.
- Response 200: `MovimientoDto`.
- Errores: `404` `ProblemDetails` si no existe o no es del propietario — el filtro global hace que ambos casos sean indistinguibles, que es lo correcto.
- Auth: ninguna (RA-01). Restringido por el filtro global de propietario.
- Existe en este bloque porque el `Location` del 201 debe apuntar a algo real; FEAT-001b lo necesita además para editar.

**Input validation**

| Campo | Reglas |
|---|---|
| `categoriaId` | int, obligatorio, debe existir en `categorias`. Ausente o 0 → "La categoría es obligatoria" |
| `monto` | decimal, obligatorio, > 0, máximo 2 decimales, ≤ 9999999999999.99 |
| `fecha` | string `yyyy-MM-dd`, obligatoria, parseable como `DateOnly` |
| `nota` | string opcional, máximo 120 caracteres. Vacío o solo espacios se normaliza a `null` |

**Error handling**

| Error | Manejo |
|---|---|
| Monto vacío, no numérico, ≤ 0 o con más de 2 decimales | 400 `ProblemDetails`, `errors.monto` con el motivo. No se crea nada (AC-08) |
| `categoriaId` ausente o 0 | 400 `ProblemDetails`, `errors.categoriaId` = "La categoría es obligatoria" (AC-09) |
| `categoriaId` inexistente | 400 `ProblemDetails`, `errors.categoriaId` = "La categoría no existe". **Se valida antes de insertar**: dejar que la FK falle daría 500 en vez de un error de validación |
| Tipo cruzado: el formulario pide un gasto y la categoría es de ingreso | 400 `ProblemDetails`, `errors.categoriaId` con el motivo (AC-10). Como el tipo se deriva de la categoría, el caso solo llega si el cliente manda un `tipoEsperado` explícito, que el endpoint compara |
| Nota de más de 120 caracteres | 400 `ProblemDetails`, `errors.nota` con el motivo (AC-13) |
| Fecha con formato inválido | 400 `ProblemDetails`, `errors.fecha` = "La fecha debe tener formato yyyy-MM-dd" |
| Cuerpo JSON malformado | 400 `ProblemDetails` genérico, sin exponer el detalle del parser |
| `GET /{id}` inexistente o de otro propietario | 404 `ProblemDetails`, sin distinguir los dos casos |
| Fallo de base al insertar | Se propaga al handler global → 500 `ProblemDetails` con `traceId`, sin stack trace |

**Required tests**

- [ ] `GetCategorias_DevuelveSieteDeGasto` — valida AC-03.
- [ ] `GetCategorias_DevuelveTresDeIngreso` — valida AC-04.
- [ ] `CrearGasto_Valido_Devuelve201YPersiste` — valida AC-05.
- [ ] `CrearIngreso_Valido_Devuelve201YPersiste` — valida AC-06.
- [ ] `Crear_FijaMonedaARS` — la respuesta y la fila persistida tienen `"ARS"` — valida AC-14.
- [ ] `Crear_ConNotaDeCientoVeinte_LaPersiste` — valida AC-11.
- [ ] `Crear_ConNotaVacia_PersisteNull` — valida AC-12.
- [ ] `Crear_DerivaElTipoDeLaCategoria` — categoría de ingreso ⇒ movimiento de tipo ingreso.
- [ ] `Crear_IgnoraUsuarioIdDelCuerpo` — sad path: enviar `usuarioId` de otro usuario y verificar que el movimiento queda del actual — valida la mitigación R-04.
- [ ] `Crear_IgnoraMonedaDelCuerpo` — sad path: enviar `"USD"` y verificar que persiste `"ARS"`.
- [ ] `Crear_MontoCero_Devuelve400` — sad path, AC-08.
- [ ] `Crear_MontoNegativo_Devuelve400` — sad path, AC-08.
- [ ] `Crear_MontoConTresDecimales_Devuelve400` — sad path, AC-08.
- [ ] `Crear_MontoAusente_Devuelve400` — sad path, AC-08.
- [ ] `Crear_SinCategoria_Devuelve400` — sad path, AC-09.
- [ ] `Crear_CategoriaInexistente_Devuelve400NoQuinientos` — sad path: verifica que la FK no se convierte en 500.
- [ ] `Crear_TipoCruzado_Devuelve400` — sad path, AC-10.
- [ ] `Crear_NotaDeCientoVeintiuno_Devuelve400` — sad path, AC-13.
- [ ] `Crear_FechaConFormatoInvalido_Devuelve400` — sad path.
- [ ] `Crear_JsonMalformado_Devuelve400SinDetalleInterno` — sad path.
- [ ] `Crear_Rechazado_NoCreaNingunMovimiento` — tras cada 400, la tabla sigue igual — valida el "no se crea ningún movimiento" de AC-08 a AC-13.
- [ ] `GetPorId_Existente_Devuelve200` — el `Location` del 201 resuelve.
- [ ] `GetPorId_Inexistente_Devuelve404` — sad path.
- [ ] `GetPorId_DeOtroPropietario_Devuelve404` — sad path.
- [ ] `Crear_FalloDeBase_DevuelveProblemDetails500SinStackTrace` — sad path: con la base caída, el alta responde `application/problem+json` con `traceId` y sin stack trace.
- [ ] `Alta_PercentilNoventaYCincoMenorAUnSegundo` — 100 ejecuciones sobre base con 1000 movimientos — valida AC-17 y NFR-01.

**Completion criterion**

Los 25 tests pasan; `POST /api/movimientos` con cuerpo válido devuelve 201 con `Location` que
resuelve a 200; los ocho casos de rechazo devuelven 400 con `errors` poblado y dejan la tabla
`movimientos` sin filas nuevas; el p95 del alta medido sobre 100 ejecuciones es menor a 1 s.

---

## Block 3 — API de listado

**Files**

- `backend/GestionGastos.Api/Movimientos/MovimientosEndpoints.cs` (modificado) — agrega `GET /api/movimientos`.
- `backend/GestionGastos.Api/Movimientos/ListadoMovimientosResponse.cs` (nuevo) — envoltorio con la marca de recorte.
- `backend/GestionGastos.Api.Tests/Movimientos/ListarMovimientosTests.cs` (nuevo)

**Logic**

Devuelve los movimientos del propietario ordenados por `fecha DESC, id DESC` — el desempate por id
evita que dos movimientos del mismo día salgan en orden distinto entre llamadas. Reutiliza el
`MovimientoDto` de Block 2, de modo que el listado y el alta no puedan divergir en su forma.

Aplica un techo de **500 movimientos** (mitigación R-07): la paginación está fuera de alcance por
PRD y FEAT-001b la reemplaza por el filtro del mes actual, pero hasta entonces un listado sin límite
es un vector de degradación gratuito. La respuesta indica si hubo recorte, para que el frontend
pueda avisarlo en vez de mentir por omisión.

La restricción por propietario no se escribe en esta consulta: la aplica el filtro global de Block 1.
El test la verifica igual, porque lo que importa es el comportamiento, no dónde está la línea.

**API contract**

`GET /api/movimientos`
- Request: sin parámetros. Los filtros llegan en FEAT-001b.
- Response 200: `{ items: MovimientoDto[], recortado: boolean, total: int }` — `recortado` es `true` cuando el propietario tiene más de 500 movimientos.
- Orden: `fecha DESC, id DESC`.
- Errores: 500 `ProblemDetails` ante fallo de base.
- Auth: ninguna (RA-01). Restringido por el filtro global de propietario.

**Input validation**

Este endpoint no acepta entrada del usuario en esta iteración: sin parámetros de ruta, de query ni
cuerpo. La validación de los filtros llega con FEAT-001b.

**Error handling**

| Error | Manejo |
|---|---|
| El propietario no tiene movimientos | 200 con `items: []`, `recortado: false`, `total: 0`. **No es un error**: una lista vacía es una respuesta válida, nunca un 404 |
| Más de 500 movimientos | 200 con los 500 más recientes y `recortado: true`. No se trunca en silencio |
| Fallo de base al consultar | Se propaga al handler global → 500 `ProblemDetails` con `traceId`, sin stack trace |
| Query string con parámetros desconocidos | Se ignoran; no se rechaza la petición. Los filtros de FEAT-001b los definirán |

**Required tests**

- [ ] `Listar_DevuelveGastosEIngresosJuntos` — valida AC-15.
- [ ] `Listar_OrdenaPorFechaDescendente` — valida AC-15.
- [ ] `Listar_DesempataPorIdDescendente` — dos movimientos del mismo día salen en orden estable.
- [ ] `Listar_CadaFilaTraeLosCincoDatos` — fecha, tipo, nombre de categoría, monto con moneda y nota — valida AC-16.
- [ ] `Listar_ExcluyeMovimientosDeOtroPropietario` — valida AC-02.
- [ ] `Listar_SinMovimientos_DevuelveListaVaciaNo404` — sad path: cero movimientos no es un error.
- [ ] `Listar_ConMasDeQuinientos_RecortaYLoSeñala` — sad path: 501 movimientos devuelven 500 items y `recortado: true` — valida la mitigación R-07.
- [ ] `Listar_ConNotaNula_DevuelveNullNoCadenaVacia` — sad path: la ausencia de nota se representa como `null`.
- [ ] `Listar_FalloDeBase_DevuelveProblemDetails500SinStackTrace` — sad path: con la base caída, responde `application/problem+json` con `traceId`.
- [ ] `Listar_ConParametrosDesconocidos_LosIgnora` — sad path: `?foo=bar` devuelve 200 con el listado completo, no 400.

**Completion criterion**

Los 8 tests pasan; `GET /api/movimientos` devuelve los movimientos del propietario en orden
`fecha DESC, id DESC`; con 501 movimientos sembrados devuelve exactamente 500 items y
`recortado: true`; con un movimiento de otro propietario sembrado, ese movimiento no aparece.

---

## Block 4 — Frontend: scaffolding y formulario de alta

**Files**

- `frontend/package.json` (nuevo) — versiones fijadas.
- `frontend/pnpm-lock.yaml` (nuevo) — commiteado (mitigación R-12).
- `frontend/vite.config.ts` (nuevo) — proxy `/api` hacia la API, config de Vitest.
- `frontend/tsconfig.json` (nuevo) — `strict: true`.
- `frontend/eslint.config.js` (nuevo), `frontend/.prettierrc` (nuevo)
- `frontend/index.html` (nuevo)
- `frontend/src/main.tsx` (nuevo), `frontend/src/App.tsx` (nuevo)
- `frontend/src/api/cliente.ts` (nuevo) — `fetch` tipado, traduce `ProblemDetails` a errores por campo.
- `frontend/src/api/tipos.ts` (nuevo) — tipos del `MovimientoDto` y `CategoriaDto`.
- `frontend/src/movimientos/FormularioMovimiento.tsx` (nuevo)
- `frontend/src/movimientos/fecha.ts` (nuevo) — `hoyComoIso()`, formatea sin `toISOString()`.
- `frontend/src/estilos/tokens.css` (nuevo) — paleta con contraste verificado.
- `frontend/src/movimientos/FormularioMovimiento.test.tsx` (nuevo)
- `frontend/src/movimientos/fecha.test.ts` (nuevo)
- `frontend/src/api/cliente.test.ts` (nuevo)

**Logic**

Formulario con selector de tipo (gasto/ingreso), selector de categoría filtrado por el tipo elegido,
monto, fecha y nota. La fecha se inicializa con el día actual **formateando los componentes locales
de la fecha**, nunca con `toISOString()`: ese método convierte a UTC y desplaza el día para cualquier
huso negativo, que es exactamente el riesgo que el PRD identifica.

El cliente HTTP traduce el `errors` del `ProblemDetails` a un mapa campo → mensaje, que el formulario
muestra junto al control correspondiente. **Ningún `catch` queda vacío**: un error inesperado se
propaga a un estado de error visible, no se traga.

**Input validation**

Espeja la del servidor, que sigue siendo la autoridad:

| Campo | Reglas en cliente |
|---|---|
| tipo | radio, obligatorio, valor por defecto "gasto" |
| categoría | select, obligatorio, opciones del tipo elegido |
| monto | `inputMode="decimal"`, obligatorio, > 0, máximo 2 decimales |
| fecha | `<input type="date">`, obligatoria, por defecto hoy |
| nota | `<textarea>`, opcional, `maxLength=120` con contador visible |

**Error handling**

| Error | Manejo |
|---|---|
| Validación rechazada por el servidor (400) | Los mensajes de `errors` se muestran junto a cada campo, con `aria-describedby`. El formulario conserva lo cargado |
| La API no responde (red caída) | Mensaje de error visible con opción de reintentar. Nunca un `catch` silencioso |
| 500 del servidor | Mensaje genérico visible; el `traceId` del `ProblemDetails` se registra en consola para diagnóstico |
| `GET /api/categorias` falla al montar | El formulario se muestra deshabilitado con el motivo, en vez de un selector vacío sin explicación |
| Doble envío por doble clic | El botón se deshabilita mientras la petición está en vuelo |

**Required tests**

- [ ] `Formulario_ProponeLaFechaDeHoy` — valida AC-07.
- [ ] `fecha_HoyComoIso_NoUsaToISOString_NoDesplazaElDia` — con el reloj fijado en un huso negativo a las 22:00, devuelve el día local — valida el riesgo de zona horaria.
- [ ] `Formulario_TipoGasto_OfreceSoloCategoriasDeGasto` — valida AC-03.
- [ ] `Formulario_TipoIngreso_OfreceSoloCategoriasDeIngreso` — valida AC-04.
- [ ] `Formulario_GastoValido_EnviaYLimpia` — valida AC-05.
- [ ] `Formulario_IngresoValido_EnviaYLimpia` — valida AC-06.
- [ ] `Formulario_ConNota_LaEnvia` — valida AC-11.
- [ ] `Formulario_SinNota_EnviaNull` — valida AC-12.
- [ ] `Formulario_RecorridoSoloConTeclado_PermiteGuardar` — `user-event.tab()` recorre todos los controles y envía — valida AC-18 y NFR-02.
- [ ] `Formulario_CadaControlTieneEtiquetaAsociada` — `getByLabelText` resuelve los cinco controles — valida AC-18.
- [ ] `Formulario_MontoCero_MuestraElMotivoJuntoAlCampo` — sad path, AC-08.
- [ ] `Formulario_MontoConTresDecimales_MuestraElMotivo` — sad path, AC-08.
- [ ] `Formulario_SinCategoria_MuestraElMotivo` — sad path, AC-09.
- [ ] `Formulario_NotaDeCientoVeintiuno_MuestraElMotivo` — sad path, AC-13.
- [ ] `Formulario_ErrorDeRed_MuestraMensajeYPermiteReintentar` — sad path: el `catch` no queda silencioso.
- [ ] `Formulario_ErrorQuinientos_MuestraMensajeGenerico` — sad path.
- [ ] `Formulario_CategoriasNoCargan_SeDeshabilitaConMotivo` — sad path.
- [ ] `Formulario_DobleClic_EnviaUnaSolaVez` — sad path.
- [ ] `cliente_TraduceProblemDetailsAErroresPorCampo` — el `errors` del RFC 9457 se mapea por nombre de campo.

**Completion criterion**

`pnpm build` compila sin errores de tipo con `strict: true`; `pnpm lint` pasa; los 19 tests pasan;
recorrer el formulario con `Tab` alcanza los cinco controles en orden y permite enviar; ningún
control queda sin `<label>` asociado.

---

## Block 5 — Frontend: listado

**Files**

- `frontend/src/movimientos/ListadoMovimientos.tsx` (nuevo)
- `frontend/src/movimientos/formato.ts` (nuevo) — formato de monto con moneda y de fecha para mostrar.
- `frontend/src/App.tsx` (modificado) — compone formulario y listado, refresca el listado tras un alta.
- `frontend/src/movimientos/ListadoMovimientos.test.tsx` (nuevo)
- `frontend/src/movimientos/formato.test.ts` (nuevo)

**Logic**

Tabla con una fila por movimiento y cinco columnas: fecha, tipo, categoría, monto con su moneda y
nota. Tras un alta exitosa el listado se recarga, de modo que el movimiento recién creado aparezca
sin recargar la página (AC-05, AC-06).

La nota se renderiza como texto. **`dangerouslySetInnerHTML` queda prohibido en todo el frontend**
(mitigación R-06): React escapa por defecto, y ese método es la única forma de perder esa garantía.

Cuando la respuesta viene con `recortado: true`, la tabla lo avisa explícitamente.

**Input validation**

Este bloque no acepta entrada del usuario: solo renderiza la respuesta de `GET /api/movimientos`. La
validación aplicable es de la respuesta: `nota` puede ser `null`, y `items` puede estar vacío.

**Error handling**

| Error | Manejo |
|---|---|
| El listado viene vacío | Estado vacío explícito ("todavía no hay movimientos"), no una tabla con encabezados y nada debajo |
| `GET /api/movimientos` falla | Mensaje de error visible con opción de reintentar. Nunca un `catch` silencioso ni una tabla vacía que simule "no hay datos" |
| `recortado: true` | Aviso visible de que se muestran los 500 más recientes |
| `nota` nula | Celda vacía, sin texto de relleno (AC-12) |

**Required tests**

- [ ] `Listado_MuestraGastosEIngresos_OrdenadosPorFecha` — valida AC-15.
- [ ] `Listado_CadaFilaMuestraLosCincoDatos` — valida AC-16.
- [ ] `Listado_MuestraLaMonedaJuntoAlMonto` — valida AC-14.
- [ ] `Listado_MuestraLaNota` — valida AC-11.
- [ ] `Listado_TrasUnAlta_MuestraElMovimientoNuevo` — valida AC-05 y AC-06 de punta a punta.
- [ ] `Listado_NotaNula_CeldaVaciaSinRelleno` — sad path, AC-12.
- [ ] `Listado_SinMovimientos_MuestraEstadoVacio` — sad path.
- [ ] `Listado_ErrorDeRed_MuestraMensajeYPermiteReintentar` — sad path: el `catch` no queda silencioso.
- [ ] `Listado_Recortado_AvisaAlUsuario` — sad path: `recortado: true` se comunica.
- [ ] `Listado_NotaConHtml_SeMuestraComoTextoPlano` — sad path: `<img onerror=...>` en la nota se renderiza escapado — valida la mitigación R-06.

**Completion criterion**

Los 10 tests pasan; con tres movimientos sembrados la tabla muestra tres filas de cinco columnas
ordenadas por fecha descendente; una nota con HTML aparece como texto literal en el DOM y no como
elemento; cargar la app sin movimientos muestra el estado vacío y no una tabla desnuda.

---

## Final verification

Cuando los cinco bloques estén completos debe cumplirse todo esto a la vez:

1. Los 19 criterios de aceptación del PRD tienen al menos un test que los nombra y pasa.
2. Cobertura de línea, rama y función ≥ 80% en backend y en frontend, medida con `coverlet` y `@vitest/coverage-v8`.
3. `dotnet build` y `pnpm build` sin errores ni advertencias; `pnpm lint` y `dotnet format --verify-no-changes` limpios.
4. El esquema en `information_schema` reporta `decimal(15,2)` para `movimientos.monto` y `date` para `movimientos.fecha`.
5. Ningún `appsettings*.json` contiene una sección `ConnectionStrings`; `git ls-files` no devuelve ningún archivo con credenciales.
6. Ninguna aparición de `FromSqlRaw`, `ExecuteSqlRaw` ni `dangerouslySetInnerHTML` en el código.
7. La API arranca escuchando en `127.0.0.1` y no responde en la IP de red de la máquina.
8. Levantando la aplicación completa: se carga un gasto y un ingreso desde el formulario, ambos aparecen en el listado con su moneda, y un monto inválido muestra el motivo sin crear nada.
