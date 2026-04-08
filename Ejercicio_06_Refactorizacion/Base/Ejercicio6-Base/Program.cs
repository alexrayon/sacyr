using System.Globalization;

public class ProjectManager
{
    public void CloseProject(int projectId)
    {
        // 1. Lógica de Negocio: Liquidación
        var project = Database.Query($"SELECT * FROM Projects WHERE Id = {projectId}");
        decimal balance = project.Budget - project.Expenses;

        // 2. Acceso a Datos: Persistencia
        Database.Execute($"UPDATE Projects SET Status = 'Closed', FinalBalance = {balance.ToString(CultureInfo.InvariantCulture)} WHERE Id = {projectId}");

        // 3. Infraestructura: Notificaciones
        NotificationGateway.Send(project.OwnerEmail, "Obra Cerrada", $"El balance final es {balance}.");

        // 4. Reportes: Generación de Documentación
        string reportsDirectory = Path.Combine(AppContext.BaseDirectory, "Reports");
        Directory.CreateDirectory(reportsDirectory);
        File.WriteAllText(Path.Combine(reportsDirectory, $"Project_{projectId}.txt"), $"Resumen de Cierre: {balance}");

        Console.WriteLine("Proyecto cerrado y notificado.");
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        var manager = new ProjectManager();
        manager.CloseProject(1);
    }
}

public static class Database
{
    private static readonly Dictionary<int, ProjectRecord> Projects = new()
    {
        [1] = new ProjectRecord
        {
            Id = 1,
            Budget = 100000m,
            Expenses = 72500m,
            OwnerEmail = "owner@sacyr.com",
            Status = "Open"
        }
    };

    public static ProjectRecord Query(string sql)
    {
        int projectId = ExtractProjectId(sql);
        if (!Projects.TryGetValue(projectId, out var project))
        {
            throw new InvalidOperationException($"Proyecto {projectId} no encontrado.");
        }

        return project;
    }

    public static void Execute(string sql)
    {
        int projectId = ExtractProjectId(sql);
        if (!Projects.TryGetValue(projectId, out var project))
        {
            throw new InvalidOperationException($"Proyecto {projectId} no encontrado.");
        }

        project.Status = "Closed";
        project.FinalBalance = project.Budget - project.Expenses;
    }

    private static int ExtractProjectId(string sql)
    {
        const string marker = "Id = ";
        int markerIndex = sql.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            throw new InvalidOperationException("No se pudo extraer el Id del proyecto.");
        }

        string idText = sql[(markerIndex + marker.Length)..].Trim();
        if (!int.TryParse(idText, out int projectId))
        {
            throw new InvalidOperationException("El Id del proyecto no es valido.");
        }

        return projectId;
    }
}

public static class NotificationGateway
{
    public static void Send(string recipient, string subject, string body)
    {
        Console.WriteLine($"Notificacion enviada a {recipient}: {subject} - {body}");
    }
}

public class ProjectRecord
{
    public int Id { get; set; }
    public decimal Budget { get; set; }
    public decimal Expenses { get; set; }
    public string OwnerEmail { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public decimal FinalBalance { get; set; }
}
