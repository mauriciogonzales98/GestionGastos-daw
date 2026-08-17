# Base de datos — GestionGastos

MySQL 8.4 local, puerto 3306. Dos schemas: `gestiongastos` (la aplicación) y `gestiongastos_test`
(los tests de integración, que corren contra MySQL real por
[ADR-002](../../docs/adr/adr-002-tests-contra-mysql-real.md)).

> **Ninguna contraseña se escribe en este repositorio.** Ni acá, ni en `appsettings*.json`, ni en un
> `.sql`. Donde este documento dice `<CONTRASEÑA>` va una que elijas vos y que solo exista en tu
> máquina.

## 1. Crear los schemas y el usuario acotado

El usuario de la aplicación **no es root** y solo tiene CRUD sobre sus dos schemas: así una
inyección de SQL no se convierte en control del servidor (mitigación R-05 del threat model).

```sql
CREATE DATABASE IF NOT EXISTS gestiongastos      CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE DATABASE IF NOT EXISTS gestiongastos_test CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

CREATE USER IF NOT EXISTS 'gestiongastos'@'localhost' IDENTIFIED BY '<CONTRASEÑA>';

GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER, REFERENCES
  ON gestiongastos.*      TO 'gestiongastos'@'localhost';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER, REFERENCES
  ON gestiongastos_test.* TO 'gestiongastos'@'localhost';

FLUSH PRIVILEGES;
```

Los permisos de DDL (`CREATE`, `DROP`, `ALTER`, `INDEX`, `REFERENCES`) están porque las migraciones
las aplica este mismo usuario en desarrollo. En un despliegue real se separan: uno que migra y otro
que solo hace CRUD.

## 2. Cargar la cadena de conexión

**De la aplicación — user-secrets, nunca `appsettings`:**

```bash
cd backend/GestionGastos.Api
dotnet user-secrets set "ConnectionStrings:Default" \
  "Server=127.0.0.1;Port=3306;Database=gestiongastos;User ID=gestiongastos;Password=<CONTRASEÑA>;"
```

Sin ella la API no arranca: falla con un mensaje explícito en vez de degradarse a un modo sin base.

**De los tests — variable de entorno:**

```bash
export ConnectionStrings__Default="Server=127.0.0.1;Port=3306;Database=gestiongastos_test;User ID=gestiongastos;Password=<CONTRASEÑA>;"
```

Va por variable de entorno y no por user-secrets porque con `dotnet test` los secretos se resuelven
contra el assembly de entrada, que es el proyecto de tests y no la API (ADR-002, mitigación R-11).

## 3. Aplicar las migraciones

```bash
cd backend/GestionGastos.Api
dotnet ef database update
```

La migración inicial crea `usuarios`, `categorias` y `movimientos`, y siembra las 10 categorías
predefinidas y el usuario de desarrollo `dev@gestiongastos.local` (un TLD reservado: no puede
corresponder a nadie real).

## 4. Si una migración falla a mitad

No hay camino de código para eso: es la migración inicial y la recuperación es operativa. Se borra
el schema y se recrea.

```sql
DROP DATABASE gestiongastos;
CREATE DATABASE gestiongastos CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
```

Después, `dotnet ef database update` otra vez. Lo mismo vale para `gestiongastos_test`, que además
se puede borrar en cualquier momento: los tests la recrean.
