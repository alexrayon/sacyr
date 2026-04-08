using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ejercicio6_Final.Abstractions;
using Ejercicio6_Final.Models;

namespace Ejercicio6_Final.Infrastructure
{
    public class InMemoryProjectRepository : IProjectRepository
    {
        private readonly Dictionary<int, ProjectData> _projects = new()
        {
            [1] = new ProjectData
            {
                Id = 1,
                Budget = 100000m,
                Expenses = 72500m,
                OwnerEmail = "owner@sacyr.com",
                Status = "Open"
            }
        };

        public Task<ProjectData?> GetByIdAsync(int projectId, CancellationToken cancellationToken = default)
        {
            _projects.TryGetValue(projectId, out ProjectData? project);
            return Task.FromResult(project);
        }

        public Task SaveClosureAsync(
            int projectId,
            decimal finalBalance,
            DateTime closedAtUtc,
            string finalStatus,
            CancellationToken cancellationToken = default)
        {
            if (!_projects.TryGetValue(projectId, out ProjectData? project))
            {
                return Task.CompletedTask;
            }

            _projects[projectId] = new ProjectData
            {
                Id = project.Id,
                Budget = project.Budget,
                Expenses = project.Expenses,
                OwnerEmail = project.OwnerEmail,
                Status = finalStatus
            };
            return Task.CompletedTask;
        }
    }
}
