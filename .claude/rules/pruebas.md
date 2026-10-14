---
paths:
  - "backend/KadreeBank.UnitTests/**/*.cs"
  - "backend/KadreeBank.IntegrationTests/**/*.cs"
  - "frontend/src/**/*.spec.ts"
---

# Reglas de pruebas

## Backend (xUnit)

- Nombres de prueba en español, como frase: `Un_retiro_sin_fondos_no_persiste_nada`.
- **Unitarias:** servicios con los falsos de `Fakes.cs` (`InMemoryBank`, `FakeCurrentUser`,
  `FixedTimeProvider`). Nada de base de datos real ni de reloj real.
- **Integración:** PostgreSQL real con Testcontainers (`PostgresFixture`). Cada prueba crea sus
  propios datos con `CreateIsolatedClientAsync(...)` para no depender del orden ni de otras pruebas.
- Prueba también lo que **no** debe pasar: que un error no deje cambios guardados, que un cliente no
  vea cuentas ajenas (404), que el rol Banco no pueda operar (403).
- Comandos: `dotnet test KadreeBank.UnitTests` (rápido) y `dotnet test` (completo, requiere Docker).

## Frontend (Vitest)

- Archivos `*.spec.ts` junto al archivo que prueban.
- Prioriza utilidades, pipes y servicios con lógica (`core/utils`, `shared/pipes`).
- Comando: `npx ng test --watch=false`.

## Siempre

- **Nunca cambies lo que espera una prueba solo para que pase.** Si una prueba falla, primero
  averigua si el error está en el código o en la prueba, y explícalo.
- No borres ni marques como omitida (`Skip`) una prueba sin preguntar.
