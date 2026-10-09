using System.Text.RegularExpressions;
using KadreeBank.Api.Exceptions;
using KadreeBank.Api.Helpers;
using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.DTOs.Auth;
using KadreeBank.Api.Models.DTOs.Responses;
using KadreeBank.Api.Models.Entities;

namespace KadreeBank.Api.Services
{
    /// <summary>Implementa inicio y cierre de sesión, renovación de tokens y registro de clientes.</summary>
    public partial class AuthService : IAuthService
    {
        private readonly IUserDao _userDao;
        private readonly ICityDao _cityDao;
        private readonly ICurrentUser _currentUser;
        private readonly PinHasher _pinHasher;
        private readonly JwtTokenFactory _tokens;
        private readonly AuthSettings _settings;
        private readonly TimeProvider _time;
        private readonly ILogger<AuthService> _logger;

        /// <summary>Crea el servicio con sus dependencias (inyectadas por DI).</summary>
        /// <param name="userDao">Acceso a usuarios y sesiones.</param>
        /// <param name="cityDao">Acceso a ciudades, para validar la ciudad del registro.</param>
        /// <param name="currentUser">Usuario de la petición (para <see cref="Me"/>).</param>
        /// <param name="pinHasher">Hash y verificación de contraseñas.</param>
        /// <param name="tokens">Emisión de access y refresh tokens.</param>
        /// <param name="settings">Intentos permitidos y duración del bloqueo.</param>
        /// <param name="time">Reloj; en pruebas se reemplaza por uno fijo.</param>
        /// <param name="logger">Registra inicios fallidos, bloqueos y reutilización de tokens.</param>
        public AuthService(IUserDao userDao, ICityDao cityDao, ICurrentUser currentUser, PinHasher pinHasher,
            JwtTokenFactory tokens, AuthSettings settings, TimeProvider time, ILogger<AuthService> logger)
        {
            _userDao = userDao ?? throw new ArgumentNullException(nameof(userDao));
            _cityDao = cityDao ?? throw new ArgumentNullException(nameof(cityDao));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _pinHasher = pinHasher ?? throw new ArgumentNullException(nameof(pinHasher));
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private DateTime Now => _time.GetUtcNow().UtcDateTime;

        /// <inheritdoc/>
        public async Task<AuthSessionResult> Login(LoginRequestDto request, CancellationToken ct)
        {
            var identifier = NormalizeIdentifier(request.Identifier);
            var user = await _userDao.FindByIdentifierAsync(identifier, ct);
            var now = Now;

            if (user is null)
            {
                // Mismo trabajo y mismo mensaje que con un usuario real: no revelamos quién es cliente.
                _pinHasher.VerifyDummy(request.Pin);
                throw new AuthenticationFailedException(_settings.MaxFailedAttempts, _settings.LockoutMinutes);
            }

            if (user.IsLocked(now))
                throw Locked(user.LockedUntilUtc!.Value, now);

            if (!_pinHasher.Verify(request.Pin, user.PinHash))
            {
                var lockedNow = user.RegisterFailedAttempt(now, _settings.MaxFailedAttempts, TimeSpan.FromMinutes(_settings.LockoutMinutes));
                await _userDao.SaveChangesAsync(ct);
                if (lockedNow)
                {
                    _logger.LogWarning("Acceso bloqueado por intentos fallidos para el usuario {UserId}", user.Id);
                    throw Locked(user.LockedUntilUtc!.Value, now);
                }
                throw new AuthenticationFailedException(_settings.MaxFailedAttempts, _settings.LockoutMinutes);
            }

            user.RegisterSuccessfulLogin();
            var session = IssueSession(user, Guid.NewGuid(), now, "Ingreso exitoso");
            await _userDao.SaveChangesAsync(ct);
            return session;
        }

        /// <inheritdoc/>
        public async Task<AuthSessionResult> Refresh(string? refreshToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new InvalidSessionException();

            var now = Now;
            var stored = await _userDao.FindRefreshTokenAsync(JwtTokenFactory.HashRefreshToken(refreshToken), ct);
            if (stored is null)
                throw new InvalidSessionException();

            if (stored.RevokedAtUtc is not null)
            {
                // Alguien presentó un token ya usado: puede ser una copia robada. Cerramos toda la sesión.
                _logger.LogWarning("Reutilización de refresh token detectada; se revoca la sesión {FamilyId}", stored.FamilyId);
                await _userDao.RevokeFamilyAsync(stored.FamilyId, now, ct);
                throw new InvalidSessionException();
            }

            if (!stored.IsActive(now))
                throw new InvalidSessionException();

            var session = IssueSession(stored.User, stored.FamilyId, now, "Sesión renovada");
            stored.Revoke(now, JwtTokenFactory.HashRefreshToken(session.RefreshToken));
            await _userDao.SaveChangesAsync(ct);
            return session;
        }

        /// <inheritdoc/>
        public async Task<ApiResponse> Logout(string? refreshToken, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                var stored = await _userDao.FindRefreshTokenAsync(JwtTokenFactory.HashRefreshToken(refreshToken), ct);
                if (stored is not null)
                    await _userDao.RevokeFamilyAsync(stored.FamilyId, Now, ct);
            }
            return new ApiResponse { Success = true, Message = "Cerraste tu sesión." };
        }

        /// <inheritdoc/>
        public async Task<ApiResponse<RegisterResultDto>> Register(RegisterRequestDto request, CancellationToken ct)
        {
            var errors = new Dictionary<string, List<string>>();
            void Add(string field, string message)
            {
                if (!errors.TryGetValue(field, out var list)) errors[field] = list = new List<string>();
                list.Add(message);
            }

            var customerType = request.CustomerType!.Value;
            var documentType = request.DocumentType!.Value;
            var documentNumber = request.DocumentNumber.Trim().ToUpperInvariant();
            var username = request.Username.Trim().ToLowerInvariant();
            var fullName = CollapseSpaces(request.FullName);

            if (!request.AcceptTerms)
                Add("acceptTerms", "Debes aceptar los términos y condiciones para crear la cuenta.");

            if (!Customer.IsDocumentAllowed(customerType, documentType))
                Add("documentType", customerType == CustomerType.Juridica
                    ? "Las empresas se registran con NIT."
                    : "Para personas usa cédula de ciudadanía, cédula de extranjería o pasaporte.");
            else if (!IsValidDocumentNumber(documentType, documentNumber))
                Add("documentNumber", DocumentFormatMessage(documentType));

            if (!UsernameFormat().IsMatch(username) || !username.Any(char.IsLetter))
                Add("username", "El usuario tiene de 6 a 20 caracteres: letras minúsculas, números y punto, con al menos una letra.");

            if (request.Pin != request.PinConfirmation)
                Add("pinConfirmation", "Las contraseñas no coinciden.");
            foreach (var message in PinRules.Validate(request.Pin, documentNumber))
                Add("pin", message);

            var cityName = await _cityDao.GetNameAsync(request.CityId, ct);
            if (cityName is null)
                Add("cityId", "La ciudad indicada no existe.");

            if (errors.Count > 0)
                throw new RequestValidationException(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));

            if (await _userDao.DocumentExistsAsync(documentNumber, ct))
                throw new ConflictException("documento_registrado", "documentNumber", "Ya existe un cliente con ese documento. Si es tuyo, ingresa con él.");
            if (await _userDao.UsernameExistsAsync(username, ct))
                throw new ConflictException("usuario_no_disponible", "username", "Ese nombre de usuario ya está en uso. Elige otro.");

            var customer = new Customer
            {
                DocumentNumber = documentNumber,
                DocumentType = documentType,
                FullName = fullName,
                Type = customerType,
                Email = request.Email.Trim().ToLowerInvariant(),
                Phone = request.Phone.Trim(),
            };
            var account = await _userDao.RegisterAsync(customer, request.CityId, username, _pinHasher.Hash(request.Pin), Now, ct);
            _logger.LogInformation("Cliente registrado con la cuenta {AccountNumber}", account.Number);

            return ApiResponse<RegisterResultDto>.Ok(new RegisterResultDto
            {
                AccountNumber = account.Number,
                AccountType = account.Type,
                OriginCity = cityName!,
                Username = username,
                FullName = fullName,
            }, "Tu cuenta está lista.");
        }

        /// <inheritdoc/>
        public async Task<ApiResponse<UsernameAvailabilityDto>> CheckUsername(string username, CancellationToken ct)
        {
            var normalized = (username ?? string.Empty).Trim().ToLowerInvariant();
            UsernameAvailabilityDto result;
            if (!UsernameFormat().IsMatch(normalized) || !normalized.Any(char.IsLetter))
                result = new UsernameAvailabilityDto { Username = normalized, Available = false, Message = "De 6 a 20 caracteres: letras minúsculas, números y punto, con al menos una letra." };
            else if (await _userDao.UsernameExistsAsync(normalized, ct))
                result = new UsernameAvailabilityDto { Username = normalized, Available = false, Message = "Ese usuario ya está en uso. Elige otro." };
            else
                result = new UsernameAvailabilityDto { Username = normalized, Available = true, Message = "Este usuario está disponible." };

            return ApiResponse<UsernameAvailabilityDto>.Ok(result, "Consulta realizada");
        }

        /// <inheritdoc/>
        public async Task<ApiResponse<AuthUserDto>> Me(CancellationToken ct)
        {
            var user = _currentUser.UserId is long id ? await _userDao.FindByIdAsync(id, ct) : null;
            if (user is null)
                throw new InvalidSessionException();
            return ApiResponse<AuthUserDto>.Ok(ToDto(user), "Sesión activa");
        }

        /// <summary>Emite access token y refresh token, y deja el refresh token pendiente de guardar.</summary>
        /// <param name="user">Usuario autenticado.</param>
        /// <param name="familyId">Familia nueva (ingreso) o la misma (renovación).</param>
        /// <param name="nowUtc">Instante actual.</param>
        /// <param name="message">Mensaje de la respuesta.</param>
        /// <returns>La sesión para el controlador.</returns>
        private AuthSessionResult IssueSession(User user, Guid familyId, DateTime nowUtc, string message)
        {
            var userDto = ToDto(user);
            var (accessToken, expires) = _tokens.CreateAccessToken(user, userDto.FullName, nowUtc);
            var (refreshToken, refreshHash) = JwtTokenFactory.CreateRefreshToken();
            var stored = new RefreshToken(user.Id, refreshHash, familyId, nowUtc, _tokens.RefreshTokenLifetime);
            _userDao.AddRefreshToken(stored);

            return new AuthSessionResult
            {
                Response = ApiResponse<LoginResultDto>.Ok(new LoginResultDto
                {
                    AccessToken = accessToken,
                    AccessTokenExpiresAtUtc = expires,
                    User = userDto,
                }, message),
                RefreshToken = refreshToken,
                RefreshTokenExpiresAtUtc = stored.ExpiresAtUtc,
            };
        }

        private AccountLockedException Locked(DateTime lockedUntilUtc, DateTime nowUtc) =>
            new(lockedUntilUtc, Math.Max(1, (int)Math.Ceiling((lockedUntilUtc - nowUtc).TotalMinutes)));

        private static AuthUserDto ToDto(User user) => new()
        {
            Username = user.Username,
            FullName = user.Customer?.FullName ?? "Banco Kadree",
            Role = user.Role,
        };

        /// <summary>Usuario en minúscula; documento tal cual (sin espacios ni puntos de miles).</summary>
        private static string NormalizeIdentifier(string identifier)
        {
            var trimmed = identifier.Trim();
            return trimmed.Any(char.IsLetter) ? trimmed.ToLowerInvariant() : trimmed.Replace(".", "").Replace(" ", "");
        }

        private static string CollapseSpaces(string value) => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        private static bool IsValidDocumentNumber(DocumentType type, string number) => type switch
        {
            DocumentType.CC or DocumentType.CE => DigitsDocument().IsMatch(number),
            DocumentType.PA => PassportDocument().IsMatch(number),
            DocumentType.NIT => NitDocument().IsMatch(number),
            _ => false,
        };

        private static string DocumentFormatMessage(DocumentType type) => type switch
        {
            DocumentType.PA => "El pasaporte tiene de 6 a 12 letras o números, sin espacios.",
            DocumentType.NIT => "Escribe el NIT con su dígito de verificación, sin puntos: 900123456-1.",
            _ => "Escribe solo números, sin puntos: de 6 a 10 dígitos.",
        };

        [GeneratedRegex("^[a-z0-9.]{6,20}$")]
        private static partial Regex UsernameFormat();

        [GeneratedRegex("^[0-9]{6,10}$")]
        private static partial Regex DigitsDocument();

        [GeneratedRegex("^[A-Z0-9]{6,12}$")]
        private static partial Regex PassportDocument();

        [GeneratedRegex("^[0-9]{9}-[0-9]$")]
        private static partial Regex NitDocument();
    }
}
