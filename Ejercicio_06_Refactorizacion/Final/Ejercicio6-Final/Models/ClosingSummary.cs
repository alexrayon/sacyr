using System;

namespace Ejercicio6_Final.Models
{
    public sealed record ClosingSummary(
        int ProjectId,
        string OwnerEmail,
        decimal Budget,
        decimal Expenses,
        decimal FinalBalance,
        DateTime ClosedAtUtc,
        string FinalStatus);
}
