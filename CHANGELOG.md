# Changelog

Todos los cambios notables de este proyecto se documentan acá.

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el proyecto se adhiere
a [Versionado Semántico](https://semver.org/lang/es/).

## [Unreleased]

### Added

- **FEAT-001a** — Alta de movimientos y listado simple: el camino mínimo de punta a punta para anotar
  un gasto o un ingreso y verlo anotado.
  - Modelo de datos inicial con las tres tablas (`usuarios`, `categorias`, `movimientos`), migración
    de EF Core y semilla de diez categorías —siete de gasto, tres de ingreso—. La pertenencia al
    usuario está en el modelo desde el día uno, aunque todavía haya un solo usuario sembrado.
  - `GET /api/categorias` — catálogo global, ordenado por tipo y nombre.
  - `POST /api/movimientos` — alta con validación de los cinco campos, `ProblemDetails` RFC 9457 con
    la extensión `errors` en los rechazos, y el tipo derivado de la categoría en el servidor.
  - `GET /api/movimientos/{id}` — lectura individual, restringida al propietario por el filtro global.
  - `GET /api/movimientos` — listado del propietario ordenado por `fecha DESC, id DESC`, con techo de
    500 filas y la señal `recortado` cuando aplica.
  - Frontend en React 19 + Vite: formulario de alta accesible por teclado, con la fecha de hoy
    propuesta y los motivos de rechazo junto a cada control; tabla de cinco columnas que se recarga
    tras un alta exitosa.
  - `CHANGELOG.md` (este archivo).

- **FEAT-001b** — Filtros del listado, edición y eliminación: lo que hace falta para que lo anotado
  se pueda corregir, borrar y encontrar.
  - `GET /api/movimientos` acepta tres filtros opcionales e independientes —`categoriaId`, `desde` y
    `hasta`—, que se resuelven en la base y no en memoria. El extremo superior del rango queda
    incluido, y un parámetro ausente significa "sin ese filtro", nunca "categoría cero" ni "fecha
    mínima".
  - `PUT /api/movimientos/{id}` — modificación de un movimiento propio con las mismas validaciones
    que el alta, compartidas en una sola implementación. El tipo no se puede cambiar: la categoría
    nueva tiene que ser del mismo tipo que el movimiento, y ese tipo sale de la fila persistida.
  - `DELETE /api/movimientos/{id}` — eliminación definitiva. El PRD descarta baja lógica, historial
    y papelera, así que la fila desaparece y no queda forma de recuperarla desde la aplicación.
  - Controles de filtro en el listado, con el mes en curso propuesto al abrir. Un rango inválido
    deja el listado anterior en pantalla en vez de vaciarlo.
  - Editar y eliminar desde cada fila. El borrado pide confirmación explícita en un diálogo que
    nombra el movimiento, y solo entonces llama al servidor.

- **FEAT-001c** — Resumen del mes con desglose por categoría: lo anotado deja de ser una lista y pasa
  a ser un número que se puede mirar.
  - `GET /api/resumen` — los tres totales del mes calendario en curso —ingresado, gastado y
    balance— más el desglose de los gastos por categoría, con una fila por categoría que tenga al
    menos un gasto. Una categoría sin gastos no aparece en cero: no aparece.
  - **El endpoint no acepta ningún parámetro, y es del contrato.** El período lo fija el servidor,
    así que el resumen es siempre del mes en curso pase lo que pase con los filtros del listado, y
    esa garantía no depende de que el cliente se porte bien.
  - Todo se agrega en SQL: la respuesta trae los tres totales y a lo sumo una fila por categoría,
    nunca la lista de movimientos, con 1000 movimientos o con 10.
  - El resumen aparece en la pantalla principal rotulado con su mes y año, se refresca tras un alta,
    una edición o una eliminación, y **no se mueve cuando el usuario filtra el listado**. El balance
    negativo se distingue por el signo menos en el texto, no solo por el color.
  - Si el resumen falla, el listado sigue en pie: son dos peticiones independientes y ofrece
    reintento por su cuenta.

### Changed

- **FEAT-001b** — El total del listado y la señal `recortado` se calculan sobre el universo **ya
  filtrado**. Contando todo lo del propietario, `recortado` mentiría con un filtro angosto.

### Security

- La cadena de conexión vive en user-secrets o en la variable de entorno `ConnectionStrings__Default`,
  nunca en `appsettings*.json`: la aplicación no arranca sin ella, en vez de caer más tarde con un
  error de conexión que invita a escribirla donde no va.
- La API escucha exclusivamente en `127.0.0.1`. Sin autenticación, el binding es el único control de
  acceso que existe.
- La pertenencia al usuario se aplica con un filtro global de EF Core en las lecturas, y en las
  escrituras sale siempre de `IUsuarioActual`, nunca del cuerpo de la petición.
- Las notas se renderizan como texto plano: no hay un solo `dangerouslySetInnerHTML` en el frontend.
- El rango de la fecha se valida contra lo que el tipo `DATE` de MySQL admite, para que una fecha
  fuera de rango sea un 400 con motivo y no un 500 del proveedor.
- Modificar y eliminar localizan la fila con una lectura sujeta al filtro global de propietario, no
  con un `ExecuteUpdate`/`ExecuteDelete` directo: sin esa lectura, la operación escribiría sobre
  filas ajenas y devolvería igual una respuesta exitosa.
- Un movimiento que no existe y uno que es de otro propietario devuelven el mismo 404, con el mismo
  título. Distinguirlos confirmaría la existencia de una fila ajena.
- El `PUT` asigna exactamente los cuatro campos del contrato. El propietario, el tipo, la moneda y la
  fecha de creación no se tocan aunque el cuerpo los traiga.
- Los filtros de fecha se parsean con formato exacto y cultura invariante, para que `01/02` no
  signifique cosas distintas según el entorno, y un rango invertido se rechaza antes de tocar la
  base. Los mensajes de error están redactados a mano: nunca se expone el detalle de la excepción
  del parseo.
- La prohibición de `dangerouslySetInnerHTML` se extiende al formulario de edición y al diálogo de
  confirmación, y pasó a estar fijada por una regla de ESLint para todo el proyecto.
- La agregación del resumen se apoya en el filtro global de propietario, sin `IgnoreQueryFilters()`.
  Es la fuga más difícil de ver de todas: no aparecería una fila de más, aparecería un número más
  grande. Está fijada por un test cuya mutación se registró, porque un test verde no prueba nada si
  nadie comprobó que puede ponerse rojo.
- Que la agregación ocurra **en SQL** es un control de seguridad y no solo de rendimiento: una
  agregación evaluada en memoria devuelve exactamente los mismos números mientras materializa la
  tabla entera en cada carga de pantalla. Lo distingue un test que observa el SQL emitido.

[Unreleased]: https://github.com/mauriciogonzales98/GestionGastos-daw/commits/main
