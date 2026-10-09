using System.ComponentModel.DataAnnotations;
using KadreeBank.Api.Models.Entities;

namespace KadreeBank.Api.Models.DTOs.Auth
{
    /// <summary>Cuerpo del inicio de sesión.</summary>
    public class LoginRequestDto
    {
        /// <summary>Nombre de usuario o número de documento.</summary>
        [Required(ErrorMessage = "Escribe tu usuario o tu número de documento.")]
        [MaxLength(20, ErrorMessage = "El usuario o documento admite máximo 20 caracteres.")]
        public string Identifier { get; set; } = string.Empty;

        /// <summary>Contraseña de exactamente 4 dígitos.</summary>
        [Required(ErrorMessage = "Escribe tu contraseña de 4 dígitos.")]
        [RegularExpression("^[0-9]{4}$", ErrorMessage = "La contraseña tiene exactamente 4 dígitos.")]
        public string Pin { get; set; } = string.Empty;
    }

    /// <summary>Datos del usuario en sesión.</summary>
    public class AuthUserDto
    {
        /// <summary>Nombre de usuario.</summary>
        public string Username { get; set; } = null!;

        /// <summary>Nombre del cliente o del usuario del banco, para mostrar.</summary>
        public string FullName { get; set; } = null!;

        /// <summary>Cliente o Banco.</summary>
        public UserRole Role { get; set; }
    }

    /// <summary>
    /// Resultado de iniciar sesión o renovarla. El refresh token NO viaja aquí: va en una cookie
    /// HttpOnly que JavaScript no puede leer.
    /// </summary>
    public class LoginResultDto
    {
        /// <summary>Access token JWT. Se envía como <c>Authorization: Bearer …</c>.</summary>
        public string AccessToken { get; set; } = null!;

        /// <summary>Vencimiento del access token (UTC).</summary>
        public DateTime AccessTokenExpiresAtUtc { get; set; }

        /// <summary>Usuario en sesión.</summary>
        public AuthUserDto User { get; set; } = null!;
    }

    /// <summary>Cuerpo del registro (creación de cuenta).</summary>
    public class RegisterRequestDto
    {
        /// <summary>Persona natural (cuenta de ahorros) o empresa (cuenta corriente).</summary>
        [Required(ErrorMessage = "Elige el tipo de cliente.")]
        public CustomerType? CustomerType { get; set; }

        /// <summary>CC, CE o PA para personas; NIT para empresas.</summary>
        [Required(ErrorMessage = "Elige el tipo de documento.")]
        public DocumentType? DocumentType { get; set; }

        /// <summary>Número de documento: solo dígitos (6 a 10); pasaporte alfanumérico; NIT con dígito de verificación (900123456-1).</summary>
        [Required(ErrorMessage = "Escribe tu número de documento.")]
        [MaxLength(20)]
        public string DocumentNumber { get; set; } = string.Empty;

        /// <summary>Nombre completo o razón social.</summary>
        [Required(ErrorMessage = "Escribe tu nombre completo o la razón social.")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 150 caracteres.")]
        public string FullName { get; set; } = string.Empty;

        /// <summary>Correo electrónico.</summary>
        [Required(ErrorMessage = "Escribe tu correo electrónico.")]
        [EmailAddress(ErrorMessage = "Escribe un correo válido, por ejemplo nombre@correo.com.")]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        /// <summary>Celular de 10 dígitos que empieza por 3.</summary>
        [Required(ErrorMessage = "Escribe tu celular.")]
        [RegularExpression("^3[0-9]{9}$", ErrorMessage = "El celular tiene 10 dígitos y empieza por 3, sin espacios.")]
        public string Phone { get; set; } = string.Empty;

        /// <summary>Ciudad: será la ciudad de origen de la cuenta.</summary>
        [Range(1, int.MaxValue, ErrorMessage = "Elige la ciudad.")]
        public int CityId { get; set; }

        /// <summary>Nombre de usuario: 6 a 20 caracteres en minúscula, números o punto, con al menos una letra.</summary>
        [Required(ErrorMessage = "Elige un nombre de usuario.")]
        [MaxLength(20)]
        public string Username { get; set; } = string.Empty;

        /// <summary>Contraseña de 4 dígitos.</summary>
        [Required(ErrorMessage = "Crea tu contraseña de 4 dígitos.")]
        public string Pin { get; set; } = string.Empty;

        /// <summary>Confirmación de la contraseña.</summary>
        [Required(ErrorMessage = "Confirma tu contraseña.")]
        public string PinConfirmation { get; set; } = string.Empty;

        /// <summary>Aceptación de términos y tratamiento de datos. Debe ser true.</summary>
        public bool AcceptTerms { get; set; }
    }

    /// <summary>Resultado del registro: la cuenta abierta.</summary>
    public class RegisterResultDto
    {
        /// <summary>Número de la cuenta abierta, por ejemplo AH-5001.</summary>
        public string AccountNumber { get; set; } = null!;

        /// <summary>Ahorros o corriente.</summary>
        public AccountType AccountType { get; set; }

        /// <summary>Ciudad de origen de la cuenta.</summary>
        public string OriginCity { get; set; } = null!;

        /// <summary>Nombre de usuario creado.</summary>
        public string Username { get; set; } = null!;

        /// <summary>Nombre del titular.</summary>
        public string FullName { get; set; } = null!;
    }

    /// <summary>Disponibilidad de un nombre de usuario.</summary>
    public class UsernameAvailabilityDto
    {
        /// <summary>Usuario consultado, normalizado a minúscula.</summary>
        public string Username { get; set; } = null!;

        /// <summary>true si cumple las reglas y nadie lo usa.</summary>
        public bool Available { get; set; }

        /// <summary>Explicación para mostrar debajo del campo.</summary>
        public string Message { get; set; } = null!;
    }
}
