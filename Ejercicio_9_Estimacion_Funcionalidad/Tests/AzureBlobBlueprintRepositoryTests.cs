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
        private const string AzuriteEmulatorConnectionString = "UseDevelopmentStorage=true";
        private const string ContainerName = "blueprints-test-container";

        [Fact]
        public async Task Upload_And_Download_Should_Return_Identical_Stream_Data_Using_BlobUri()
        {
            // Arrange
            var blobServiceClient = new BlobServiceClient(AzuriteEmulatorConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
            
            await containerClient.CreateIfNotExistsAsync();

            var repository = new AzureBlobBlueprintRepository(blobServiceClient, ContainerName);
            
            var blueprintId = Guid.NewGuid().ToString();
            var originalBlueprintContent = "Metadatos y líneas vectoriales del CAD DWG (Sacyr Ingeniería).";
            using var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(originalBlueprintContent));

            // Act - Upload
            string sqlBlobUriReference = await repository.UploadAsync(blueprintId, uploadStream);

            // Act - Download
            using var downloadStream = await repository.DownloadAsync(sqlBlobUriReference);
            using var reader = new StreamReader(downloadStream);
            var retrievedBlueprintContent = await reader.ReadToEndAsync();

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(sqlBlobUriReference), "La URI no puede estar vacía.");
            Assert.Contains(blueprintId, sqlBlobUriReference);
            Assert.Equal(originalBlueprintContent, retrievedBlueprintContent);
            
            // CleanUp
            await containerClient.DeleteIfExistsAsync();
        }
    }
}
