using System;
using System.Threading;
using System.Threading.Tasks;
using Ejercicio6_Final.Abstractions;
using Ejercicio6_Final.Exceptions;
using Ejercicio6_Final.Models;

namespace Ejercicio6_Final.Services
{
    public record ProjectClosingService(
        IProjectRepository ProjectRepository,
        INotificationService NotificationService,
        IReportGenerator ReportGenerator)
    {
        public async Task<ProjectClosingResult> CloseProjectAsync(int projectId, CancellationToken cancellationToken = default)
        {
            if (projectId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(projectId), "El id del proyecto debe ser mayor que cero.");
            }

            ProjectData? project = await ProjectRepository.GetByIdAsync(projectId, cancellationToken);

            if (project is null)
            {
                throw new ProjectNotFoundException(projectId);
            }

            if (string.Equals(project.Status, "Closed", StringComparison.OrdinalIgnoreCase))
            {
                throw new ProjectAlreadyClosedException(projectId);
            }

            decimal finalBalance = project.Budget - project.Expenses;
            DateTime closedAtUtc = DateTime.UtcNow;
            const string finalStatus = "Closed";

            await ProjectRepository.SaveClosureAsync(
                project.Id,
                finalBalance,
                closedAtUtc,
                finalStatus,
                cancellationToken);

            ClosingSummary summary = new(
                project.Id,
                project.OwnerEmail,
                project.Budget,
                project.Expenses,
                finalBalance,
                closedAtUtc,
                finalStatus);

            bool notificationSent = true;
            string? warningMessage = null;

            try
            {
                await NotificationService.SendProjectClosureAsync(summary, cancellationToken);
            }
            catch (Exception ex)
            {
                notificationSent = false;
                warningMessage = $"No se pudo enviar la notificacion: {ex.Message}";
            }

            await ReportGenerator.GenerateClosingReportAsync(summary, cancellationToken);

            return new ProjectClosingResult(summary, notificationSent, warningMessage);
        }
    }
}
