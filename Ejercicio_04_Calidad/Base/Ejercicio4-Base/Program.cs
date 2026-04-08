public class RiskMarginCalculator
{
    public decimal CalculateRiskMargin(decimal budget, int type, string region, bool urgent, int complexity)
    {
        decimal margin = 0;
        if (budget > 0)
        {
            if (type == 1) // Autopistas
            {
                if (region == "EMEA")
                {
                    if (urgent)
                    {
                        if (complexity > 7) margin = budget * 0.15m;
                        else margin = budget * 0.10m;
                    }
                    else margin = budget * 0.05m;
                }
                else if (region == "LATAM")
                {
                    if (complexity > 5) margin = budget * 0.12m;
                    else margin = budget * 0.08m;
                }
            }
            else if (type == 2) // Túneles
            {
                if (complexity > 8)
                {
                    if (urgent) margin = budget * 0.25m;
                    else margin = budget * 0.20m;
                }
                else margin = budget * 0.18m;
            }
            else
            {
                if (budget > 1000000) margin = budget * 0.05m;
                else margin = budget * 0.02m;
            }
        }

        return margin;
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        var calculator = new RiskMarginCalculator();
        var margin = calculator.CalculateRiskMargin(500000m, 1, "EMEA", true, 8);
        Console.WriteLine($"Margen calculado: {margin}");
    }
}
