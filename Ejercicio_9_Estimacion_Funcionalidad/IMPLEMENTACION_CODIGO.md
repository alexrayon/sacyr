# Implementación del Repositorio de Azure Blob y Tests de Integración

Este documento contiene la implementación real en C# de las directrices delimitadas en la Planificación Técnica y el **ADR-009**. Puedes incorporar este código en tu capa de Infraestructura y en el proyecto de Tests correspondientes.

## 1. Repositorio Core (`AzureBlobBlueprintRepository.cs`)

El siguiente código implementa el repositorio en C# usando la librería `Azure.Storage.Blobs` con soporte explícito para subidas de *Block Blobs* para optimizar RAM, operando `100% asíncrono` (extremo a extremo) y blindado con el patrón de resiliencia _Exponential Backoff & Jitter_ a través de `Polly`.

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Polly;
using Polly.Retry;

namespace Sacyr.Planos.Infrastructure
{
    /// <summary>
    /// Interfaz agnóstica de almacenamiento de planos extraída del patrón Adaptador. 
    /// </summary>
    public interface IBlueprintStorage
    {
        Task<string> UploadAsync(string blueprintId, Stream fileStream, CancellationToken cancellationToken = default);
        Task<Stream> DownloadAsync(string blobUri, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Repositorio en base al servicio Azure Blob Storage regulado por Azure AD Managed Identity y el ADR-009
    /// </summary>
    public class AzureBlobBlueprintRepository : IBlueprintStorage
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly BlobContainerClient _containerClient;
        private readonly AsyncRetryPolicy _retryPolicy;

        public AzureBlobBlueprintRepository(BlobServiceClient blobServiceClient, string containerName)
        {
            _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
            _containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            
            // POLÍTICA DE RESILIENCIA (ADR-009.4):
            // Implementación local de Polly en caso de no estar en los delegating handlers globales de HTTP.
            // Gestiona inestabilidades transitorias y límite de IOPS (Throttling - HTTP 429) de forma asíncrona.
            var jitterer = new Random();
            _retryPolicy = Policy
                .Handle<RequestFailedException>(ex => 
                    ex.Status == 429 || // Too Many Requests (Throttling proveniente de Azure Storage)
                    ex.Status == 503 || // Server Busy
                    ex.Status >= 500)   // Service Internals / Network Failures
                .WaitAndRetryAsync(5, // Se intenta 5 veces como máximo
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) // 2, 4, 8...
                                  + TimeSpan.FromMilliseconds(jitterer.Next(0, 1000))); // Backoff aleatorio extra
        }

        public async Task<string> UploadAsync(string blueprintId, Stream fileStream, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(blueprintId)) throw new ArgumentNullException(nameof(blueprintId));
            if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));

            // Retorno resiliente
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                // Convención de nomenclatura asumiendo ficheros cad .dwg o .pdf
                var blobClient = _containerClient.GetBlobClient($"{blueprintId}.dwg");
                
                // OPTIMIZACIÓN (ADR-009.1): Streaming nativo y segmentación pesada.
                // En lugar de retener en memoria el IFormFile, se sube troceado configurando MaxTransferSize.
                var options = new BlobUploadOptions
                {
                    TransferOptions = new StorageTransferOptions
                    {
                        MaximumTransferSize = 4 * 1024 * 1024 // Corta la transferencia en segmentos de 4 MB para archivos grandes
                    }
                };

                // Asíncrono puro hasta el Kernel de TCP
                await blobClient.UploadAsync(fileStream, options, cancellationToken);
                
                // Devuelve exclusivamente el metadato (URI) para ser guardado por el Repositorio de EF Core en SQL Server.
                return blobClient.Uri.ToString();
            });
        }

        public async Task<Stream> DownloadAsync(string blobUri, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(blobUri)) throw new ArgumentNullException(nameof(blobUri));

            return await _retryPolicy.ExecuteAsync(async () =>
            {
                // Se recrea el cliente utilizando la URI recuperada previamente en SQL para el metadato.
                var blobClient = new BlobClient(new Uri(blobUri), _blobServiceClient.Options);
                
                // El motor provee un Streaming continuo; no vuelca el Byte-Array saturando RAM del Container App
                var response = await blobClient.DownloadStreamingAsync(null, cancellationToken);
                return response.Value.Content; 
            });
        }
    }
}
```

---

## 2. Test de Integración con xUnit (`AzureBlobBlueprintRepositoryActivity.Tests.cs`)

Esta clase de test requiere arrancar el almacenamiento emulado local `Azurite`. Valida estrictamente el ciclo de vida del *Stream*, simulando cómo el core de SQL guardaría y recuperaría el String (la URI referencial) y su posterior resolución contra Azure.

```csharp
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Xunit;
using Sacyr.Planos.Infrastructure;

namespace Sacyr.Planos.Tests
{
    public class AzureBlobBlueprintRepositoryTests
    {
        // En un CI/CD real, Azurite estará levantado en el puerto 10000 mediante un contenedor de servicio.
        private const string AzuriteEmulatorConnectionString = "UseDevelopmentStorage=true";
        private const string ContainerName = "blueprints-test-container";

        [Fact]
        public async Task Upload_And_Download_Should_Return_Identical_Stream_Data_Using_BlobUri()
        {
            // --- ARRANGE ---
            // Inyectar el simulador de dependencias. Note la ausencia de Keys fijas, listo para Entra ID y Managed Identity en Prto.
            var blobServiceClient = new BlobServiceClient(AzuriteEmulatorConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
            
            // Garantizar la limpieza de entorno de la aserción
            await containerClient.CreateIfNotExistsAsync();

            var repository = new AzureBlobBlueprintRepository(blobServiceClient, ContainerName);
            
            // Preparar el plano en memoria (Binario simulado)
            var blueprintId = Guid.NewGuid().ToString();
            var originalBlueprintContent = "Metadatos y líneas vectoriales del CAD DWG (Sacyr Ingeniería).";
            using var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(originalBlueprintContent));

            // --- ACT ---
            
            // 1. Simulación de Fase de Escritura (Escritura en Azure y captura de URI referencial)
            string sqlBlobUriReference = await repository.UploadAsync(blueprintId, uploadStream);

            // 2. Simulación de Fase de Lectura (El caso de uso consulta la SQL interna y pide al BlobStorage el Stream)
            using var downloadStream = await repository.DownloadAsync(sqlBlobUriReference);
            using var reader = new StreamReader(downloadStream);
            var retrievedBlueprintContent = await reader.ReadToEndAsync();

            // --- ASSERT ---
            
            Assert.False(string.IsNullOrWhiteSpace(sqlBlobUriReference), "La URI no puede estar vacía.");
            Assert.Contains(blueprintId, sqlBlobUriReference);
            Assert.Equal(originalBlueprintContent, retrievedBlueprintContent); // Debe haber preservado 100% de la carga sin colisión
            
            // --- CLEANUP ---
            // Reciclaje del contenedor temporal
            await containerClient.DeleteIfExistsAsync();
        }
    }
}
```
