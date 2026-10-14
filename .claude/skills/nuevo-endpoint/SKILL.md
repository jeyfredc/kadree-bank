---
name: nuevo-endpoint
description: Agrega un endpoint nuevo a la API de Kadree Bank, de punta a punta (DTO, servicio, DAO, controlador, pruebas y modelo del frontend). Úsala cuando pidan crear o exponer una nueva operación o consulta en la API.
---

# Agregar un endpoint a la API

Antes de escribir código, muestra un plan corto: ruta HTTP, quién puede usarla (Cliente, Banco o
pública), DTOs de entrada y salida, y qué errores puede devolver. Espera confirmación.

## Pasos

1. **DTO** en `backend/KadreeBank.Api/Models/DTOs/<Área>/`, con DataAnnotations para validar
   formato y `/// <summary>` en cada propiedad.
2. **Servicio:** agrega el método a la interfaz en `Interfaces/I<Área>Service.cs` y su
   implementación en `Services/`. Devuelve `ApiResponse<T>.Ok(datos, "mensaje en español")`.
   - Si el dato es de un cliente, filtra por dueño con `ICurrentUser` (cuenta ajena → 404).
   - Si es una regla de negocio, lanza una excepción de `Exceptions/`.
3. **DAO** (si necesita datos): método en la interfaz `I<Área>Dao` y en `Models/DAO/`, con
   proyección a DTO y `AsNoTracking()`. Si mueve dinero, usa `ExecuteInTransactionAsync` y
   `GetByNumberForUpdateAsync`.
4. **Controlador:** acción con `[ProducesResponseType]` por cada código posible y comentario XML con
   `<response code>`. Seguridad: por defecto exige sesión; usa `[AllowAnonymous]` o
   `[Authorize(Policy = AuthPolicies.Banco)]` según el caso.
5. **Excepción nueva** (solo si hace falta): créala en `Exceptions/BankExceptions.cs` y mapéala en
   `Middleware/ApiExceptionHandler.cs` con su código HTTP y su `errorCode` en snake_case.
6. **Pruebas:**
   - Unitarias del servicio en `KadreeBank.UnitTests`, con `InMemoryBank` y `FakeCurrentUser`:
     el caso feliz, la validación y la autorización (dueño y rol).
   - Si toca SQL o HTTP, una de integración en `KadreeBank.IntegrationTests`.
7. **Frontend:** agrega la interfaz del DTO en `frontend/src/app/core/models/` con los mismos
   nombres en camelCase, y el método en el servicio de `core/services/` con `map(unwrap)`.
8. **Documentación:** agrega el endpoint a la tabla de endpoints de `docs/TECNICA-BACKEND.md`.

## Verificación

```bash
cd backend && dotnet build && dotnet test KadreeBank.UnitTests
cd ../frontend && npx ng build
```

Si hay Docker disponible, corre también `dotnet test` para las pruebas de integración.
Al terminar, resume qué archivos cambiaron y prueba el endpoint en Swagger (`/swagger`).
