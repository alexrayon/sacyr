using System.Threading;
using System.Threading.Tasks;
using Ejercicio6_Final.Models;

namespace Ejercicio6_Final.Abstractions
{
    public interface INotificationService
    {
        Task SendProjectClosureAsync(ClosingSummary summary, CancellationToken cancellationToken = default);
    }
}
