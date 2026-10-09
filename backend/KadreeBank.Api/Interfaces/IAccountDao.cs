using KadreeBank.Api.Exceptions;
using KadreeBank.Api.Models.DTOs.Accounts;
using KadreeBank.Api.Models.Entities;

namespace KadreeBank.Api.Interfaces
{
    /// <summary>
    /// Acceso a datos de cuentas y movimientos.
    /// Las escrituras siguen siempre este patrón dentro de <see cref="ExecuteInTransactionAsync{T}"/>:
    /// <see cref="GetByNumberForUpdateAsync"/> (bloquea) → la entidad cambia el saldo →
    /// <see cref="AddMovement"/> → al terminar se guarda y se confirma.
    /// Las lecturas devuelven DTOs, no llevan seguimiento de cambios y no toman bloqueos.
    /// </summary>
    public interface IAccountDao
    {
        /// <summary>
        /// Ejecuta <paramref name="work"/> en una transacción READ COMMITTED; al terminar guarda los
        /// cambios pendientes y confirma. Si <paramref name="work"/> o el guardado fallan, revierte
        /// todo y libera los bloqueos.
        /// </summary>
        /// <typeparam name="T">Tipo del resultado que produce <paramref name="work"/>.</typeparam>
        /// <param name="work">Trabajo a ejecutar dentro de la transacción. Recibe el token de cancelación.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>El resultado de <paramref name="work"/>, después del commit.</returns>
        /// <exception cref="ConcurrencyConflictException">No se obtuvo el bloqueo de una cuenta antes del tiempo límite.</exception>
        /// <exception cref="BusinessRuleException">La base de datos rechazó un saldo negativo (restricción CHECK).</exception>
        Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);

        /// <summary>
        /// Obtiene la cuenta con bloqueo exclusivo de fila (<c>SELECT … FOR UPDATE</c>) hasta que
        /// termine la transacción. Otra operación sobre la misma cuenta espera a que esta termine.
        /// </summary>
        /// <param name="accountNumber">Número de cuenta.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>La cuenta con seguimiento de cambios, o null si no existe.</returns>
        /// <exception cref="InvalidOperationException">Se llamó fuera de <see cref="ExecuteInTransactionAsync{T}"/>.</exception>
        Task<Account?> GetByNumberForUpdateAsync(string accountNumber, CancellationToken ct);

        /// <summary>Agrega un movimiento para que se guarde al confirmar la transacción.</summary>
        /// <param name="movement">Movimiento creado por <see cref="Account.Deposit"/> o <see cref="Account.Withdraw"/>.</param>
        void AddMovement(Movement movement);

        /// <summary>Lista las cuentas con su titular, ciudad de origen y saldo.</summary>
        /// <param name="customerId">Solo las cuentas de este cliente; null = todas (personal del banco).</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Cuentas ordenadas por número.</returns>
        Task<List<AccountSummaryDto>> GetAllAsync(long? customerId, CancellationToken ct);

        /// <summary>Obtiene el resumen de una cuenta.</summary>
        /// <param name="accountNumber">Número de cuenta.</param>
        /// <param name="customerId">Exige que la cuenta sea de este cliente; null = cualquier cuenta (personal del banco).</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>El resumen, o null si la cuenta no existe o es de otro cliente.</returns>
        Task<AccountSummaryDto?> GetSummaryAsync(string accountNumber, long? customerId, CancellationToken ct);

        /// <summary>Obtiene los últimos movimientos de una cuenta, del más nuevo al más antiguo.</summary>
        /// <param name="accountNumber">Número de cuenta.</param>
        /// <param name="limit">Cantidad máxima de movimientos.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Los movimientos (lista vacía si no hay o si la cuenta no existe).</returns>
        Task<List<MovementDto>> GetRecentMovementsAsync(string accountNumber, int limit, CancellationToken ct);

        /// <summary>Obtiene el saldo que tenía la cuenta justo antes de un instante.</summary>
        /// <param name="accountNumber">Número de cuenta.</param>
        /// <param name="beforeUtc">Instante en UTC (excluido).</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>El saldo resultante del último movimiento anterior a <paramref name="beforeUtc"/>, o 0 si no hay movimientos previos.</returns>
        Task<decimal> GetBalanceBeforeAsync(string accountNumber, DateTime beforeUtc, CancellationToken ct);

        /// <summary>Obtiene los movimientos de una cuenta en un rango, en orden cronológico.</summary>
        /// <param name="accountNumber">Número de cuenta.</param>
        /// <param name="fromUtc">Inicio del rango en UTC (incluido).</param>
        /// <param name="toUtc">Fin del rango en UTC (excluido).</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Los movimientos del rango, del más antiguo al más nuevo.</returns>
        Task<List<MovementDto>> GetMovementsInRangeAsync(string accountNumber, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    }
}
