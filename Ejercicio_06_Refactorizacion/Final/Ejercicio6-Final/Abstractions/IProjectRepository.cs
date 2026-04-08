using System;
using System.Threading;
using System.Threading.Tasks;
using Ejercicio6_Final.Models;

namespace Ejercicio6_Final.Abstractions
{
    public interface IProjectRepository
    {
        Task<ProjectData?> GetByIdAsync(int projectId, CancellationToken cancellationToken = default);

        Task SaveClosureAsync(
            int projectId,
            decimal finalBalance,
            DateTime closedAtUtc,
            string finalStatus,
            CancellationToken cancellationToken = default);
    }
}
