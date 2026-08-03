using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using WdsfAnalyzer.Core;

namespace WdsfAnalyzer.Web;

public sealed class BlobWdsfPageCache : IWdsfPageCache
{
    private readonly BlobContainerClient container;

    public BlobWdsfPageCache(Uri serviceUri, string containerName)
    {
        var blobService = new BlobServiceClient(serviceUri, new DefaultAzureCredential());
        container = blobService.GetBlobContainerClient(containerName);
    }

    public async Task<string?> GetAsync(Uri url, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await container.GetBlobClient(GetBlobName(url)).DownloadContentAsync(cancellationToken);
            return response.Value.Content.ToString();
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task SetAsync(Uri url, string content, CancellationToken cancellationToken = default)
    {
        await container.GetBlobClient(GetBlobName(url)).UploadAsync(
            BinaryData.FromString(content),
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "text/html; charset=utf-8"
                }
            },
            cancellationToken);
    }

    private static string GetBlobName(Uri url)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url.AbsoluteUri)));
        return $"{key}.html";
    }
}