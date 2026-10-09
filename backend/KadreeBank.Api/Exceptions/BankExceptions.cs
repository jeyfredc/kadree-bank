using System.Globalization;
using KadreeBank.Api.Models.Entities;

namespace KadreeBank.Api.Exceptions
{
    /// <summary>
    /// Violación de una regla de negocio (HTTP 422). <see cref="ErrorCode"/> es estable
    /// para que el cliente pueda reaccionar sin interpretar el mensaje.
    /// </summary>
    public class BusinessRuleException : Exception
    {
        /// <summary>Código estable del error, por ejemplo <c>saldo_insuficiente</c>.</summary>
        public string ErrorCode { get; }

        /// <summary>Crea la excepción.</summary>
        /// <param name="errorCode">Código estable que viaja en <c>ApiResponse.ErrorCode</c>.</param>
        /// <param name="message">Mensaje para el usuario, en español.</param>
        public BusinessRuleException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }

    /// <summary>El retiro dejaría la cuenta con saldo negativo (HTTP 422, código <c>saldo_insuficiente</c>).</summary>
    public class InsufficientFundsException : BusinessRuleException
    {
        /// <summary>Saldo que tenía la cuenta al intentar el retiro.</summary>
        public decimal Balance { get; }

        /// <summary>Monto que se intentó retirar.</summary>
        public decimal Requested { get; }

        /// <summary>Crea la excepción con un mensaje que muestra los montos en formato colombiano.</summary>
        /// <param name="accountNumber">Número de cuenta.</param>
        /// <param name="balance">Saldo actual.</param>
        /// <param name="requested">Monto solicitado.</param>
        public InsufficientFundsException(string accountNumber, decimal balance, decimal requested)
            : base("saldo_insuficiente",
                $"La cuenta {accountNumber} no tiene saldo suficiente: saldo ${Cop(balance)}, retiro solicitado ${Cop(requested)}. " +
                "Una cuenta no puede quedar con saldo negativo.")
        {
            Balance = balance;
            Requested = requested;
        }

        /// <summary>Formatea un monto como pesos colombianos: <c>1.234.567,89</c>.</summary>
        private static string Cop(decimal value) => value.ToString("N2", CultureInfo.GetCultureInfo("es-CO"));
    }

    /// <summary>Monto no válido: cero, negativo, con más de dos decimales o sobre el tope (HTTP 422, código <c>monto_invalido</c>).</summary>
    public class InvalidAmountException : BusinessRuleException
    {
        /// <summary>Crea la excepción.</summary>
        /// <param name="message">Motivo por el que el monto no es válido.</param>
        public InvalidAmountException(string message) : base("monto_invalido", message) { }
    }

    /// <summary>El tipo de cuenta no corresponde al tipo de cliente (HTTP 422, código <c>tipo_cuenta_no_permitido</c>).</summary>
    public class AccountTypeNotAllowedException : BusinessRuleException
    {
        /// <summary>Crea la excepción.</summary>
        /// <param name="customerType">Tipo del cliente.</param>
        /// <param name="accountType">Tipo de cuenta que se intentó abrir.</param>
        public AccountTypeNotAllowedException(CustomerType customerType, AccountType accountType)
            : base("tipo_cuenta_no_permitido",
                $"Un cliente {customerType} no puede abrir una cuenta {accountType}. " +
                "Las cuentas de ahorros son para personas naturales y las corrientes para empresas.")
        {
        }
    }

    /// <summary>El recurso solicitado no existe (HTTP 404, código <c>no_encontrado</c>).</summary>
    public class NotFoundException : Exception
    {
        /// <summary>Crea la excepción.</summary>
        /// <param name="message">Mensaje para el usuario, por ejemplo "La cuenta X no existe.".</param>
        public NotFoundException(string message) : base(message) { }
    }

    /// <summary>Datos de entrada inválidos detectados en el servicio (HTTP 400, código <c>validacion</c>).</summary>
    public class RequestValidationException : Exception
    {
        /// <summary>Errores por campo, en el mismo formato que <c>ApiResponse.ValidationErrors</c>.</summary>
        public IDictionary<string, string[]> ValidationErrors { get; }

        /// <summary>Crea la excepción para un solo campo.</summary>
        /// <param name="field">Nombre del campo en camelCase, como lo envía el cliente (p. ej. <c>cityId</c>).</param>
        /// <param name="message">Mensaje de error del campo.</param>
        public RequestValidationException(string field, string message) : base(message)
        {
            ValidationErrors = new Dictionary<string, string[]> { [field] = new[] { message } };
        }

        /// <summary>Crea la excepción con errores en varios campos.</summary>
        /// <param name="errors">Mensajes por campo (camelCase); debe tener al menos uno.</param>
        public RequestValidationException(IDictionary<string, string[]> errors)
            : base(errors.Values.SelectMany(v => v).FirstOrDefault() ?? "La solicitud tiene datos inválidos.")
        {
            ValidationErrors = errors;
        }
    }

    /// <summary>El usuario no tiene permiso para esta operación (HTTP 403, código <c>sin_permiso</c>).</summary>
    public class ForbiddenException : Exception
    {
        /// <summary>Crea la excepción.</summary>
        /// <param name="message">Qué no se puede hacer y por qué.</param>
        public ForbiddenException(string message) : base(message) { }
    }

    /// <summary>
    /// Usuario o contraseña incorrectos (HTTP 401, código <c>credenciales_invalidas</c>).
    /// El mensaje es siempre el mismo, exista o no el usuario, para no revelar quién es cliente.
    /// </summary>
    public class AuthenticationFailedException : Exception
    {
        /// <summary>Crea la excepción con el mensaje genérico.</summary>
        /// <param name="maxAttempts">Intentos permitidos antes del bloqueo.</param>
        /// <param name="lockoutMinutes">Duración del bloqueo.</param>
        public AuthenticationFailedException(int maxAttempts, int lockoutMinutes)
            : base($"Usuario o contraseña incorrectos. Después de {maxAttempts} intentos fallidos bloquearemos el acceso por {lockoutMinutes} minutos.") { }
    }

    /// <summary>El acceso está bloqueado por intentos fallidos (HTTP 423, código <c>acceso_bloqueado</c>).</summary>
    public class AccountLockedException : Exception
    {
        /// <summary>Hasta cuándo dura el bloqueo (UTC).</summary>
        public DateTime LockedUntilUtc { get; }

        /// <summary>Crea la excepción.</summary>
        /// <param name="lockedUntilUtc">Fin del bloqueo.</param>
        /// <param name="minutesLeft">Minutos que faltan, para el mensaje.</param>
        public AccountLockedException(DateTime lockedUntilUtc, int minutesLeft)
            : base($"Bloqueamos el acceso por seguridad después de varios intentos fallidos. Intenta de nuevo en {minutesLeft} minuto{(minutesLeft == 1 ? "" : "s")}.")
        {
            LockedUntilUtc = lockedUntilUtc;
        }
    }

    /// <summary>La sesión no es válida o venció; hay que ingresar de nuevo (HTTP 401, código <c>sesion_invalida</c>).</summary>
    public class InvalidSessionException : Exception
    {
        /// <summary>Crea la excepción.</summary>
        public InvalidSessionException() : base("Tu sesión terminó. Ingresa de nuevo.") { }
    }

    /// <summary>El dato ya está registrado: documento o usuario en uso (HTTP 409).</summary>
    public class ConflictException : Exception
    {
        /// <summary>Código estable: <c>documento_registrado</c> o <c>usuario_no_disponible</c>.</summary>
        public string ErrorCode { get; }

        /// <summary>Campo del formulario al que se refiere, en camelCase.</summary>
        public string Field { get; }

        /// <summary>Crea la excepción.</summary>
        /// <param name="errorCode">Código estable.</param>
        /// <param name="field">Campo del formulario.</param>
        /// <param name="message">Mensaje para el usuario.</param>
        public ConflictException(string errorCode, string field, string message) : base(message)
        {
            ErrorCode = errorCode;
            Field = field;
        }
    }

    /// <summary>
    /// No se obtuvo a tiempo el bloqueo de la cuenta porque otra operación lo retiene
    /// (HTTP 409, código <c>operacion_concurrente</c>). Es seguro reintentar: no se aplicó ningún cambio.
    /// </summary>
    public class ConcurrencyConflictException : Exception
    {
        /// <summary>Crea la excepción.</summary>
        /// <param name="message">Mensaje para el usuario.</param>
        /// <param name="inner">Error original de PostgreSQL (tiempo de bloqueo agotado).</param>
        public ConcurrencyConflictException(string message, Exception? inner = null) : base(message, inner) { }
    }
}
