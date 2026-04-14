using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Polly;
using Polly.Retry;

namespace Sacyr.Planos.Infrastructure
{
    public interface IBlueprintStorage
    {
        Task<string> UploadAsync(string blueprintId, Stream fileStream, CancellationToken cancellationToken = default);
        Task<Stream> DownloadAsync(string blobUri, CancellationToken cancellationToken = default);
    }

    public class AzureBlobBlueprintRepository : IBlueprintStorage
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly BlobContainerClient _containerClient;
        private readonly AsyncRetryPolicy _retryPolicy;

        public AzureBlobBlueprintRepository(BlobServiceClient blobServiceClient, string containerName)
        {
            _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
            _containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            
            var jitterer = new Random();
            _retryPolicy = Policy
                .Handle<RequestFailedException>(ex => 
                    ex.Status == 429 || 
                    ex.Status == 503 || 
                    ex.Status >= 500)   
                .WaitAndRetryAsync(5, 
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) 
                                  + TimeSpan.FromMilliseconds(jitterer.Next(0, 1000)));
        }

        public async Task<string> UploadAsync(string blueprintId, Stream fileStream, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(blueprintId)) throw new ArgumentNullException(nameof(blueprintId));
            if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));

            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var blobClient = _containerClient.GetBlobClient($"{blueprintId}.dwg");
                
                var options = new BlobUploadOptions
                {
                    TransferOptions = new StorageTransferOptions
                    {
                        MaximumTransferSize = 4 * 1024 * 1024 
                    }
                };

                await blobClient.UploadAsync(fileStream, options, cancellationToken);
                return blobClient.Uri.ToString();
            });
        }

        public async Task<Stream> DownloadAsync(string blobUri, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(blobUri)) throw new ArgumentNullException(nameof(blobUri));

            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var blobClient = new BlobClient(new Uri(blobUri));
                var response = await blobClient.DownloadStreamingAsync(null, cancellationToken);
                return response.Value.Content; 
            });
        }
    }
}
