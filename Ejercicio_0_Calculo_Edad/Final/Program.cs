using System;
using Utilities;

class Program
{
    static void Main(string[] args)
    {
        // Ejemplo 1: Caso normal - Persona nacida el 15-05-1990, referencia 15-05-2023
        DateTime birth1 = new DateTime(1990, 5, 15);
        DateTime ref1 = new DateTime(2023, 5, 15);
        var age1 = AgeCalculator.CalculateAge(birth1, ref1);
        Console.WriteLine($"Edad para nacimiento 15-05-1990 y referencia 15-05-2023: {age1.Years} años, {age1.Months} meses, {age1.Days} días");

        // Ejemplo 2: Caso límite - Fecha de nacimiento igual a referencia
        DateTime birth2 = new DateTime(2000, 1, 1);
        DateTime ref2 = new DateTime(2000, 1, 1);
        var age2 = AgeCalculator.CalculateAge(birth2, ref2);
        Console.WriteLine($"Edad para nacimiento y referencia 01-01-2000: {age2.Years} años, {age2.Months} meses, {age2.Days} días");

        // Ejemplo 3: Caso con año bisiesto - Nacida 29-02-2000, referencia 28-02-2023
        DateTime birth3 = new DateTime(2000, 2, 29);
        DateTime ref3 = new DateTime(2023, 2, 28);
        var age3 = AgeCalculator.CalculateAge(birth3, ref3);
        Console.WriteLine($"Edad para nacimiento 29-02-2000 y referencia 28-02-2023: {age3.Years} años, {age3.Months} meses, {age3.Days} días");

        // Ejemplo 4: Error - Fecha de nacimiento posterior
        try
        {
            DateTime birth4 = new DateTime(2025, 1, 1);
            DateTime ref4 = new DateTime(2020, 1, 1);
            var age4 = AgeCalculator.CalculateAge(birth4, ref4);
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"Error esperado: {ex.Message}");
        }

        // Ejemplo 5: Caso normal con meses/días - Nacida 01-01-2000, referencia 31-12-2022
        DateTime birth5 = new DateTime(2000, 1, 1);
        DateTime ref5 = new DateTime(2022, 12, 31);
        var age5 = AgeCalculator.CalculateAge(birth5, ref5);
        Console.WriteLine($"Edad para nacimiento 01-01-2000 y referencia 31-12-2022: {age5.Years} años, {age5.Months} meses, {age5.Days} días");
    }
}