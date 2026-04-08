namespace Sacyr.Construction.ClosingProcess
{
    // 1. Abstracciones (Fase 2: Planificación)
    public interface IProjectRepository { Task<Project> GetByIdAsync(int id); Task UpdateStatusAsync(int id, decimal balance); }
    public interface INotificationService { Task SendClosingNoticeAsync(string email, decimal balance); }
    public interface IReportGenerator { Task CreateSummaryAsync(int id, decimal balance); }

    // 2. Orquestador de Negocio con Responsabilidad Única (SRP)
    public class ProjectClosingService(
        IProjectRepository repository, 
        INotificationService notifications, 
        IReportGenerator reports)
    {
        public async Task CloseProjectAsync(int projectId)
        {
            // Cláusulas de Guarda (Fase 4: Implementación limpia)
            var project = await repository.GetByIdAsync(projectId);
            if (project == null) throw new InvalidOperationException($"Proyecto {projectId} no localizado.");
            if (project.Status == "Closed") return;

            // Lógica de Negocio Financiera
            decimal balance = project.Budget - project.Expenses;

            // Coordinación de servicios (Orquestación sin detalles técnicos)
            await repository.UpdateStatusAsync(projectId, balance);
            await notifications.SendClosingNoticeAsync(project.OwnerEmail, balance);
            await reports.CreateSummaryAsync(projectId, balance);

            Console.WriteLine($"Cierre administrativo completado satisfactoriamente para obra {projectId}.");
        }
    }

    public sealed class InMemoryProjectRepository : IProjectRepository
    {
        private readonly Dictionary<int, Project> _projects = new()
        {
            [1] = new Project(1, 100000m, 72500m, "owner@sacyr.com", "Open", 0m)
        };

        public Task<Project> GetByIdAsync(int id)
        {
            _projects.TryGetValue(id, out var project);
            return Task.FromResult(project)!;
        }

        public Task UpdateStatusAsync(int id, decimal balance)
        {
            if (_projects.TryGetValue(id, out var project))
            {
                _projects[id] = project with { Status = "Closed", FinalBalance = balance };
            }

            return Task.CompletedTask;
        }
    }

    public sealed class ConsoleNotificationService : INotificationService
    {
        public Task SendClosingNoticeAsync(string email, decimal balance)
        {
            Console.WriteLine($"Notificacion enviada a {email}: balance final {balance}.");
            return Task.CompletedTask;
        }
    }

    public sealed class FileReportGenerator : IReportGenerator
    {
        public Task CreateSummaryAsync(int id, decimal balance)
        {
            string reportsDirectory = Path.Combine(AppContext.BaseDirectory, "Reports");
            Directory.CreateDirectory(reportsDirectory);
            File.WriteAllText(Path.Combine(reportsDirectory, $"Project_{id}.txt"), $"Resumen de Cierre: {balance}");
            return Task.CompletedTask;
        }
    }

    public record Project(int Id, decimal Budget, decimal Expenses, string OwnerEmail, string Status, decimal FinalBalance);

    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var repository = new InMemoryProjectRepository();
            var notifications = new ConsoleNotificationService();
            var reports = new FileReportGenerator();
            var service = new ProjectClosingService(repository, notifications, reports);

            await service.CloseProjectAsync(1);
        }
    }
}
