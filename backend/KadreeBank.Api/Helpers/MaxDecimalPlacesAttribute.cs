using System.ComponentModel.DataAnnotations;

namespace KadreeBank.Api.Helpers
{
    /// <summary>Valida que un decimal no tenga más de N cifras decimales (p. ej. pesos con centavos).</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class MaxDecimalPlacesAttribute : ValidationAttribute
    {
        private readonly int _places;

        /// <summary>Crea el validador.</summary>
        /// <param name="places">Cantidad máxima de decimales permitidos.</param>
        public MaxDecimalPlacesAttribute(int places)
        {
            _places = places;
        }

        /// <summary>Indica si el valor cumple el máximo de decimales.</summary>
        /// <param name="value">Valor de la propiedad.</param>
        /// <returns>true si no es un decimal o si no tiene más decimales de los permitidos.</returns>
        public override bool IsValid(object? value) =>
            value is not decimal amount || decimal.Round(amount, _places) == amount;
    }
}
