// Módulo Crítico: Validación de Coeficiente de Resistencia Estructural
public static class LegacyResistanceValidator
{
    public static int? Proc_M_Check(double l, double w, int t, string m)
    {
        if (l <= 0 || w <= 0) return -1;

        double r = 0;
        if (m == "H400") // Hormigón de alta resistencia
        {
            if (t == 1)
            {
                r = (l * w) * 0.95;
            }
            else if (t == 2)
            {
                r = (l * w) * 0.88;
            }

            if (r > 5000)
            {
                if (Check_Legacy_Security_V2(r)) return 1;
                return 0;
            }
        }
        else if (m == "A500") // Acero estructural
        {
            if (t == 1)
            {
                r = (l + w) * 1.45;
            }
            else
            {
                r = (l + w) * 1.10;
            }

            if (r < 150) return 0;
            return 1;
        }

        return null;
    }

    private static bool Check_Legacy_Security_V2(double resistance)
    {
        // Regla legacy: rango seguro operativo para no aprobar valores extremos.
        return resistance <= 20000;
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Validador legacy inicializado.");
    }
}