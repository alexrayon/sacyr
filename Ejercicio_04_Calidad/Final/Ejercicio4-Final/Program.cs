namespace Sacyr.Concesiones.RiskEngine
{
    public class RiskMarginCalculator
    {
        // Constantes que encapsulan la política financiera de la compañía
        private const decimal MarginDefault = 0.02m;
        private const decimal MarginLargeBudget = 0.05m;
        private const decimal MarginTunelStandard = 0.18m;
        private const decimal MarginTunelCritical = 0.20m;
        private const decimal MarginTunelCriticalUrgent = 0.25m;

        public decimal CalculateRiskMargin(decimal budget, int type, string region, bool urgent, int complexity)
        {
            // 1. Cláusula de Guarda inicial (Falla Rápido)
            if (budget <= 0) return 0;

            // 2. Orquestador lineal (Switch Expression C# 12)
            return type switch
            {
                1 => GetAutopistaMargin(budget, region, urgent, complexity),
                2 => GetTunelMargin(budget, urgent, complexity),
                _ => GetGenericMargin(budget)
            };
        }

        private decimal GetAutopistaMargin(decimal budget, string region, bool urgent, int complexity)
        {
            if (region == "LATAM") 
                return budget * (complexity > 5 ? 0.12m : 0.08m);

            if (region != "EMEA") return 0;

            if (!urgent) return budget * 0.05m;

            return budget * (complexity > 7 ? 0.15m : 0.10m);
        }

        private decimal GetTunelMargin(decimal budget, bool urgent, int complexity)
        {
            if (complexity <= 8) return budget * MarginTunelStandard;

            return urgent ? budget * MarginTunelCriticalUrgent : budget * MarginTunelCritical;
        }

        private decimal GetGenericMargin(decimal budget) => 
            budget > 1000000 ? budget * MarginLargeBudget : budget * MarginDefault;
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
}
