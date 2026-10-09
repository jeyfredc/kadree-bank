// Contrato de backend: Models/DTOs/Responses/PagedResult.cs.

/** Una página de resultados calculada en el servidor. */
export interface PagedResult<T> {
  items: T[];
  /** Página actual, desde 1. */
  page: number;
  pageSize: number;
  /** Total de elementos en todas las páginas. */
  totalItems: number;
  /** Total de páginas (0 si no hay elementos). */
  totalPages: number;
}

/** Página y tamaño pedidos. */
export interface PageRequest {
  page: number;
  pageSize: number;
}

/** Tamaño máximo de página que acepta la API. */
export const MAX_PAGE_SIZE = 100;
