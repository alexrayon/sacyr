using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ejercicio6_Final.Abstractions;
using Ejercicio6_Final.Models;

namespace Ejercicio6_Final.Infrastructure
{
    public class FileReportGenerator : IReportGenerator
    {
        private readonly string _reportsDirectory;

        public FileReportGenerator(string reportsDirectory)
        {
            _reportsDirectory = string.IsNullOrWhiteSpace(reportsDirectory)
                ? throw new ArgumentException("La ruta del directorio de reportes es obligatoria.", nameof(reportsDirectory))
                : reportsDirectory;
        }

        public async Task GenerateClosingReportAsync(ClosingSummary summary, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(_reportsDirectory);

            string filePath = Path.Combine(_reportsDirectory, $"Project_{summary.ProjectId}.txt");
            string content =
                $"Proyecto: {summary.ProjectId}{Environment.NewLine}" +
                $"Estado: {summary.FinalStatus}{Environment.NewLine}" +
                $"Balance final: {summary.FinalBalance}{Environment.NewLine}" +
                $"Fecha UTC: {summary.ClosedAtUtc:O}";

            await File.WriteAllTextAsync(filePath, content, cancellationToken);
        }
    }
}
