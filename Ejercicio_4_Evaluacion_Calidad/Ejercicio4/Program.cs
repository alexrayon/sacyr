public class RiskMarginCalculator
{
    public decimal CalculateRiskMargin(decimal budget, int type, string region, bool urgent, int complexity)
    {
        if (budget <= 0)
        {
            return 0;
        }

        return type switch
        {
            1 => CalculateHighwayMargin(budget, region, urgent, complexity),
            2 => CalculateTunnelMargin(budget, urgent, complexity),
            _ => CalculateOtherMargin(budget)
        };
    }

    private static decimal CalculateHighwayMargin(decimal budget, string region, bool urgent, int complexity)
    {
        if (region == "EMEA" && urgent && complexity > 7)
        {
            return budget * MarginRates.MarginHighwaysEMEACriticalComplexity;
        }

        if (region == "EMEA" && urgent)
        {
            return budget * MarginRates.MarginHighwaysEMEAStandardComplexity;
        }

        if (region == "EMEA")
        {
            return budget * MarginRates.MarginHighwaysEMEANonUrgent;
        }

        if (region == "LATAM" && complexity > 5)
        {
            return budget * MarginRates.MarginHighwaysLATAMHighComplexity;
        }

        if (region == "LATAM")
        {
            return budget * MarginRates.MarginHighwaysLATAMStandard;
        }

        return 0;
    }

    private static decimal CalculateTunnelMargin(decimal budget, bool urgent, int complexity)
    {
        if (complexity > 8 && urgent)
        {
            return budget * MarginRates.MarginTunnelsUrgentCriticalComplexity;
        }

        if (complexity > 8)
        {
            return budget * MarginRates.MarginTunnelsCriticalComplexity;
        }

        return budget * MarginRates.MarginTunnelsStandard;
    }

    private static decimal CalculateOtherMargin(decimal budget)
    {
        return budget > 1_000_000m
            ? budget * MarginRates.MarginOtherLargeContract
            : budget * MarginRates.MarginOtherSmallContract;
    }
}

public static class MarginRates
{
    public const decimal MarginHighwaysEMEACriticalComplexity = 0.15m;
    public const decimal MarginHighwaysEMEAStandardComplexity = 0.10m;
    public const decimal MarginHighwaysEMEANonUrgent = 0.05m;
    public const decimal MarginHighwaysLATAMHighComplexity = 0.12m;
    public const decimal MarginHighwaysLATAMStandard = 0.08m;
    public const decimal MarginTunnelsUrgentCriticalComplexity = 0.25m;
    public const decimal MarginTunnelsCriticalComplexity = 0.20m;
    public const decimal MarginTunnelsStandard = 0.18m;
    public const decimal MarginOtherLargeContract = 0.05m;
    public const decimal MarginOtherSmallContract = 0.02m;
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
