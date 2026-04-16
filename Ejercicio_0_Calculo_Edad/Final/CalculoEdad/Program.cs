using CalculoEdad;

var checks = new List<(string Name, Action Run)>
{
    (
        "Caso normal con cumpleaños cumplido",
        () => AssertEqual(26, AgeCalculator.CalculateAge("2000-04-10", "2026-04-16"))
    ),
    (
        "Caso normal con cumpleaños pendiente",
        () => AssertEqual(25, AgeCalculator.CalculateAge("2000-12-20", "2026-04-16"))
    ),
    (
        "Caso límite misma fecha",
        () => AssertEqual(0, AgeCalculator.CalculateAge("2026-04-16", "2026-04-16"))
    ),
    (
        "Caso límite nacido en 29 de febrero",
        () => AssertEqual(21, AgeCalculator.CalculateAge("2004-02-29", "2025-03-01"))
    ),
    (
        "Error por formato de fecha inválido",
        () => AssertThrows<ArgumentException>(() => AgeCalculator.CalculateAge("2000-15-40", "2026-04-16"))
    ),
    (
        "Error por nacimiento posterior a referencia",
        () => AssertThrows<ArgumentOutOfRangeException>(() => AgeCalculator.CalculateAge("2027-01-01", "2026-12-31"))
    )
};

var failed = false;

foreach (var check in checks)
{
    try
    {
        check.Run();
        Console.WriteLine($"[OK] {check.Name}");
    }
    catch (Exception ex)
    {
        failed = true;
        Console.WriteLine($"[ERROR] {check.Name}: {ex.Message}");
    }
}

Environment.ExitCode = failed ? 1 : 0;

static void AssertEqual(int expected, int actual)
{
    if (expected != actual)
    {
        throw new InvalidOperationException($"Esperado: {expected}, Actual: {actual}.");
    }
}

static void AssertThrows<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Se esperaba excepción de tipo {typeof(TException).Name}.");
}
