---
paths:
  - "db/**/*.sql"
  - "backend/KadreeBank.Api/Models/DAO/**/*.cs"
  - "backend/KadreeBank.Api/Data/**/*.cs"
---

# Reglas de base de datos (PostgreSQL + EF Core)

- **No hay migraciones de EF.** El esquema vive en `db/01_schema.sql` y los datos de prueba en
  `db/02_seed.sql`. Para cambiar el esquema usa la skill `cambiar-esquema-bd`.
- Tablas y columnas en `snake_case`; el mapeo está en `KadreeBankDbContext`.
- Fechas en `timestamptz` y en UTC.
- Montos en `numeric(18,2)`.
- Las restricciones (`CHECK`, `UNIQUE`, FK) se nombran con prefijo: `ck_`, `uq_`, `fk_`, `ix_`.
- La tabla `movements` es **inmutable**: un trigger impide `UPDATE` y `DELETE`. No lo quites.

## Consultas

- Lecturas: proyecciones a DTO con `AsNoTracking()`; trae solo las columnas necesarias.
- Nunca armes SQL concatenando texto. Si necesitas SQL directo, usa `FromSql` o `ExecuteSql`
  con interpolación (EF la convierte en parámetros).
- Listados grandes siempre paginados (`Helpers/Paging.cs`).
- Evita el N+1: no hagas consultas dentro de un ciclo.

## Escrituras de dinero

- Siempre dentro de `ExecuteInTransactionAsync` y con la cuenta obtenida por
  `GetByNumberForUpdateAsync` (`SELECT ... FOR UPDATE`).
- El `CHECK (balance >= 0)` es una segunda defensa; la regla principal está en `Account.Withdraw`.
- Si cambias algo en este flujo, corre `ConcurrencyTests` y las pruebas de integración.
