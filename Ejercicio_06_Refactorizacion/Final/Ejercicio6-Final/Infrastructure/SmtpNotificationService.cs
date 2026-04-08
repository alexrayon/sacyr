using System;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Ejercicio6_Final.Abstractions;
using Ejercicio6_Final.Models;

namespace Ejercicio6_Final.Infrastructure
{
    public class SmtpNotificationService : INotificationService
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _fromAddress;
        private readonly bool _enableSsl;

        public SmtpNotificationService(
            string host,
            int port,
            string fromAddress,
            bool enableSsl)
        {
            _host = string.IsNullOrWhiteSpace(host)
                ? throw new ArgumentException("El host SMTP es obligatorio.", nameof(host))
                : host;
            _fromAddress = string.IsNullOrWhiteSpace(fromAddress)
                ? throw new ArgumentException("El remitente SMTP es obligatorio.", nameof(fromAddress))
                : fromAddress;
            _port = port;
            _enableSsl = enableSsl;
        }

        public async Task SendProjectClosureAsync(ClosingSummary summary, CancellationToken cancellationToken = default)
        {
            using MailMessage message = new(
                _fromAddress,
                summary.OwnerEmail,
                "Obra cerrada",
                $"La obra {summary.ProjectId} ha sido cerrada con balance final {summary.FinalBalance}.");

            using SmtpClient smtp = new(_host, _port)
            {
                EnableSsl = _enableSsl
            };

            cancellationToken.ThrowIfCancellationRequested();
            await smtp.SendMailAsync(message, cancellationToken);
        }
    }
}
