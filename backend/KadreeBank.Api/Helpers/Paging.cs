using KadreeBank.Api.Exceptions;

namespace KadreeBank.Api.Helpers
{
    /// <summary>Límites y validación de los parámetros de paginación (<c>page</c>, <c>pageSize</c>).</summary>
    public static class Paging
    {
        /// <summary>Tamaño de página cuando no se indica otro.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>Tamaño máximo de página: evita respuestas enormes aunque se pida más.</summary>
        public const int MaxPageSize = 100;

        /// <summary>Valida la página y el tamaño pedidos.</summary>
        /// <param name="page">Número de página, desde 1.</param>
        /// <param name="pageSize">Elementos por página, de 1 a <see cref="MaxPageSize"/>.</param>
        /// <exception cref="RequestValidationException">Página menor que 1 o tamaño fuera de rango.</exception>
        public static void Validate(int page, int pageSize)
        {
            if (page < 1)
                throw new RequestValidationException("page", "La página debe ser 1 o mayor.");
            if (pageSize < 1 || pageSize > MaxPageSize)
                throw new RequestValidationException("pageSize", $"El tamaño de página debe estar entre 1 y {MaxPageSize}.");
        }

        /// <summary>Cuántos elementos saltar para llegar a la página.</summary>
        public static int Skip(int page, int pageSize) => (page - 1) * pageSize;
    }
}
