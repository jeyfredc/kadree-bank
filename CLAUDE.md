# Kadree Bank

Banca en línea: cuentas de ahorros (personas) y corrientes (empresas), consignaciones, retiros,
extractos y dos reportes para el rol Banco. Monorepo con API .NET, frontend Angular y PostgreSQL.

## Stack

- **Backend:** .NET 9, ASP.NET Core, EF Core 9 + Npgsql, JWT, xUnit (`backend/`)
- **Frontend:** Angular 22 con **NgModule** (`standalone: false`), signals, zoneless, Vitest (`frontend/`)
- **Base de datos:** PostgreSQL 17. Esquema en `db/01_schema.sql`, datos de prueba en `db/02_seed.sql`
- **Entrega:** Docker Compose (`docker-compose.yml`)

## Comandos

```bash
# Todo con Docker (requiere .env, copiado de .env.example)
docker compose up -d --build

# Backend
cd backend && dotnet build
dotnet test KadreeBank.UnitTests          # rápidas, sin Docker
dotnet test                               # incluye integración (Testcontainers, requiere Docker)

# Frontend (Node 22.22+ o 24.15+; Node 23 no sirve)
cd frontend && npm ci
npx ng test --watch=false
npx ng build
npx ng serve                              # http://localhost:4200, proxy /api → :8080
npx prettier --write <archivos que cambiaste>
```

## Arquitectura

Backend en un solo proyecto organizado por carpetas:

```
HTTP → Controller → Service → DAO → KadreeBankDbContext → PostgreSQL
                       └→ Entity (reglas del dinero: Deposit / Withdraw)
```

Frontend: `core/` (modelos, servicios, interceptor, guardas, utilidades), `shared/` (SharedModule)
y `features/` (auth, banking, reports), cada una como módulo con carga diferida.

Antes de un cambio grande, lee la guía técnica de la parte que vas a tocar:
`docs/TECNICA-BACKEND.md` o `docs/TECNICA-FRONTEND.md`.

## Reglas de oro

1. **El saldo nunca puede quedar negativo.** Solo `Account.Deposit` y `Account.Withdraw` cambian el
   saldo, y siempre dentro de `AccountDao.ExecuteInTransactionAsync` con la cuenta bloqueada
   (`GetByNumberForUpdateAsync`, `FOR UPDATE`). No cambies este patrón sin preguntar.
2. **Toda respuesta de la API es `ApiResponse<T>`**, también los errores. Los controladores no usan
   `try/catch`: se lanza una excepción de `Exceptions/` y `ApiExceptionHandler` la traduce.
3. **Contrato backend ↔ frontend:** si cambias un DTO de C#, actualiza su interfaz en
   `frontend/src/app/core/models/` en el mismo cambio.
4. **Un cliente solo ve lo suyo.** Una cuenta ajena responde 404 (no 403) para no revelar que existe.
5. **Fechas en UTC** en la base y en la API; se muestran en hora de Bogotá (`BankCalendar`, pipe `bankDate`).
6. **Sin secretos en el código.** Las claves salen de `.env` / variables de entorno. Nunca leas,
   muestres ni copies el contenido de `.env`.
7. **Textos para el usuario en español**, en el mismo tono de los mensajes que ya existen.
8. El frontend usa **NgModule a propósito**. No migres a standalone salvo que se pida.

## Forma de trabajar

- Para cambios de más de un archivo, muestra primero un plan corto y espera confirmación.
- Cambios pequeños y enfocados. No refactorices lo que no hace parte de la tarea.
- Todo cambio de comportamiento lleva prueba: unitaria y, si toca SQL o HTTP, de integración.
- Antes de dar algo por terminado, compila y corre las pruebas de la parte que tocaste.
- Si una instrucción de este archivo choca con lo que pide la tarea, pregunta antes de seguir.

Las reglas detalladas por tipo de archivo están en `.claude/rules/` y los procedimientos paso a
paso en `.claude/skills/`.
