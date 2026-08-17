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

[Unreleased]: https://github.com/mauriciogonzales98/GestionGastos-daw/commits/main
