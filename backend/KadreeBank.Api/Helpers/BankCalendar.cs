using KadreeBank.Api.Exceptions;

namespace KadreeBank.Api.Helpers
{
    /// <summary>Configuración del banco, sección <c>Bank</c> de appsettings (o variables <c>Bank__*</c>).</summary>
    public class BankOptions
    {
        /// <summary>Nombre de la sección en la configuración.</summary>
        public const string Section = "Bank";

        /// <summary>Zona horaria IANA con la que se delimitan los meses de extractos y reportes.</summary>
        public string TimeZone { get; set; } = "America/Bogota";
    }

    /// <summary>
    /// Convierte fechas de la zona horaria del banco a rangos UTC. Los movimientos se guardan en UTC;
    /// un retiro a las 11 p. m. del 31 en Bogotá pertenece a ese mes aunque en UTC ya sea el día 1.
    /// </summary>
    public class BankCalendar
    {
        private readonly TimeZoneInfo _zone;
        private readonly TimeProvider _time;

        /// <summary>Crea el calendario.</summary>
        /// <param name="options">Configuración con la zona horaria del banco.</param>
        /// <param name="time">Reloj del sistema; en pruebas se reemplaza por uno fijo.</param>
        /// <exception cref="TimeZoneNotFoundException">La zona horaria configurada no existe en el sistema.</exception>
        public BankCalendar(BankOptions options, TimeProvider time)
        {
            _zone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone);
            _time = time;
        }

        /// <summary>Convierte un mes de la zona horaria del banco en un rango UTC.</summary>
        /// <param name="year">Año.</param>
        /// <param name="month">Mes (1-12).</param>
        /// <returns>Inicio (incluido) y fin (excluido) del mes en UTC. Para Bogotá, septiembre de 2026 es 2026-09-01 05:00Z a 2026-10-01 05:00Z.</returns>
        public (DateTime FromUtc, DateTime ToUtc) MonthRangeUtc(int year, int month)
        {
            var localStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            return (TimeZoneInfo.ConvertTimeToUtc(localStart, _zone),
                    TimeZoneInfo.ConvertTimeToUtc(localStart.AddMonths(1), _zone));
        }

        /// <summary>Instante UTC en que empieza <paramref name="day"/> en la zona del banco.</summary>
        /// <param name="day">Día en hora del banco.</param>
        /// <returns>Medianoche de ese día convertida a UTC.</returns>
        public DateTime DayStartUtc(DateOnly day) =>
            TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), _zone);

        /// <summary>Mes actual en la zona horaria del banco (no en UTC).</summary>
        /// <returns>Año y mes de hoy en hora del banco.</returns>
        public (int Year, int Month) CurrentMonth()
        {
            var local = TimeZoneInfo.ConvertTime(_time.GetUtcNow(), _zone);
            return (local.Year, local.Month);
        }

        /// <summary>Indica si un mes es posterior al mes actual del banco.</summary>
        /// <param name="year">Año.</param>
        /// <param name="month">Mes (1-12).</param>
        /// <returns>true si el mes todavía no ha empezado.</returns>
        public bool IsFutureMonth(int year, int month)
        {
            var (y, m) = CurrentMonth();
            return year > y || (year == y && month > m);
        }

        /// <summary>Valida año y mes de un extracto o reporte (no se permiten meses futuros).</summary>
        /// <param name="year">Año (2000-2100).</param>
        /// <param name="month">Mes (1-12).</param>
        /// <exception cref="RequestValidationException">Año o mes fuera de rango, o mes futuro.</exception>
        public void ValidateMonth(int year, int month)
        {
            if (month is < 1 or > 12)
                throw new RequestValidationException("month", "El mes debe estar entre 1 y 12.");
            if (year is < 2000 or > 2100)
                throw new RequestValidationException("year", "El año debe estar entre 2000 y 2100.");
            if (IsFutureMonth(year, month))
                throw new RequestValidationException("month", "No se puede consultar un mes futuro.");
        }
    }
}
