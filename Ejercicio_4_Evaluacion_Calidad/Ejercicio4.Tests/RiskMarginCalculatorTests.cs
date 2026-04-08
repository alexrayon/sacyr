using Xunit;

namespace Ejercicio4.Tests;

public class RiskMarginCalculatorTests
{
    [Theory]
    [MemberData(nameof(GetLegacyScenarios))]
    public void CalculateRiskMargin_MatchesLegacyImplementation(decimal budget, int type, string region, bool urgent, int complexity)
    {
        var calculator = new RiskMarginCalculator();
        var actual = calculator.CalculateRiskMargin(budget, type, region, urgent, complexity);
        var expected = LegacyCalculateRiskMargin(budget, type, region, urgent, complexity);

        Assert.Equal(expected, actual);
    }

    public static IEnumerable<object[]> GetLegacyScenarios()
    {
        yield return new object[] { 500000m, 1, "EMEA", true, 8 };
        yield return new object[] { 500000m, 1, "EMEA", true, 7 };
        yield return new object[] { 500000m, 1, "EMEA", false, 10 };
        yield return new object[] { 500000m, 1, "LATAM", true, 6 };
        yield return new object[] { 500000m, 1, "LATAM", false, 5 };
        yield return new object[] { 500000m, 2, "ANY", true, 9 };
        yield return new object[] { 500000m, 2, "ANY", false, 9 };
        yield return new object[] { 500000m, 2, "ANY", true, 8 };
        yield return new object[] { 2000000m, 3, "UNKNOWN", false, 1 };
        yield return new object[] { 500000m, 3, "UNKNOWN", false, 1 };
        yield return new object[] { -100m, 1, "EMEA", true, 10 };
        yield return new object[] { 500000m, 1, "APAC", true, 10 };
    }

    private static decimal LegacyCalculateRiskMargin(decimal budget, int type, string region, bool urgent, int complexity)
    {
        decimal margin = 0;

        if (budget > 0)
        {
            if (type == 1)
            {
                if (region == "EMEA")
                {
                    if (urgent)
                    {
                        if (complexity > 7)
                        {
                            margin = budget * 0.15m;
                        }
                        else
                        {
                            margin = budget * 0.10m;
                        }
                    }
                    else
                    {
                        margin = budget * 0.05m;
                    }
                }
                else if (region == "LATAM")
                {
                    if (complexity > 5)
                    {
                        margin = budget * 0.12m;
                    }
                    else
                    {
                        margin = budget * 0.08m;
                    }
                }
            }
            else if (type == 2)
            {
                if (complexity > 8)
                {
                    if (urgent)
                    {
                        margin = budget * 0.25m;
                    }
                    else
                    {
                        margin = budget * 0.20m;
                    }
                }
                else
                {
                    margin = budget * 0.18m;
                }
            }
            else
            {
                if (budget > 1000000)
                {
                    margin = budget * 0.05m;
                }
                else
                {
                    margin = budget * 0.02m;
                }
            }
        }

        return margin;
    }
}
