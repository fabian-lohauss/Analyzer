using System.Text.Json;

namespace WdsfAnalyzer.Core;

public sealed class FileWdsfAnalysisCache(string cacheDirectory) : IWdsfAnalysisCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CoupleAnalysis?> GetAsync(string min, CancellationToken cancellationToken = default)
    {
        var aliasPath = AliasPath(min);
        if (!File.Exists(aliasPath))
        {
            return null;
        }

        var coupleKey = await File.ReadAllTextAsync(aliasPath, cancellationToken);
        var analysisPath = AnalysisPath(coupleKey);
        if (!File.Exists(analysisPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(analysisPath);
        return await JsonSerializer.DeserializeAsync<CoupleAnalysis>(stream, JsonOptions, cancellationToken);
    }

    public async Task SetAsync(CoupleAnalysis analysis, CancellationToken cancellationToken = default)
    {
        if (analysis.Partnership is not { CacheKey: { } coupleKey, ManMin: { } manMin, LadyMin: { } ladyMin })
        {
            return;
        }

        Directory.CreateDirectory(Path.Combine(cacheDirectory, "analyses"));
        Directory.CreateDirectory(Path.Combine(cacheDirectory, "analysis-aliases"));
        await using (var stream = File.Create(AnalysisPath(coupleKey)))
        {
            await JsonSerializer.SerializeAsync(stream, analysis, JsonOptions, cancellationToken);
        }

        await File.WriteAllTextAsync(AliasPath(manMin), coupleKey, cancellationToken);
        await File.WriteAllTextAsync(AliasPath(ladyMin), coupleKey, cancellationToken);
    }

    private string AnalysisPath(string coupleKey) => Path.Combine(cacheDirectory, "analyses", $"{coupleKey}.json");

    private string AliasPath(string min) => Path.Combine(cacheDirectory, "analysis-aliases", $"{min}.txt");
}