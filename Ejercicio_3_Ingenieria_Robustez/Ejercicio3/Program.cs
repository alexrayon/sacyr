using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sacyr.Telemetry.RobustDemo
{
    public sealed class RobustTelemetryService
    {
        private readonly ConcurrentDictionary<string, byte> _processedOperationKeys;
        private readonly object _accumulatorLock = new object();
        private decimal _totalFuel;
        private long _duplicatesBlocked;

        public RobustTelemetryService(int estimatedCapacity = 1024)
        {
            _processedOperationKeys = new ConcurrentDictionary<string, byte>(Environment.ProcessorCount * 2, estimatedCapacity);
            _totalFuel = 0m;
            _duplicatesBlocked = 0;
        }

        public decimal TotalFuel
        {
            get
            {
                lock (_accumulatorLock)
                {
                    return _totalFuel;
                }
            }
        }

        public long DuplicatesBlocked => Interlocked.Read(ref _duplicatesBlocked);

        public async Task<bool> ProcessReportAsync(TelemetryReport report)
        {
            if (report is null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (string.IsNullOrWhiteSpace(report.OperationKey))
            {
                throw new ArgumentException("OperationKey is required.", nameof(report.OperationKey));
            }

            if (_processedOperationKeys.ContainsKey(report.OperationKey))
            {
                Interlocked.Increment(ref _duplicatesBlocked);
                return await Task.FromResult(false);
            }

            if (!_processedOperationKeys.TryAdd(report.OperationKey, 0))
            {
                Interlocked.Increment(ref _duplicatesBlocked);
                return await Task.FromResult(false);
            }

            lock (_accumulatorLock)
            {
                _totalFuel += report.FuelConsumed;
            }

            return await Task.FromResult(true);
        }
    }

    public sealed class TelemetryReport
    {
        public required string OperationKey { get; init; }
        public required string SensorId { get; init; }
        public decimal FuelConsumed { get; init; }
    }

    public class Program
    {
        private const int TotalReports = 1000;
        private const double DuplicateRatio = 0.20;
        private const decimal FuelPerReport = 10.0m;

        public static async Task Main()
        {
            var service = new RobustTelemetryService(estimatedCapacity: 2048);
            var reports = GenerateReports(TotalReports, DuplicateRatio);
            var expectedTotal = CalculateExpectedTotal(reports);

            var tasks = reports.Select(report => service.ProcessReportAsync(report)).ToArray();
            await Task.WhenAll(tasks);

            Console.WriteLine("--- DEMO FUNCIONAL DE TELEMETRÍA ROBUSTA ---");
            Console.WriteLine($"Total Acumulado: {service.TotalFuel:N2}L");
            Console.WriteLine($"Duplicados Bloqueados: {service.DuplicatesBlocked}");
            Console.WriteLine($"Total Esperado: {expectedTotal:N2}L");

            if (service.TotalFuel == expectedTotal)
            {
                Console.WriteLine("ÉXITO");
            }
            else
            {
                Console.WriteLine("FALLO: la cifra no es la esperada.");
            }
        }

        private static IReadOnlyList<TelemetryReport> GenerateReports(int totalReports, double duplicateRatio)
        {
            if (totalReports <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalReports), "Debe ser mayor que cero.");
            }

            if (duplicateRatio < 0 || duplicateRatio > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(duplicateRatio), "Debe estar entre 0 y 1.");
            }

            var uniqueCount = (int)Math.Round(totalReports * (1 - duplicateRatio));
            uniqueCount = Math.Max(uniqueCount, 1);
            var duplicateCount = totalReports - uniqueCount;

            var uniqueReports = new List<TelemetryReport>(uniqueCount);
            for (var index = 0; index < uniqueCount; index++)
            {
                uniqueReports.Add(new TelemetryReport
                {
                    SensorId = $"SENSOR_{index % 50 + 1:D3}",
                    OperationKey = $"SENSOR_{index % 50 + 1:D3}#{index:D5}",
                    FuelConsumed = FuelPerReport
                });
            }

            var allReports = new List<TelemetryReport>(totalReports);
            allReports.AddRange(uniqueReports);

            var random = new Random(42);
            for (var duplicateIndex = 0; duplicateIndex < duplicateCount; duplicateIndex++)
            {
                var original = uniqueReports[random.Next(uniqueReports.Count)];
                allReports.Add(new TelemetryReport
                {
                    SensorId = original.SensorId,
                    OperationKey = original.OperationKey,
                    FuelConsumed = original.FuelConsumed
                });
            }

            return allReports.OrderBy(_ => random.Next()).ToArray();
        }

        private static decimal CalculateExpectedTotal(IReadOnlyList<TelemetryReport> reports)
        {
            return reports
                .GroupBy(report => report.OperationKey)
                .Select(group => group.First().FuelConsumed)
                .Sum();
        }
    }
}

