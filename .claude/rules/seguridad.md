---
paths:
  - "backend/KadreeBank.Api/Helpers/**/*.cs"
  - "backend/KadreeBank.Api/Controllers/AuthController.cs"
  - "backend/KadreeBank.Api/Services/AuthService.cs"
  - "backend/KadreeBank.Api/Program.cs"
  - "frontend/src/app/core/services/auth.service.ts"
  - "frontend/src/app/core/interceptors/**/*.ts"
  - "frontend/src/app/core/guards/**/*.ts"
---

# Reglas de seguridad (sesión, contraseñas, permisos)

Esta parte es delicada. **Antes de cambiar cualquier cosa aquí, explica el cambio y espera confirmación.**

- **Contraseñas (PIN de 4 dígitos):** se guardan con HMAC-SHA256 + pepper y luego PBKDF2
  (`PinHasher`). Nunca las guardes, registres en logs ni las devuelvas en una respuesta.
- **Access token:** JWT de 15 minutos, en memoria del frontend. No lo guardes en `localStorage`.
- **Refresh token:** viaja solo en la cookie `kb_refresh` (HttpOnly, Secure, SameSite=Strict,
  Path=/api/auth). En la base solo se guarda su hash. Se rota en cada uso y, si se reutiliza uno
  revocado, se revoca toda la familia.
- **Login:** el mensaje de error es siempre genérico (no revela si el usuario existe). Hay bloqueo
  por intentos fallidos y límite de peticiones por IP (`RateLimitPolicies`).
- **Autorización:** se valida en la API. Las guardas de Angular solo mejoran la experiencia.
  Un cliente que pide una cuenta ajena recibe 404.
- **Secretos:** `Jwt:Key`, `Auth:PinPepper` y la cadena de conexión salen de variables de entorno.
  `Program.cs` los valida al arrancar. Nunca pongas valores reales en `appsettings*.json`.
- No registres en logs datos personales (documento, teléfono, correo) ni tokens.
