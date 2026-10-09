namespace KadreeBank.Api.Models.DTOs.Responses
{
    /// <summary>
    /// Respuesta estándar de la API: toda respuesta, de éxito o de error, tiene esta forma.
    /// Las respuestas con datos usan <see cref="ApiResponse{T}"/>.
    /// </summary>
    public class ApiResponse
    {
        /// <summary>true si la operación se completó; false si hubo un error.</summary>
        public bool Success { get; set; }

        /// <summary>Mensaje para mostrar al usuario, en español.</summary>
        public string? Message { get; set; }

        /// <summary>
        /// Código estable del error para que el cliente reaccione sin interpretar el mensaje:
        /// <c>validacion</c>, <c>no_encontrado</c>, <c>operacion_concurrente</c>, <c>saldo_insuficiente</c>,
        /// <c>monto_invalido</c>, <c>error_interno</c>… Es null cuando <see cref="Success"/> es true.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>Lista plana de mensajes de error (vacía si no hay).</summary>
        public IEnumerable<string> Errors { get; set; } = new List<string>();

        /// <summary>Errores de validación por campo, por ejemplo <c>{ "amount": ["El monto debe ser…"] }</c>.</summary>
        public IDictionary<string, string[]> ValidationErrors { get; set; } = new Dictionary<string, string[]>();

        /// <summary>Crea una respuesta de error sin detalle por campo.</summary>
        /// <param name="message">Mensaje para el usuario.</param>
        /// <param name="errorCode">Código estable del error.</param>
        /// <returns>Respuesta con <see cref="Success"/> en false.</returns>
        public static ApiResponse Fail(string message, string errorCode) => new()
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
        };
    }

    /// <summary>Respuesta estándar con datos.</summary>
    /// <typeparam name="T">Tipo de los datos devueltos.</typeparam>
    public class ApiResponse<T> : ApiResponse
    {
        /// <summary>Datos de la respuesta; null en los errores.</summary>
        public T? Data { get; set; }

        /// <summary>Crea una respuesta exitosa con datos.</summary>
        /// <param name="data">Datos a devolver.</param>
        /// <param name="message">Mensaje para el usuario, por ejemplo "Retiro realizado exitosamente".</param>
        /// <returns>Respuesta con <see cref="ApiResponse.Success"/> en true.</returns>
        public static ApiResponse<T> Ok(T data, string message) => new()
        {
            Success = true,
            Message = message,
            Data = data,
        };
    }
}
