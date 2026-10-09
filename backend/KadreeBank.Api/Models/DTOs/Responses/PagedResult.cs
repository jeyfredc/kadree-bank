namespace KadreeBank.Api.Models.DTOs.Responses
{
    /// <summary>
    /// Una página de resultados. El servidor calcula los totales; el cliente solo recibe las filas
    /// de la página pedida.
    /// </summary>
    /// <typeparam name="T">Tipo de cada elemento.</typeparam>
    public class PagedResult<T>
    {
        /// <summary>Elementos de esta página.</summary>
        public List<T> Items { get; set; } = new();

        /// <summary>Número de página, desde 1.</summary>
        public int Page { get; set; }

        /// <summary>Tamaño de página pedido.</summary>
        public int PageSize { get; set; }

        /// <summary>Total de elementos en todas las páginas.</summary>
        public int TotalItems { get; set; }

        /// <summary>Total de páginas (0 si no hay elementos).</summary>
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
    }
}
