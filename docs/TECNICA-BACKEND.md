# Kadree Bank: documentación técnica del backend

API REST en **.NET 9 / ASP.NET Core** con **EF Core 9 + Npgsql** sobre **PostgreSQL 17** y autenticación **JWT**. Para la instalación y ejecución, ver el [README](../README.md).

---

## 1. Arquitectura

Es un **solo proyecto web organizado por carpetas** (estilo ApiInscripcionMaterias), con el flujo:

```
HTTP → Controller → Service → DAO → KadreeBankDbContext → PostgreSQL
                       │
                       └→ Entity (reglas de negocio: saldo, montos, bloqueo de usuario)
```

| Capa | Carpeta | Hace | No hace |
|---|---|---|---|
| Controller | `Controllers/` | Recibe la petición, valida el DTO con DataAnnotations (`[ApiController]` → 400 automático), aplica `[Authorize]` y devuelve `ApiResponse` con su código HTTP | Lógica de negocio, `try/catch` |
| Service | `Services/` | Orquesta el caso de uso, valida reglas que dependen de datos, aplica autorización por dueño y arma el `ApiResponse` | Consultas SQL |
| Entity | `Models/Entities/` | Reglas del dominio: `Account.Deposit/Withdraw`, `User.RegisterFailedAttempt`, `RefreshToken.Revoke` | Saber de EF o HTTP |
| DAO | `Models/DAO/` | Todo el acceso a datos con EF: bloqueo de fila, transacciones, proyecciones y reportes en LINQ | Reglas de negocio |
| Helpers | `Helpers/` | Piezas técnicas: JWT, hash de contraseña, reglas de contraseña, usuario actual, calendario, seeder | — |
| Middleware | `Middleware/` | `ApiExceptionHandler`: convierte cualquier excepción en `ApiResponse` | — |

### Archivos principales

| Archivo | Responsabilidad |
|---|---|
| `Program.cs` | Valida secretos, registra dependencias (DI), JWT, autorización, rate limiting, CORS, Swagger, health check y pipeline; ejecuta `DemoUserSeeder` |
| `Controllers/AuthController.cs` | `api/auth`: login, refresh, logout, register, username-availability, me. Escribe y borra la cookie `kb_refresh` |
| `Controllers/AccountsController.cs` | `api/accounts` (`[Authorize]`): cuentas, movimientos, extractos, consignaciones y retiros |
| `Controllers/ReportsController.cs` | `api/reports` (`[Authorize(Policy = "Banco")]`) |
| `Controllers/CitiesController.cs` | `api/cities` (`[AllowAnonymous]`, se usa en el registro) |
| `Services/AuthService.cs` | Login con bloqueo, emisión y rotación de tokens, registro con validaciones, disponibilidad de usuario |
| `Services/AccountService.cs` | Consultas filtradas por dueño, operaciones (solo rol Cliente) y extractos |
| `Services/ReportService.cs`, `CityService.cs` | Reportes y catálogo |
| `Models/DAO/UserDao.cs` | Usuarios, refresh tokens y registro transaccional (cliente + cuenta + usuario) |
| `Models/DAO/AccountDao.cs` | Lecturas y operaciones con `SELECT … FOR UPDATE` |
| `Models/DAO/ReportDao.cs`, `CityDao.cs` | Reportes en LINQ y ciudades |
| `Helpers/JwtTokenFactory.cs` | Crea el access token (HS256), genera y hashea el refresh token y entrega los `TokenValidationParameters` |
| `Helpers/PinHasher.cs` | Hash de la contraseña: HMAC-SHA256 con pepper, luego PBKDF2-SHA256; incluye `VerifyDummy` |
| `Helpers/PinRules.cs` | Reglas de una contraseña nueva |
| `Helpers/CurrentUser.cs` | Implementa `ICurrentUser` leyendo los claims del token |
| `Helpers/SecuritySettings.cs` | `JwtSettings`, `AuthSettings` y su validador al arrancar |
| `Helpers/DemoUserSeeder.cs` | Asigna `Auth:DemoPin` a los usuarios con `pin_hash = 'PENDIENTE'` |
| `Helpers/BankCalendar.cs` | Delimita meses en la zona `Bank:TimeZone` (America/Bogota) |
| `Helpers/AuthPolicies.cs`, `RateLimitPolicies.cs` | Nombres de la política `Banco` y del limitador `auth` |

---

## 2. Contrato de respuesta

Todas las respuestas, de éxito o de error, usan `ApiResponse<T>` (`Models/DTOs/Responses/ApiResponse.cs`):

```json
{
  "success": false,
  "message": "La cuenta AH-1006 no tiene saldo suficiente: ...",
  "errorCode": "saldo_insuficiente",
  "errors": [],
  "validationErrors": {},
  "data": null
}
```

`validationErrors` se llena en los 400 con los mensajes por campo, en camelCase (`{ "documentNumber": ["..."] }`). El frontend los muestra junto al campo correspondiente.

### Códigos de error

| HTTP | `errorCode` | Excepción u origen | Cuándo |
|---|---|---|---|
| 400 | `validacion` | `RequestValidationException`, DataAnnotations | Datos con formato inválido; detalle por campo |
| 400 | `solicitud_invalida` | `BadHttpRequestException` | JSON mal formado |
| 401 | `credenciales_invalidas` | `AuthenticationFailedException` | Usuario o contraseña incorrectos (mensaje genérico) |
| 401 | `sesion_invalida` | `InvalidSessionException` | Refresh token ausente, vencido, revocado o reutilizado |
| 401 | `sesion_expirada` / `no_autenticado` | Evento `OnChallenge` de JwtBearer | Access token vencido o ausente |
| 403 | `sin_permiso` | `ForbiddenException`, `OnForbidden` | Rol sin permiso (cliente en reportes, Banco al operar) |
| 404 | `no_encontrado` | `NotFoundException` | Cuenta inexistente **o ajena**: no se revela que exista |
| 409 | `documento_registrado`, `usuario_no_disponible` | `ConflictException` | Registro duplicado (incluye `validationErrors` del campo) |
| 409 | `operacion_concurrente` | `ConcurrencyConflictException` | No se obtuvo el bloqueo de la cuenta en 5 s |
| 422 | `saldo_insuficiente`, `monto_invalido`, `tipo_cuenta_no_permitido` | `BusinessRuleException` | Regla de negocio |
| 423 | `acceso_bloqueado` | `AccountLockedException` | Usuario bloqueado por intentos fallidos |
| 429 | `demasiados_intentos` | Rate limiter | Más de 10 peticiones de autenticación por minuto desde una IP |
| 500 | `error_interno` | Cualquier otra | Mensaje genérico; el detalle va al log |

---

## 3. Seguridad

### 3.1 Flujo de sesión

```
POST /api/auth/login {identifier, pin}
  ← 200 { accessToken (JWT, 15 min), accessTokenExpiresAtUtc, user }
  ← Set-Cookie: kb_refresh=<opaco>; HttpOnly; Secure; SameSite=Strict; Path=/api/auth; 7 días

GET /api/accounts   Authorization: Bearer <accessToken>

(access token vencido → 401 sesion_expirada)
POST /api/auth/refresh   (el navegador envía la cookie)
  ← 200 nuevo accessToken + nueva cookie (rotación)

POST /api/auth/logout   → revoca la familia y borra la cookie
```

- **Access token** (JWT HS256, 15 minutos). Claims:
  - `sub`: id del usuario.
  - `unique_name`: usuario.
  - `name`: nombre completo.
  - `role`: `Cliente` o `Banco`.
  - `customer_id`: solo en los clientes.
  - `jti`.

  Configuración: `MapInboundClaims = false`, `NameClaimType = "name"`, `RoleClaimType = "role"` y `ClockSkew` de 30 s. Se valida el emisor (`kadree-bank-api`), la audiencia (`kadree-bank-web`), la firma y la vigencia.
- **Refresh token**:
  - Son 32 bytes aleatorios en Base64Url, con vigencia de 7 días.
  - En la tabla `refresh_tokens` se guarda **solo su SHA-256**.
  - Viaja únicamente en la cookie HttpOnly, así que JavaScript no puede leerlo. La cookie tiene `Path=/api/auth`, de modo que no se envía al resto de la API.
- **Rotación con familias**: cada refresh revoca el token usado, guarda `replaced_by_hash` y emite uno nuevo de la misma `family_id`. Si alguien presenta un token **ya revocado** (posible robo), se revoca **toda la familia** y la sesión termina en todos lados.
- **El frontend guarda el access token solo en memoria.** Al recargar la página, lo recupera con `/api/auth/refresh`.
- **CSRF**: la cookie es `SameSite=Strict` y solo sirve en `/api/auth/refresh` y `/api/auth/logout`. Las operaciones de dinero exigen el header `Authorization`, que un sitio externo no puede enviar.

### 3.2 Contraseñas de 4 dígitos

Una contraseña de 4 dígitos tiene solo 10.000 combinaciones, así que la defensa no puede depender del hash solamente. Se combinan:

1. **Pepper**: `HMAC-SHA256(Auth:PinPepper, pin)`. El pepper vive fuera de la base (user-secrets o variables de entorno). Un volcado de la base sin el pepper no permite probar las 10.000 combinaciones.
2. **PBKDF2-SHA256** con **600.000 iteraciones** y una sal aleatoria de 16 bytes. Formato guardado: `pbkdf2-sha256$600000$<sal>$<hash>`. La comparación es de tiempo constante (`CryptographicOperations.FixedTimeEquals`).
3. **Bloqueo**: 3 intentos fallidos bloquean el usuario **15 minutos** (`Auth:MaxFailedAttempts`, `Auth:LockoutMinutes`). Un ingreso exitoso reinicia el contador. El desbloqueo es automático.
4. **Rate limiting** por IP en los endpoints de autenticación: ventana fija de 10 por minuto (`RateLimit:AuthPermitLimit`).
5. **Sin enumeración de usuarios**:
   - Mismo mensaje y código (401) si el usuario no existe o la contraseña es incorrecta.
   - `VerifyDummy` hace el mismo trabajo de PBKDF2 cuando el usuario no existe, para que el tiempo de respuesta no lo delate.
   - Ante 3 fallos con un usuario inexistente nunca se responde 423, porque eso confirmaría qué usuarios existen. Solo el usuario real se bloquea.
6. **Reglas de contraseña nueva** (`PinRules`): exactamente 4 dígitos, no todos iguales, sin secuencia ascendente ni descendente y distinta de los últimos 4 dígitos del documento.

PBKDF2 se eligió en lugar de Argon2 porque viene en .NET (`Rfc2898DeriveBytes.Pbkdf2`) y no requiere paquetes de terceros. OWASP lo recomienda con 600.000 iteraciones para SHA-256.

### 3.3 Autorización

- **`FallbackPolicy`** exige un usuario autenticado en **todo** endpoint que no tenga `[AllowAnonymous]`. Un endpoint nuevo nace protegido.
- **Por rol**: la política `Banco` (`RequireRole("Banco")`) protege los reportes.
- **Por dueño** (`AccountService`):
  - `OwnerFilter` es `null` para el rol Banco (ve todo) y el `customer_id` del token para un cliente.
  - Todas las consultas de cuentas, movimientos y extractos filtran por él, así que una cuenta ajena responde **404**.
- **Operaciones**: solo el rol Cliente, y solo sobre sus cuentas. El rol Banco recibe **403** `sin_permiso`. Cada movimiento guarda `created_by_user_id` para auditoría.

### 3.4 Configuración y secretos

| Clave | Fuente en desarrollo | Fuente en Docker |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | user-secrets | `ConnectionStrings__DefaultConnection` (armada en compose) |
| `Jwt:Key` (≥ 32 bytes) | user-secrets | `JWT_KEY` → `Jwt__Key` |
| `Auth:PinPepper` (≥ 32 caracteres) | user-secrets | `PIN_PEPPER` → `Auth__PinPepper` |
| `Auth:DemoPin` | user-secrets | `DEMO_PIN` → `Auth__DemoPin` |
| `Auth:RefreshCookieSecure` | `true` (los navegadores aceptan cookies `Secure` en `http://localhost`) | `true` |
| `ReverseProxy:Enabled` | `false` | `true`: lee `X-Forwarded-For` de Nginx para el rate limiting por IP |

`SecuritySettingsValidator.Validate` detiene el arranque si falta un secreto o es demasiado corto. `appsettings.json` deja `Jwt:Key` y `Auth:PinPepper` vacíos a propósito.

---

## 4. Endpoints

### Autenticación (`/api/auth`, limitador `auth`)

| Método y ruta | Cuerpo / parámetros | Respuesta |
|---|---|---|
| `POST /login` | `LoginRequestDto { identifier, pin }`. `identifier` es el usuario (se pasa a minúsculas) o el documento (se quitan puntos y espacios) | 200 `LoginResultDto` + cookie · 401 · 423 · 429 |
| `POST /refresh` | Cookie `kb_refresh` | 200 `LoginResultDto` + nueva cookie · 401 `sesion_invalida` (y se borra la cookie) |
| `POST /logout` | Cookie (opcional) | 200; revoca la familia y borra la cookie |
| `POST /register` | `RegisterRequestDto` (abajo) | 201 `RegisterResultDto` · 400 · 409 |
| `GET /username-availability?username=` | — | 200 `UsernameAvailabilityDto { username, available, message }` |
| `GET /me` | Bearer | 200 `AuthUserDto { username, fullName, role }` |

`RegisterRequestDto`:

| Campo | Regla |
|---|---|
| `customerType` | `Natural` o `Juridica` |
| `documentType` | `CC`, `CE` o `PA` para Natural; `NIT` para Juridica (también lo exige un CHECK en la base) |
| `documentNumber` | CC/CE: 6 a 10 dígitos · PA: 6 a 12 alfanuméricos (se pasan a mayúsculas) · NIT: `^\d{9}-\d$` |
| `fullName` | 3 a 150 caracteres |
| `email` | correo válido, máximo 150 |
| `phone` | `^3\d{9}$` |
| `cityId` | ciudad existente; será la ciudad de origen |
| `username` | `^[a-z0-9.]{6,20}$` con al menos una letra |
| `pin`, `pinConfirmation` | iguales y conformes a `PinRules` |
| `acceptTerms` | `true` |

`UserDao.RegisterAsync` hace todo en **una transacción**: inserta el cliente, toma el número de la secuencia `account_number_seq` (desde 5001), crea la cuenta (`AH-5001` o `CC-5001`) con saldo 0 y crea el usuario. Una carrera que viole la unicidad (código PostgreSQL `23505`) se traduce a `ConflictException`.

### Cuentas (`/api/accounts`, `[Authorize]`)

| Método y ruta | Descripción |
|---|---|
| `GET /` | `AccountSummaryDto[]`: propias, o todas si es Banco |
| `GET /{number}` | `AccountSummaryDto` |
| `GET /{number}/movements?limit=20` | `MovementDto[]`, del más reciente al más antiguo (`limit` entre 1 y 100) |
| `GET /{number}/statements/{year}/{month}` | `StatementDto`; meses futuros → 400 |
| `POST /{number}/deposits` | `OperationRequestDto { amount, cityId, description? }` → 201 `OperationResultDto` |
| `POST /{number}/withdrawals` | Igual → 201, o 422 `saldo_insuficiente` |

`amount`: entre 0,01 y 1.000.000.000.000, con máximo 2 decimales (`[Range]` en cultura invariante y `[MaxDecimalPlaces]`).

### Paginación de los reportes

Los tres reportes se paginan **en la base de datos**:

- Parámetros `page` (desde 1) y `pageSize` (1 a 100; por defecto 20). Si están fuera de rango, la API responde 400 `validacion`. Los valida `Helpers/Paging.cs`.
- `data` es un `PagedResult<T>` con `items`, `page`, `pageSize`, `totalItems` y `totalPages`. Una página fuera de rango llega con `items` vacío y los totales.
- El DAO hace un `COUNT` para el total y un `SELECT … ORDER BY … OFFSET … LIMIT`. Cada orden termina en un id, para que la paginación sea estable y no repita ni salte filas.
- **Transacciones por sede** se pagina por **bloque cliente-sede** y devuelve `BranchTransactionsPageDto`, que agrega `totalTransactions`:
  1. Cuenta los bloques y las transacciones.
  2. Elige los bloques de la página con `GROUP BY`, `ORDER BY` total y `OFFSET/LIMIT`.
  3. Trae solo las transacciones de esos clientes y sedes.

  Así un cliente nunca queda partido entre dos páginas. Una prueba de integración recorre todas las páginas y verifica que reproducen el reporte completo, en el mismo orden.

### Ciudades y reportes

| Método y ruta | Acceso | Descripción |
|---|---|---|
| `GET /api/cities` | Público | `CityDto[]` |
| `GET /api/reports/transactions-by-branch?year=&month=&cityId=&type=` | Banco | `BranchTransactionDto[]`: **una fila por transacción** del mes, con su **sede** (ciudad donde se hizo), cliente, `movementId`, `accountNumber`, `type` (`Consignacion` o `Retiro`), `amount`, `description` y `createdAtUtc`. `customerTransactionCount` repite en cada fila el total del cliente en la sede y define el orden, de mayor a menor (desempate por sede y nombre); dentro de cada cliente, orden cronológico. `cityId` es opcional (sin él, todas las sedes; si no existe, 400) y `type` también (`Consignacion` o `Retiro`; otro valor, 400). Solo lista clientes con al menos una transacción que cumpla los filtros. En el DAO es un único `GROUP BY` en LINQ. Los tres reportes incluyen `firstTransactionAtUtc` y `lastTransactionAtUtc` (`MIN`/`MAX` de `created_at` en el período; null en el reporte 1 si el cliente no tuvo transacciones) |
| `GET /api/reports/transactions-by-customer?year=&month=` | Banco | `CustomerTransactionCountDto[]`: todos los clientes con su conteo del mes, de mayor a menor |
| `GET /api/reports/out-of-city-withdrawals?minTotal=1000000&from=&to=` | Banco | `OutOfCityWithdrawalsDto[]`: total retirado fuera de la ciudad de origen de cada cuenta, **estrictamente mayor** que `minTotal`; `from` y `to` (`yyyy-MM-dd`) son opcionales |

---

## 5. Base de datos

El esquema está en `db/01_schema.sql` y los datos de prueba en `db/02_seed.sql`. **No hay migraciones de EF**: el `DbContext` solo mapea entidades a tablas en snake_case. Los mismos scripts se usan en Docker (`/docker-entrypoint-initdb.d`) y en las pruebas de integración.

| Tabla | Claves y restricciones relevantes |
|---|---|
| `cities` | Catálogo |
| `customers` | `document_number` UNIQUE; `ck_customers_type`; `ck_customers_document_type` (NATURAL: CC/CE/PA; JURIDICA: NIT); `UNIQUE (id, customer_type)` para la FK compuesta |
| `accounts` | `number` UNIQUE; `balance NUMERIC(18,2) CHECK (balance >= 0)`; FK compuesta `(customer_id, customer_type)`; CHECK ahorros ↔ NATURAL y corriente ↔ JURIDICA |
| `movements` | Inmutables (un trigger impide UPDATE y DELETE); `balance_after`; `created_by_user_id` → `users`; índices `(account_id, created_at)`, `created_at` y un índice parcial de retiros |
| `users` | `username` UNIQUE con CHECK de formato; `customer_id` UNIQUE (un usuario por cliente); rol `CLIENTE` o `BANCO`; un CHECK exige `customer_id` para CLIENTE y NULL para BANCO; `failed_attempts`, `locked_until` |
| `uq_users_single_admin` | Índice único parcial `ON users ((true)) WHERE role = 'BANCO'`: garantiza **un único administrador** |
| `refresh_tokens` | `token_hash char(64)` UNIQUE; `family_id uuid`; `expires_at`, `revoked_at`, `replaced_by_hash` |
| `account_number_seq` | Secuencia desde 5001 para las cuentas que se abren en el registro |

**Fechas**: todas son `timestamptz` en UTC. Los meses de los extractos y del reporte 1 se delimitan en `America/Bogota` (`BankCalendar`).

---

## 6. Concurrencia

Cada consignación o retiro se ejecuta en una transacción READ COMMITTED (`AccountDao.ExecuteInTransactionAsync`):

```
SET LOCAL lock_timeout = '5000ms'
SELECT * FROM accounts WHERE number = $1 FOR UPDATE    -- FromSql, parametrizado
-- Account.Withdraw valida amount <= balance
UPDATE accounts SET balance = ...; INSERT INTO movements (...)   -- SaveChangesAsync
COMMIT
```

- Las operaciones sobre la misma cuenta se serializan; las de cuentas distintas corren en paralelo.
- Si el bloqueo no se obtiene en 5 s, la API responde 409 `operacion_concurrente` y es seguro reintentar.
- El `CHECK (balance >= 0)` es una defensa en profundidad: si se viola, se traduce a `saldo_insuficiente`.
- Se eligió el bloqueo pesimista y no el optimista porque las cuentas corrientes de empresas reciben muchas operaciones simultáneas: con control optimista habría muchos reintentos.

Lo prueban `ConcurrencyTests` (unitarias) y `ConcurrencyAgainstPostgresTests` (60 retiros simultáneos contra PostgreSQL real).

---

## 7. Pruebas

| Proyecto | Qué cubre | Cómo corre |
|---|---|---|
| `KadreeBank.UnitTests` (73) | Entidades, servicios con DAO falsos (`Fakes.cs`: `FakeCurrentUser`, `FixedTimeProvider`), validación de DTOs, calendario, concurrencia, `PinHasher`, `PinRules`, `JwtTokenFactory`, bloqueo, rotación y reutilización de refresh tokens, y autorización por dueño y por rol | `dotnet test KadreeBank.UnitTests` |
| `KadreeBank.IntegrationTests` (18) | PostgreSQL desechable con **Testcontainers** (`PostgresFixture`) + `WebApplicationFactory`: concurrencia real, CHECK, inmutabilidad, reportes contra la semilla, extracto y HTTP (401, 404 de cuenta ajena, 403, 422, 423, registro con 201 y 409, y luego login) | `dotnet test` (requiere Docker; sin Docker se omiten) |

`PostgresFixture` fija claves de prueba por variables de entorno, desactiva `RefreshCookieSecure` y sube el límite del rate limiter. `CreateIsolatedClientAsync(balance)` crea un cliente, una cuenta y un usuario propios para cada prueba (con el nombre «Cliente de prueba», que los tests de reportes excluyen), así las pruebas no dependen del orden.

---

## 8. Cómo extender

**Agregar un endpoint**:

1. Crea el DTO en `Models/DTOs/<Área>/` con DataAnnotations.
2. Agrega el método en la interfaz del servicio y su implementación, que devuelve un `ApiResponse<T>`.
3. Si necesita datos, agrégalo en la interfaz del DAO y en su implementación con EF y `AsNoTracking()`.
4. Agrega la acción en el controlador, con `[ProducesResponseType]` y comentarios XML para Swagger. Queda protegida por la `FallbackPolicy`; usa `[AllowAnonymous]` o `[Authorize(Policy = ...)]` según el caso.
5. Para errores de negocio, lanza una excepción de `Exceptions/`. Si es un tipo nuevo, mapéalo en `ApiExceptionHandler`.
6. Agrega pruebas unitarias del servicio y, si toca SQL, una de integración.

**Cambiar el esquema**: edita `db/01_schema.sql` (y `02_seed.sql` si aplica), actualiza el mapeo en `KadreeBankDbContext` y recrea la base con `docker compose down -v`. Las pruebas de integración aplican los scripts en cada ejecución.
