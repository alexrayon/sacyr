using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Sacyr.Infrastructure.Storage
{
    public interface IBlueprintStorage
    {
        Task<string> SaveAsync(Stream fileStream, string fileName);
    }

    public class AzureBlobBlueprintRepository(
        BlobServiceClient blobServiceClient) : IBlueprintStorage
    {
        private const string ContainerName = "planos-tecnicos";

        public async Task<string> SaveAsync(Stream fileStream, string fileName)
        {
            // Cláusula de Guarda: Validación de entrada
            if (fileStream == null || string.IsNullOrEmpty(fileName))
                throw new ArgumentException("El archivo o el nombre no son válidos.");

            try
            {
                var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
                var blobClient = containerClient.GetBlobClient(fileName);

                Console.WriteLine($"Subiendo plano {fileName} a Azure Storage...");
                
                await blobClient.UploadAsync(fileStream, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "application/pdf" }
                });

                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error critico al subir el plano {fileName} a la nube: {ex.Message}");
                throw;
            }
        }
    }

    public static class Program
    {
        public static void Main(string[] args)
        {
            string connectionString = "UseDevelopmentStorage=true";
            var blobServiceClient = new BlobServiceClient(connectionString);
            IBlueprintStorage repository = new AzureBlobBlueprintRepository(blobServiceClient);

            Console.WriteLine($"Repositorio Azure Blob preparado: {repository.GetType().Name}");
        }
    }
}
