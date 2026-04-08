namespace Ejercicio6_Final.Models
{
    public class ProjectData
    {
        public int Id { get; init; }

        public decimal Budget { get; init; }

        public decimal Expenses { get; init; }

        public string OwnerEmail { get; init; } = string.Empty;

        public string Status { get; init; } = "Open";
    }
}
