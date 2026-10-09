using KadreeBank.Api.Exceptions;
using KadreeBank.Api.Helpers;
using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.DTOs.Reports;
using KadreeBank.Api.Models.DTOs.Responses;
using KadreeBank.Api.Models.Entities;

namespace KadreeBank.Api.Services
{
    /// <summary>
    /// Implementa los reportes: valida los parámetros (incluida la paginación), convierte las fechas
    /// de la zona horaria del banco a UTC y delega la consulta en <see cref="IReportDao"/>.
    /// </summary>
    public class ReportService : IReportService
    {
        /// <summary>Umbral del reporte 2 cuando no se indica otro: $1.000.000.</summary>
        public const decimal DefaultOutOfCityThreshold = 1_000_000m;

        private readonly IReportDao _reportDao;
        private readonly ICityDao _cityDao;
        private readonly BankCalendar _calendar;

        /// <summary>Crea el servicio con sus dependencias (inyectadas por DI).</summary>
        /// <param name="reportDao">Consultas de los reportes.</param>
        /// <param name="cityDao">Ciudades, para validar la sede del reporte por sede.</param>
        /// <param name="calendar">Convierte meses y días de la zona horaria del banco a UTC.</param>
        public ReportService(IReportDao reportDao, ICityDao cityDao, BankCalendar calendar)
        {
            _reportDao = reportDao ?? throw new ArgumentNullException(nameof(reportDao));
            _cityDao = cityDao ?? throw new ArgumentNullException(nameof(cityDao));
            _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        }

        /// <inheritdoc/>
        public async Task<ApiResponse<BranchTransactionsPageDto>> GetTransactionsByBranch(
            int year, int month, int? cityId, MovementType? type, int page, int pageSize, CancellationToken ct)
        {
            _calendar.ValidateMonth(year, month);
            Paging.Validate(page, pageSize);
            if (cityId is not null && await _cityDao.GetNameAsync(cityId.Value, ct) is null)
                throw new RequestValidationException("cityId", "La sede indicada no existe.");

            var (fromUtc, toUtc) = _calendar.MonthRangeUtc(year, month);
            var result = await _reportDao.GetTransactionsByBranchAsync(fromUtc, toUtc, cityId, type, page, pageSize, ct);
            return ApiResponse<BranchTransactionsPageDto>.Ok(result, "Reporte generado exitosamente");
        }

        /// <inheritdoc/>
        public async Task<ApiResponse<PagedResult<CustomerTransactionCountDto>>> GetTransactionsByCustomer(
            int year, int month, int page, int pageSize, CancellationToken ct)
        {
            _calendar.ValidateMonth(year, month);
            Paging.Validate(page, pageSize);

            var (fromUtc, toUtc) = _calendar.MonthRangeUtc(year, month);
            var result = await _reportDao.GetTransactionCountByCustomerAsync(fromUtc, toUtc, page, pageSize, ct);
            return ApiResponse<PagedResult<CustomerTransactionCountDto>>.Ok(result, "Reporte generado exitosamente");
        }

        /// <inheritdoc/>
        public async Task<ApiResponse<PagedResult<OutOfCityWithdrawalsDto>>> GetOutOfCityWithdrawals(
            decimal? minTotal, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
        {
            var threshold = minTotal ?? DefaultOutOfCityThreshold;
            if (threshold < 0)
                throw new RequestValidationException("minTotal", "El umbral no puede ser negativo.");
            if (from is not null && to is not null && from > to)
                throw new RequestValidationException("from", "La fecha inicial debe ser anterior o igual a la final.");
            Paging.Validate(page, pageSize);

            // "to" es inclusivo: se consulta hasta el inicio del día siguiente.
            DateTime? fromUtc = from is null ? null : _calendar.DayStartUtc(from.Value);
            DateTime? toUtc = to is null ? null : _calendar.DayStartUtc(to.Value.AddDays(1));

            var result = await _reportDao.GetOutOfCityWithdrawalsAsync(threshold, fromUtc, toUtc, page, pageSize, ct);
            return ApiResponse<PagedResult<OutOfCityWithdrawalsDto>>.Ok(result, "Reporte generado exitosamente");
        }
    }
}
