using System;

namespace Utilities
{
    public static class AgeCalculator
    {
        public static (int Years, int Months, int Days) CalculateAge(DateTime birthDate, DateTime referenceDate)
        {
            // Validaciones previas
            if (birthDate > referenceDate)
            {
                throw new ArgumentException("La fecha de nacimiento no puede ser posterior a la fecha de referencia.");
            }

            // Ajuste para años bisiestos: si birthDate es 29-02 y el año de referenceDate no es bisiesto, usar 28-02
            DateTime adjustedBirthDate = birthDate;
            if (birthDate.Month == 2 && birthDate.Day == 29 && !DateTime.IsLeapYear(referenceDate.Year))
            {
                adjustedBirthDate = new DateTime(birthDate.Year, 2, 28);
            }

            // Cálculo de años
            int years = referenceDate.Year - adjustedBirthDate.Year;
            if (referenceDate < new DateTime(referenceDate.Year, adjustedBirthDate.Month, adjustedBirthDate.Day))
            {
                years--;
            }

            // Cálculo de meses y días restantes
            DateTime tempDate = adjustedBirthDate.AddYears(years);
            int months = 0;
            while (tempDate.AddMonths(1) <= referenceDate)
            {
                tempDate = tempDate.AddMonths(1);
                months++;
            }

            int days = (referenceDate - tempDate).Days;

            // Aproximar días a 30 por mes para consistencia
            if (days > 30) days = 30;

            return (years, months, days);
        }
    }
}