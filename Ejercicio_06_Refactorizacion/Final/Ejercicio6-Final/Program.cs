using System;
using System.IO;
using Ejercicio6_Final.Abstractions;
using Ejercicio6_Final.Infrastructure;
using Ejercicio6_Final.Services;

IProjectRepository repository = new InMemoryProjectRepository();
INotificationService notificationService = new ConsoleNotificationService();
IReportGenerator reportGenerator = new FileReportGenerator(Path.Combine(AppContext.BaseDirectory, "Reports"));

ProjectClosingService service = new(repository, notificationService, reportGenerator);

var result = await service.CloseProjectAsync(1);

Console.WriteLine($"Proyecto {result.Summary.ProjectId} cerrado con balance final {result.Summary.FinalBalance}.");
if (!result.NotificationSent)
{
    Console.WriteLine(result.WarningMessage);
}
