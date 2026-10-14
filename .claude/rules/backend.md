---
paths:
  - "backend/KadreeBank.Api/**/*.cs"
---

# Reglas del backend (.NET)

## Capas: qué va en cada una

| Capa | Hace | No hace |
|---|---|---|
| `Controllers/` | Recibe la petición, valida el DTO, aplica `[Authorize]`, devuelve `Ok(await servicio...)` | Lógica de negocio, `try/catch`, acceso a datos |
| `Services/` | Orquesta el caso de uso, valida lo que depende de datos, filtra por dueño, arma `ApiResponse<T>` | SQL o LINQ contra el `DbContext` |
| `Models/Entities/` | Reglas del dominio (saldo, montos, bloqueo de usuario) | Saber de EF o de HTTP |
| `Models/DAO/` | Todo el acceso a datos con EF Core | Reglas de negocio |
| `Helpers/` | Piezas técnicas: JWT, hash, calendario, usuario actual | — |

## Convenciones

- Cada servicio y DAO tiene su interfaz en `Interfaces/` y se registra en `Program.cs`.
- Dependencias por constructor con `?? throw new ArgumentNullException(nameof(x))`.
- Todo método async recibe `CancellationToken ct` y lo pasa hasta EF.
- Usa `TimeProvider` (inyectado) para la hora; nunca `DateTime.Now` ni `DateTime.UtcNow` directo.
- Usa `ICurrentUser` para saber quién hace la petición; no leas los claims a mano.
- Errores de negocio: lanza una excepción de `Exceptions/BankExceptions.cs`. Si creas un tipo nuevo,
  agrégalo en `Middleware/ApiExceptionHandler.cs` con su código HTTP y su `errorCode`.
- Los `errorCode` van en snake_case y en español (`saldo_insuficiente`, `no_encontrado`).
- Montos siempre `decimal`, nunca `double` ni `float`.
- Las propiedades de las entidades tienen `private set`; se cambian con métodos de la entidad.

## Documentación

- El proyecto genera documentación XML: todo miembro público lleva `/// <summary>` en español
  (si falta, el compilador avisa con CS1591).
- Cada acción del controlador lleva `[ProducesResponseType]` para cada código que puede devolver,
  y `<response code="...">` en el comentario, para que Swagger quede completo.

## Endpoints

- Por defecto todo exige sesión (`FallbackPolicy`). Usa `[AllowAnonymous]` solo si es público y
  `[Authorize(Policy = AuthPolicies.Banco)]` para lo que es solo del administrador.
- Las operaciones de dinero son solo para el rol Cliente; el rol Banco consulta, no opera.
