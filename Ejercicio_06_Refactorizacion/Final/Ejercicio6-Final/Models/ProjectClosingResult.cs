namespace Ejercicio6_Final.Models
{
    public sealed record ProjectClosingResult(
        ClosingSummary Summary,
        bool NotificationSent,
        string? WarningMessage);
}
