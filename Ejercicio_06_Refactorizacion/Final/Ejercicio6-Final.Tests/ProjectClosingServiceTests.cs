using System;
using System.Threading;
using System.Threading.Tasks;
using Ejercicio6_Final.Abstractions;
using Ejercicio6_Final.Exceptions;
using Ejercicio6_Final.Models;
using Ejercicio6_Final.Services;
using Moq;
using Xunit;

namespace Ejercicio6_Final.Tests
{
    public class ProjectClosingServiceTests
    {
        [Fact]
        public async Task CloseProjectAsync_Throws_WhenProjectDoesNotExist()
        {
            var repositoryMock = new Mock<IProjectRepository>();
            var notificationMock = new Mock<INotificationService>();
            var reportMock = new Mock<IReportGenerator>();

            repositoryMock
                .Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProjectData?)null);

            var service = new ProjectClosingService(repositoryMock.Object, notificationMock.Object, reportMock.Object);

            await Assert.ThrowsAsync<ProjectNotFoundException>(() => service.CloseProjectAsync(42));

            repositoryMock.Verify(r => r.SaveClosureAsync(
                It.IsAny<int>(),
                It.IsAny<decimal>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
            notificationMock.Verify(n => n.SendProjectClosureAsync(It.IsAny<ClosingSummary>(), It.IsAny<CancellationToken>()), Times.Never);
            reportMock.Verify(g => g.GenerateClosingReportAsync(It.IsAny<ClosingSummary>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CloseProjectAsync_Throws_WhenProjectAlreadyClosed()
        {
            var repositoryMock = new Mock<IProjectRepository>();
            var notificationMock = new Mock<INotificationService>();
            var reportMock = new Mock<IReportGenerator>();

            repositoryMock
                .Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProjectData
                {
                    Id = 7,
                    Budget = 100m,
                    Expenses = 75m,
                    OwnerEmail = "owner@sacyr.com",
                    Status = "Closed"
                });

            var service = new ProjectClosingService(repositoryMock.Object, notificationMock.Object, reportMock.Object);

            await Assert.ThrowsAsync<ProjectAlreadyClosedException>(() => service.CloseProjectAsync(7));

            repositoryMock.Verify(r => r.SaveClosureAsync(
                It.IsAny<int>(),
                It.IsAny<decimal>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CloseProjectAsync_Saves_Notifies_AndGeneratesReport_WhenAllOk()
        {
            var repositoryMock = new Mock<IProjectRepository>();
            var notificationMock = new Mock<INotificationService>();
            var reportMock = new Mock<IReportGenerator>();

            repositoryMock
                .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProjectData
                {
                    Id = 1,
                    Budget = 1000m,
                    Expenses = 400m,
                    OwnerEmail = "owner@sacyr.com",
                    Status = "Open"
                });

            var service = new ProjectClosingService(repositoryMock.Object, notificationMock.Object, reportMock.Object);

            ProjectClosingResult result = await service.CloseProjectAsync(1);

            Assert.True(result.NotificationSent);
            Assert.Null(result.WarningMessage);
            Assert.Equal(600m, result.Summary.FinalBalance);

            repositoryMock.Verify(r => r.SaveClosureAsync(
                1,
                600m,
                It.IsAny<DateTime>(),
                "Closed",
                It.IsAny<CancellationToken>()), Times.Once);
            notificationMock.Verify(n => n.SendProjectClosureAsync(It.IsAny<ClosingSummary>(), It.IsAny<CancellationToken>()), Times.Once);
            reportMock.Verify(g => g.GenerateClosingReportAsync(It.IsAny<ClosingSummary>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CloseProjectAsync_WhenNotificationFails_StillSavesAndGeneratesReport()
        {
            var repositoryMock = new Mock<IProjectRepository>();
            var notificationMock = new Mock<INotificationService>();
            var reportMock = new Mock<IReportGenerator>();

            repositoryMock
                .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProjectData
                {
                    Id = 1,
                    Budget = 500m,
                    Expenses = 450m,
                    OwnerEmail = "owner@sacyr.com",
                    Status = "Open"
                });

            notificationMock
                .Setup(n => n.SendProjectClosureAsync(It.IsAny<ClosingSummary>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("SMTP no disponible"));

            var service = new ProjectClosingService(repositoryMock.Object, notificationMock.Object, reportMock.Object);

            ProjectClosingResult result = await service.CloseProjectAsync(1);

            Assert.False(result.NotificationSent);
            Assert.NotNull(result.WarningMessage);
            Assert.Contains("No se pudo enviar la notificacion", result.WarningMessage);

            repositoryMock.Verify(r => r.SaveClosureAsync(
                1,
                50m,
                It.IsAny<DateTime>(),
                "Closed",
                It.IsAny<CancellationToken>()), Times.Once);
            reportMock.Verify(g => g.GenerateClosingReportAsync(It.IsAny<ClosingSummary>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
