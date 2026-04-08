using System;
using System.Threading;
using System.Threading.Tasks;
using Ejercicio6_Final.Abstractions;
using Ejercicio6_Final.Models;

namespace Ejercicio6_Final.Infrastructure
{
    public class ConsoleNotificationService : INotificationService
    {
        public Task SendProjectClosureAsync(ClosingSummary summary, CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"Notificacion enviada a {summary.OwnerEmail} para el proyecto {summary.ProjectId}.");
            return Task.CompletedTask;
        }
    }
}
