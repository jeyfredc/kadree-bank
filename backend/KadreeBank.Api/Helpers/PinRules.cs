namespace KadreeBank.Api.Helpers
{
    /// <summary>Reglas de una contraseña nueva de 4 dígitos (registro).</summary>
    public static class PinRules
    {
        /// <summary>Valida una contraseña nueva.</summary>
        /// <param name="pin">Contraseña propuesta.</param>
        /// <param name="documentNumber">Documento del cliente: la contraseña no puede ser sus últimos 4 dígitos.</param>
        /// <returns>Mensajes de las reglas que no se cumplen; vacío si es válida.</returns>
        public static List<string> Validate(string pin, string documentNumber)
        {
            var errors = new List<string>();
            if (pin.Length != 4 || !pin.All(char.IsAsciiDigit))
            {
                errors.Add("La contraseña tiene exactamente 4 dígitos.");
                return errors;
            }

            if (pin.Distinct().Count() == 1)
                errors.Add("La contraseña no puede repetir el mismo número (por ejemplo 1111).");

            if (IsSequence(pin, +1) || IsSequence(pin, -1))
                errors.Add("La contraseña no puede ser una secuencia (por ejemplo 1234 o 4321).");

            var documentDigits = new string(documentNumber.Where(char.IsAsciiDigit).ToArray());
            if (documentDigits.Length >= 4 && documentDigits[^4..] == pin)
                errors.Add("La contraseña no puede ser igual a los últimos 4 dígitos de tu documento.");

            return errors;
        }

        private static bool IsSequence(string pin, int step)
        {
            for (var i = 1; i < pin.Length; i++)
                if (pin[i] - pin[i - 1] != step)
                    return false;
            return true;
        }
    }
}
