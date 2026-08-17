# ADR-002: Tests de integración contra MySQL real

| Field | Value |
|-------|-------|
| Date | 2026-08-17 |
| Ticket | FEAT-001a |
| Status | Accepted |

## Context

NFR-03 del PRD exige que los montos se persistan en decimal exacto de 2 decimales, y AC-19 lo
verifica con 0.10 + 0.20 = 0.30. El PRD exige además fechas sin hora y sin conversión de zona
horaria, y restricciones de esquema (FK NOT NULL, UNIQUE sobre nombre y tipo de categoría).

Todo eso es comportamiento **del esquema**, no de la lógica de C#. La pregunta es contra qué base
corren los tests.

## Options considered

### Option 1: MySQL real, base `gestiongastos_test`
- **Pros:** es lo único que verifica la migración, el tipo real de la columna, las FK y los UNIQUE. Detecta el caso que motivó esta decisión: el default de Pomelo para `decimal` sin `HasPrecision` es `decimal(65,30)`, y un test de 0.10 + 0.20 **pasaría igual** contra ese tipo, dando NFR-03 por cumplido cuando no lo está. Solo leyendo `information_schema` de una base real se detecta.
- **Cons:** exige MySQL corriendo para pasar los gates. Los tests son más lentos y hay que limpiar el estado entre ellos.

### Option 2: proveedor en memoria de EF Core (o SQLite en memoria)
- **Pros:** corre en cualquier lado sin infraestructura, arranca instantáneo, aislamiento perfecto entre tests.
- **Cons:** el proveedor in-memory no es una base de datos relacional: no aplica tipos de columna, no valida FK ni UNIQUE, y no ejecuta las migraciones. AC-19 pasaría sin demostrar nada, porque estaría probando el `decimal` de C#, que nunca estuvo en duda. SQLite se acerca más pero tampoco tiene los tipos de MySQL.

### Option 3: Testcontainers
- **Pros:** MySQL real y desechable por corrida, reproducible, sin ensuciar la máquina.
- **Cons:** exige Docker, que no está declarado en el Stack ni instalado en el entorno. El Stack dice "MySQL 8.4.5 local", que es la opción 1.

## Decision

**Opción 1.** El riesgo que estos tests tienen que atrapar vive en el esquema, y una base en memoria
es ciega justamente ahí: daría verde sobre un `decimal(65,30)` que incumple NFR-03. Testcontainers
resolvería lo mismo con más aislamiento, pero introduce Docker como dependencia de entorno que el
Stack no declara y que hoy no está instalada.

## Consequences

- Los tests requieren MySQL corriendo con la base `gestiongastos_test`. Sin eso, el gate de tests falla — y es correcto que falle.
- La cadena de conexión de los tests llega por la variable de entorno `ConnectionStrings__Default`: con `dotnet test`, los user-secrets se resuelven contra el assembly de entrada, que es el proyecto de tests y no la API, así que apoyarse en ellos dejaría la credencial sin domicilio y empujaría a escribirla en `appsettings.Testing.json`, que `AGENTS.md` prohíbe.
- Afecta: `backend/GestionGastos.Api.Tests/Infra/BaseDeDatosFixture.cs`, `ApiFactory.cs`, `backend/db/README.md`.
- Se acepta que los tests son más lentos y que el entorno de CI, cuando exista, tendrá que levantar un MySQL.
- El usuario de MySQL que usa la aplicación está acotado al schema con permisos CRUD y no es root, para que una inyección no escale a control del servidor (R-05 del threat model).
