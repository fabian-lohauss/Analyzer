using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using WdsfAnalyzer.Core;

namespace WdsfAnalyzer.Web;

public sealed class BlobWdsfAnalysisCache(BlobContainerClient container) : IWdsfAnalysisCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CoupleAnalysis?> GetAsync(string min, CancellationToken cancellationToken = default)
    {
        try
        {
            var alias = await container.GetBlobClient(AliasName(min)).DownloadContentAsync(cancellationToken);
            var coupleKey = alias.Value.Content.ToString();
            var analysis = await container.GetBlobClient(AnalysisName(coupleKey)).DownloadContentAsync(cancellationToken);
            return analysis.Value.Content.ToObjectFromJson<CoupleAnalysis>(JsonOptions);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task SetAsync(CoupleAnalysis analysis, CancellationToken cancellationToken = default)
    {
        if (analysis.Partnership is not { CacheKey: { } coupleKey, ManMin: { } manMin, LadyMin: { } ladyMin })
        {
            return;
        }

        await UploadAsync(AnalysisName(coupleKey), BinaryData.FromObjectAsJson(analysis, JsonOptions), "application/json", cancellationToken);
        await UploadAsync(AliasName(manMin), BinaryData.FromString(coupleKey), "text/plain; charset=utf-8", cancellationToken);
        await UploadAsync(AliasName(ladyMin), BinaryData.FromString(coupleKey), "text/plain; charset=utf-8", cancellationToken);
    }

    private async Task UploadAsync(string name, BinaryData content, string contentType, CancellationToken cancellationToken) =>
        await container.GetBlobClient(name).UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            },
            cancellationToken);

    private static string AnalysisName(string coupleKey) => $"analyses/{coupleKey}.json";

    private static string AliasName(string min) => $"analysis-aliases/{min}.txt";
}