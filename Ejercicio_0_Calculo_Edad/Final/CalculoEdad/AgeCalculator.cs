using System.Globalization;

namespace CalculoEdad;

public static class AgeCalculator
{
    private const string DateFormat = "yyyy-MM-dd";

    public static int CalculateAge(DateOnly birthDate, DateOnly referenceDate)
    {
        if (birthDate == DateOnly.MinValue)
        {
            throw new ArgumentException("La fecha de nacimiento no es válida.", nameof(birthDate));
        }

        if (referenceDate == DateOnly.MinValue)
        {
            throw new ArgumentException("La fecha de referencia no es válida.", nameof(referenceDate));
        }

        if (birthDate > referenceDate)
        {
            throw new ArgumentOutOfRangeException(nameof(birthDate), "La fecha de nacimiento no puede ser posterior a la fecha de referencia.");
        }

        var age = referenceDate.Year - birthDate.Year;
        var birthdayThisYear = birthDate.AddYears(age);

        if (referenceDate < birthdayThisYear)
        {
            age--;
        }

        return age;
    }

    public static int CalculateAge(string birthDateText, string referenceDateText)
    {
        if (string.IsNullOrWhiteSpace(birthDateText))
        {
            throw new ArgumentException("La fecha de nacimiento es obligatoria.", nameof(birthDateText));
        }

        if (string.IsNullOrWhiteSpace(referenceDateText))
        {
            throw new ArgumentException("La fecha de referencia es obligatoria.", nameof(referenceDateText));
        }

        if (!DateOnly.TryParseExact(birthDateText, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
        {
            throw new ArgumentException($"Fecha de nacimiento inválida. Formato esperado: {DateFormat}.", nameof(birthDateText));
        }

        if (!DateOnly.TryParseExact(referenceDateText, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var referenceDate))
        {
            throw new ArgumentException($"Fecha de referencia inválida. Formato esperado: {DateFormat}.", nameof(referenceDateText));
        }

        return CalculateAge(birthDate, referenceDate);
    }
}
