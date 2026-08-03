using System.Collections.Concurrent;

namespace WdsfAnalyzer.Core;

public sealed class CachedWdsfAnalysisSource(
    IWdsfAnalysisSource inner,
    IWdsfAnalysisCache persistentCache) : IWdsfAnalysisSource
{
    private readonly ConcurrentDictionary<string, CoupleAnalysis> cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> aliases = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new(StringComparer.Ordinal);

    public async Task<CoupleAnalysis> LoadAsync(
        string min,
        DateOnly coverageStart,
        bool refresh,
        CancellationToken cancellationToken = default)
    {
        if (!refresh && TryGet(min, coverageStart, out var cached))
        {
            return cached;
        }

        var gate = gates.GetOrAdd(min, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!refresh && TryGet(min, coverageStart, out cached))
            {
                return cached;
            }

            if (!refresh && await persistentCache.GetAsync(min, cancellationToken) is { } persisted && persisted.CoverageStart == coverageStart)
            {
                Remember(persisted);
                return persisted;
            }

            var analysis = await inner.LoadAsync(min, coverageStart, refresh, cancellationToken);
            await persistentCache.SetAsync(analysis, cancellationToken);
            Remember(analysis);
            return analysis;
        }
        finally
        {
            gate.Release();
        }
    }

    private bool TryGet(string min, DateOnly coverageStart, out CoupleAnalysis analysis)
    {
        if (aliases.TryGetValue(min, out var coupleKey) &&
            cache.TryGetValue(coupleKey, out analysis!) &&
            analysis.CoverageStart == coverageStart)
        {
            return true;
        }

        analysis = null!;
        return false;
    }

    private void Remember(CoupleAnalysis analysis)
    {
        if (analysis.Partnership is not { CacheKey: { } coupleKey, ManMin: { } manMin, LadyMin: { } ladyMin })
        {
            return;
        }

        cache[coupleKey] = analysis;
        aliases[manMin] = coupleKey;
        aliases[ladyMin] = coupleKey;
    }
}
