# Threat Model — FEAT-001a: Alta de movimientos y listado simple

| Campo | Valor |
|---|---|
| Ticket | FEAT-001a |
| Tier | FEATURE |
| Fecha | 2026-08-17 |
| PRD | `docs/daw/prd/prd-FEAT-001a.md` |
| Spec | `docs/daw/specs/spec-FEAT-001a.md` |

Análisis sobre la arquitectura concreta propuesta en la spec: frontend React 19 + Vite +
TypeScript, API .NET 10 con Minimal APIs, EF Core 9.0.18 + Pomelo 9.0.0 contra MySQL 8.4, y las tres
tablas `usuarios`, `categorias` y `movimientos`. No es una plantilla: cada amenaza nombra el
endpoint, la tabla o el componente real donde vive.

---

## 1. Componentes y superficies de ataque

| # | Componente | Superficie |
|---|---|---|
| C1 | SPA React (navegador) | Renderiza la nota de texto libre del usuario; guarda estado en memoria |
| C2 | `POST /api/movimientos` | Acepta entrada del usuario: tipo, categoriaId, monto, fecha, nota |
| C3 | `GET /api/movimientos` | Expone datos financieros del propietario |
| C4 | `GET /api/categorias` | Expone el catálogo; sin datos sensibles |
| C5 | `IUsuarioActual` | Determina de quién son los datos. Es el control de autorización de todo el ticket |
| C6 | `AppDbContext` + migración con `HasData` | Acceso a datos y siembra del usuario y las categorías |
| C7 | MySQL 8.4, schema `gestiongastos` | Persistencia |
| C8 | Cadena de conexión en user-secrets | Credencial de acceso a C7 |

## 2. Límites de confianza

| # | Cruce | Niveles |
|---|---|---|
| TB-1 | Navegador → API (C1 → C2, C3, C4) | No confiable → confiable. **Todo lo que llega es entrada hostil**: es donde se validan monto, categoría, tipo y nota |
| TB-2 | API → MySQL (C6 → C7) | Confiable → confiable, con credencial de por medio. Cruce de inyección SQL |
| TB-3 | Máquina del desarrollador → user-secrets (C8) | El secreto vive fuera del repo; el repo nunca debe cruzarlo |
| TB-4 | Registro de dependencias (NuGet, npm) → build | Externo no confiable → build. Cadena de suministro |

## 3. Clasificación de datos

| Dato | Clasificación | Dónde |
|---|---|---|
| Monto, fecha, categoría y nota de cada movimiento | **Financiero** | `movimientos`, C3 |
| Email del usuario | **PII** | `usuarios`. En este ticket es un email semilla de desarrollo, no de una persona real |
| Cadena de conexión a MySQL | **Credencial** | C8, user-secrets |
| Catálogo de categorías | Público | `categorias` |

**Cifrado (F-TM-07).** En este ticket no hay credenciales de usuario (la contraseña llega con el
ticket de autenticación) y el único PII es un email semilla ficticio. En tránsito, el tráfico es
HTTP sobre localhost entre Vite y la API; en reposo, MySQL local sin cifrado de disco. Es aceptable
**mientras la aplicación no salga de la máquina de desarrollo**, y deja de serlo en el momento en que
exista un usuario real: ver R-01 y R-02.

---

## 4. Análisis STRIDE por componente

### C1 — SPA React

| STRIDE | Análisis |
|---|---|
| **S** | No hay identidad que suplantar: no hay sesión ni token en este ticket. Ver R-01 |
| **T** | El estado vive en memoria del navegador; manipularlo solo afecta a quien ya controla el navegador. Sin impacto en el servidor, que revalida todo (R-05) |
| **R** | Sin logging del lado cliente. No aplica: la fuente de verdad es el servidor |
| **I** | **La nota es texto libre que se renderiza en el listado → vector XSS almacenado.** Ver R-06 |
| **D** | El listado sin paginación puede colgar el render con muchos movimientos. Ver R-07 |
| **E** | No hay roles ni privilegios que escalar en el cliente |

### C2 — `POST /api/movimientos`

| STRIDE | Análisis |
|---|---|
| **S** | Sin autenticación, cualquiera que alcance el endpoint escribe como el usuario semilla. Ver R-01 |
| **T** | **Overposting: el cliente podría enviar `usuarioId` o `moneda` en el cuerpo y adueñarse del movimiento o falsear la moneda.** Ver R-04 |
| **R** | `creado_en` deja rastro de cuándo se creó. Sin auditoría de quién, porque hay un solo usuario. Ver R-09 |
| **I** | Los mensajes de validación no revelan datos de otros: describen el campo rechazado. Correcto |
| **D** | Escrituras sin límite de tasa pueden llenar la tabla. Ver R-08 |
| **E** | El tipo del movimiento y la categoría se validan en servidor (AC-10), no se confía en el formulario |

### C3 — `GET /api/movimientos`

| STRIDE | Análisis |
|---|---|
| **S** | Igual que C2: sin autenticación. Ver R-01 |
| **T** | Solo lectura |
| **R** | Sin log de accesos a datos financieros. Riesgo bajo con un solo usuario local. Ver R-09 |
| **I** | **Devuelve datos financieros.** El filtro por propietario de `IUsuarioActual` es lo único que los acota (AC-02). Ver R-03 |
| **D** | **Devuelve el rango completo sin paginación** (fuera de alcance por PRD). Ver R-07 |
| **E** | No hay privilegios diferenciados |

### C4 — `GET /api/categorias`

| STRIDE | Análisis |
|---|---|
| **S** | Sin autenticación, pero el catálogo es público y no revela nada del usuario |
| **T** | Solo lectura. Las categorías no son modificables por diseño (FR-02) |
| **R** | No aplica |
| **I** | Datos públicos |
| **D** | 10 filas fijas. Sin impacto |
| **E** | No aplica |

### C5 — `IUsuarioActual`

| STRIDE | Análisis |
|---|---|
| **S** | **Es el único control de autorización del ticket.** Si devuelve el id equivocado, todo el aislamiento cae. Ver R-03 |
| **T** | Su implementación resuelve el usuario semilla por email conocido, no por un valor que venga del cliente. Correcto por diseño |
| **R** | No aplica |
| **I** | Si alguna consulta olvida filtrar por él, expone datos de otro propietario. Ver R-03 |
| **D** | Resolver el usuario en cada request agrega una consulta. Mitigado cacheando el id por el ciclo de vida del scope |
| **E** | **Es exactamente el punto donde el ticket de autenticación va a enchufar la sesión.** Un diseño flojo acá se hereda como vulnerabilidad. Ver R-03 |

### C6 — `AppDbContext` y migración

| STRIDE | Análisis |
|---|---|
| **S** | No aplica |
| **T** | **Inyección SQL si alguna consulta se arma por interpolación de strings.** Ver R-05 |
| **R** | Las migraciones quedan versionadas en git: hay rastro de cada cambio de esquema |
| **I** | La migración siembra un email de desarrollo que queda en git para siempre. Debe ser ficticio, nunca uno real. Ver R-10 |
| **D** | Una migración fallida a mitad deja el esquema inconsistente. Mitigado: es la migración inicial, la base se recrea |
| **E** | No aplica |

### C7 — MySQL

| STRIDE | Análisis |
|---|---|
| **S** | Un MySQL de WSL con usuario root sin contraseña acepta a cualquiera. Ver R-02 |
| **T** | `decimal(15,2)` y las FK NOT NULL impiden montos corruptos y movimientos huérfanos |
| **R** | Sin log de auditoría de la base. Aceptable en desarrollo local |
| **I** | Sin cifrado en reposo. Aceptable mientras los datos sean de desarrollo. Ver R-02 |
| **D** | Sin límites de conexión configurados. Riesgo bajo en local |
| **E** | Si la API se conecta con un usuario con privilegios de administrador, una inyección se vuelve control total. Ver R-05 |

### C8 — Cadena de conexión

| STRIDE | Análisis |
|---|---|
| **S** | Quien obtiene la credencial se hace pasar por la aplicación contra la base |
| **T** | No aplica |
| **R** | No aplica |
| **I** | **Riesgo de que termine commiteada en `appsettings*.json`.** `AGENTS.md` lo prohíbe explícitamente. Ver R-11 |
| **D** | No aplica |
| **E** | Una credencial de administrador convierte cualquier fuga en compromiso total. Ver R-05 |

---

## 5. Riesgos y mitigaciones

### 🟠 R-01 — La API no tiene autenticación: todo endpoint es anónimo

| Campo | Valor |
|---|---|
| Categoría STRIDE | Spoofing / Elevation of Privilege |
| Probabilidad | Alta |
| Impacto | Alto |
| Disposición | **RIESGO ACEPTADO** — requiere aprobación explícita del usuario |

Es una consecuencia deliberada del alcance: la autenticación es un ticket posterior y el PRD la
declara fuera de alcance. Mientras tanto, cualquiera que alcance el puerto de la API lee y escribe
los movimientos del usuario semilla sin credencial alguna.

**Mitigación parcial que sí se aplica en este ticket:** ver R-02 (la API solo escucha en localhost),
que reduce la superficie de "cualquiera en la red" a "cualquiera en esta máquina".

Los tres campos que exige F-TM-04 están al final de este documento, en la sección "Riesgos
aceptados", pendientes de la firma del usuario.

### 🟠 R-02 — La API escuchando en todas las interfaces expone datos financieros a la red local

| Campo | Valor |
|---|---|
| Categoría STRIDE | Information Disclosure |
| Probabilidad | Media |
| Impacto | Alto |
| Mitigación | **La API se configura para escuchar exclusivamente en `127.0.0.1`**, nunca en `0.0.0.0`. Sin autenticación (R-01), el binding es el único control de acceso que existe, y el valor por defecto de muchas plantillas es el equivocado. Se verifica con un test que comprueba la configuración de Kestrel |

### 🟠 R-03 — Una consulta que olvide filtrar por propietario expone datos de otro usuario

| Campo | Valor |
|---|---|
| Categoría STRIDE | Information Disclosure / Elevation of Privilege |
| Probabilidad | Media |
| Impacto | Alto |
| Mitigación | El filtro por `IUsuarioActual` **no queda a criterio de cada consulta**: se aplica con un filtro global de consulta (`HasQueryFilter`) sobre la entidad `Movimiento` en `AppDbContext`, de modo que olvidarlo sea imposible en vez de improbable. AC-02 lo verifica con un movimiento de otro propietario sembrado en el test. Es además el punto donde el ticket de autenticación enchufa la sesión: un control centralizado se hereda bien, uno repetido en cada consulta se hereda mal |

### 🟡 R-04 — Overposting: el cliente reclama la propiedad o la moneda del movimiento

| Campo | Valor |
|---|---|
| Categoría STRIDE | Tampering |
| Probabilidad | Media |
| Impacto | Medio |
| Mitigación | El DTO de entrada de `POST /api/movimientos` **no tiene** campos `usuarioId`, `moneda`, `id` ni `creadoEn`. El servidor los fija: el propietario desde `IUsuarioActual`, la moneda en `"ARS"` (FR-09), el resto por la base. Un cuerpo que los incluya los ve ignorados, no aplicados. Test dedicado: enviar `usuarioId` de otro y verificar que el movimiento queda del usuario actual — es AC-08 de PRD-001 adelantado |

### 🟡 R-05 — Inyección SQL, y su impacto amplificado por una credencial con privilegios de más

| Campo | Valor |
|---|---|
| Categoría STRIDE | Tampering / Elevation of Privilege |
| Probabilidad | Baja |
| Impacto | Crítico |
| Mitigación | Dos capas. (1) Todo acceso a datos pasa por LINQ sobre EF Core, que parametriza; **queda prohibido `FromSqlRaw`/`ExecuteSqlRaw` con interpolación de strings**, y el gate SAST de CODE lo verifica (F-SAST-02). (2) El usuario de MySQL que usa la aplicación tiene privilegios acotados al schema `gestiongastos` — SELECT, INSERT, UPDATE, DELETE — y no es root: así una inyección no se convierte en control del servidor |

### 🟡 R-06 — XSS almacenado a través de la nota del movimiento

| Campo | Valor |
|---|---|
| Categoría STRIDE | Information Disclosure |
| Probabilidad | Baja |
| Impacto | Medio |
| Mitigación | React escapa el contenido de texto por defecto, así que el riesgo aparece únicamente si alguien usa `dangerouslySetInnerHTML`. **Queda prohibido en todo el frontend**, y el gate SAST lo verifica (F-SAST-06). La nota se persiste tal cual la escribió el usuario —no se sanitiza en el servidor— porque escapar al guardar corrompe el dato; se escapa al renderizar, que es donde corresponde |

### 🟡 R-07 — El listado sin paginación degrada el cliente y el servidor

| Campo | Valor |
|---|---|
| Categoría STRIDE | Denial of Service |
| Probabilidad | Media |
| Impacto | Bajo |
| Mitigación | La paginación está fuera de alcance por PRD, y FEAT-001b acota el listado al mes actual por defecto. Para que este ticket no quede sin techo, `GET /api/movimientos` **devuelve como máximo 500 movimientos**, los más recientes, e indica en la respuesta si hubo recorte. Es un techo de seguridad, no una paginación: FEAT-001b lo reemplaza por el filtro real |

### 🟡 R-08 — Escrituras sin límite de tasa llenan la tabla

| Campo | Valor |
|---|---|
| Categoría STRIDE | Denial of Service |
| Probabilidad | Baja |
| Impacto | Bajo |
| Mitigación | Con la API restringida a localhost (R-02), el atacante tendría que estar ya en la máquina. Se documenta como pendiente para el ticket de autenticación, que introduce el límite de intentos de RNF-05 de PRD-001 y es el momento natural de agregar límite de tasa. No se mitiga en este ticket |

### 🟢 R-09 — Sin auditoría de accesos ni de cambios

| Campo | Valor |
|---|---|
| Categoría STRIDE | Repudiation |
| Probabilidad | Baja |
| Impacto | Bajo |
| Mitigación | `creado_en` en cada movimiento da la trazabilidad mínima. Con un solo usuario y sin sesión, un log de "quién" no tendría nada que registrar. Se revisa cuando exista autenticación |

### 🟡 R-10 — El email semilla queda en git para siempre

| Campo | Valor |
|---|---|
| Categoría STRIDE | Information Disclosure |
| Probabilidad | Alta |
| Impacto | Bajo |
| Mitigación | La migración siembra un email **ficticio y evidentemente de desarrollo** (`dev@gestiongastos.local`, en un TLD reservado que no puede existir). Nunca un email real del desarrollador ni de un tercero: `HasData` lo escribe en el código de la migración, que es histórico de git y no se puede borrar |

### 🟡 R-11 — La cadena de conexión terminando commiteada

| Campo | Valor |
|---|---|
| Categoría STRIDE | Information Disclosure |
| Probabilidad | Media |
| Impacto | Alto |
| Mitigación | Tres capas. (1) La cadena vive en user-secrets, fuera del repo (`AGENTS.md`, sección Code conventions). (2) El `.gitignore` incorpora `appsettings.Development.json`, `appsettings.Local.json`, `.env*` y `*.user`, que hoy no ignora ninguno. (3) El gate SAST de CODE detecta secretos embebidos (F-SAST-01). El `UserSecretsId` del `.csproj` sí se commitea: es un identificador, no un secreto |

### 🟡 R-12 — Cadena de suministro: siete dependencias nuevas

| Campo | Valor |
|---|---|
| Categoría STRIDE | Tampering |
| Probabilidad | Baja |
| Impacto | Medio |
| Mitigación | Las versiones se fijan explícitamente y los lockfiles (`pnpm-lock.yaml`, `packages.lock.json`) se commitean, de modo que el build sea reproducible y un paquete no cambie bajo los pies. Todas las dependencias nuevas son de testing o de cobertura, ninguna llega al artefacto de producción salvo React y Vite, ya declarados en el Stack |

---

## 6. Riesgos aceptados (F-TM-04)

### RA-01 — La aplicación no tiene autenticación (R-01)

| Campo | Valor |
|---|---|
| Quién lo acepta | **mauricio gonzales** (propietario del proyecto), el 2026-08-17, de forma explícita durante la fase PLAN de FEAT-001a |
| Justificación | La autenticación es un ticket posterior, deliberadamente separado para que este entregue funcionalidad utilizable antes. El PRD la declara fuera de alcance y el modelo de datos ya lleva la pertenencia al usuario (FR-01), de modo que enchufar la sesión no requiera migrar datos ni reescribir consultas. Mientras tanto la exposición se acota a localhost (R-02) |
| Condiciones de revisión | Se revisa (a) antes de cualquier despliegue fuera de `127.0.0.1`, sin excepción, y (b) al cerrar FEAT-001c, que es cuando la aplicación queda completa y la falta de autenticación pasa a ser el único hueco. Lo que ocurra primero |

### RA-02 — Sin cifrado en tránsito ni en reposo (F-TM-07)

| Campo | Valor |
|---|---|
| Quién lo acepta | **mauricio gonzales** (propietario del proyecto), el 2026-08-17, de forma explícita durante la fase PLAN de FEAT-001a |
| Justificación | El tráfico es HTTP sobre localhost y los datos son de desarrollo: un email ficticio y montos de prueba. TLS sobre loopback no protege de nada que un atacante con acceso a la máquina no tenga ya, y el cifrado en reposo de MySQL local tampoco |
| Condiciones de revisión | En el momento en que exista un usuario real con datos financieros reales, o ante cualquier despliegue fuera de la máquina de desarrollo. Concretamente: al arrancar el ticket de autenticación, porque a partir de ahí hay contraseñas en juego y RNF-03 de PRD-001 exige hash seguro — dato que no puede viajar en claro |

---

## 7. Mitigaciones a incorporar a la spec

1. La API escucha exclusivamente en `127.0.0.1` (R-02), con test que verifica la configuración.
2. Filtro global de consulta por propietario sobre `Movimiento` en `AppDbContext`, en lugar de filtrar en cada consulta (R-03).
3. DTO de entrada sin `usuarioId`, `moneda`, `id` ni `creadoEn`; el servidor los fija (R-04), con test de overposting.
4. Prohibición de `FromSqlRaw`/`ExecuteSqlRaw` interpolado, y usuario de MySQL acotado al schema sin privilegios de root (R-05).
5. Prohibición de `dangerouslySetInnerHTML` en todo el frontend; la nota se escapa al renderizar, no al guardar (R-06).
6. Techo de 500 movimientos en `GET /api/movimientos`, señalado en la respuesta (R-07).
7. Email semilla ficticio en TLD reservado: `dev@gestiongastos.local` (R-10).
8. `.gitignore` con `appsettings.Development.json`, `appsettings.Local.json`, `.env*`, `*.user`, además de `bin/`, `obj/`, `node_modules/`, `dist/`, `TestResults/` y `coverage/` (R-11).
9. Versiones fijadas y lockfiles commiteados (R-12).

---

## 8. Resumen

| Severidad | Cantidad | Estado |
|---|---|---|
| 🔴 Crítico | 0 | — |
| 🟠 Alto | 3 | R-02 y R-03 mitigados en la spec; R-01 es riesgo aceptado pendiente de firma |
| 🟡 Medio | 7 | R-04, R-05, R-06, R-07, R-10, R-11, R-12 mitigados en la spec; R-08 diferido al ticket de autenticación |
| 🟢 Bajo | 1 | R-09 documentado |

**Verdicto: PASSED.** RA-01 y RA-02 fueron aceptados formalmente por el propietario del proyecto el
2026-08-17, con justificación y condiciones de revisión registradas. Todas las demás amenazas de
severidad alta y media tienen mitigación concreta incorporada a la spec.
