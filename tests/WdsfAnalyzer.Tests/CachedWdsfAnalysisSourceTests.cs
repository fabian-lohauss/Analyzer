using WdsfAnalyzer.Core;

namespace WdsfAnalyzer.Tests;

public sealed class CachedWdsfAnalysisSourceTests
{
    private static readonly DateOnly CoverageStart = new(2024, 8, 1);

    [Fact]
    public async Task LoadAsync_reuses_completed_analysis_until_refresh()
    {
        var source = new StubAnalysisSource((min, coverageStart, refresh) => Task.FromResult(CreateAnalysis(min, coverageStart, refresh)));
        var persistent = new StubAnalysisCache();
        var cached = new CachedWdsfAnalysisSource(source, persistent);

        var first = await cached.LoadAsync("10006615", CoverageStart, false);
        var second = await cached.LoadAsync("10023815", CoverageStart, false);

        Assert.Same(first, second);
        Assert.Equal(1, source.CallCount);
        Assert.Equal("10006615-10023815", persistent.LastKey);
    }

    [Fact]
    public async Task LoadAsync_reuses_persisted_couple_from_either_partner()
    {
        var persistent = new StubAnalysisCache();
        var firstSource = new StubAnalysisSource((min, coverageStart, refresh) => Task.FromResult(CreateAnalysis(min, coverageStart, refresh)));
        var firstCache = new CachedWdsfAnalysisSource(firstSource, persistent);
        var original = await firstCache.LoadAsync("10006615", CoverageStart, false);
        var secondSource = new StubAnalysisSource((min, coverageStart, refresh) => Task.FromResult(CreateAnalysis(min, coverageStart, refresh)));
        var secondCache = new CachedWdsfAnalysisSource(secondSource, persistent);

        var restored = await secondCache.LoadAsync("10023815", CoverageStart, false);

        Assert.Same(original, restored);
        Assert.Equal(0, secondSource.CallCount);
    }

    [Fact]
    public async Task LoadAsync_refreshes_and_replaces_completed_analysis()
    {
        var source = new StubAnalysisSource((min, coverageStart, refresh) => Task.FromResult(CreateAnalysis(min, coverageStart, refresh)));
        var cached = new CachedWdsfAnalysisSource(source, new StubAnalysisCache());
        var original = await cached.LoadAsync("10006615", CoverageStart, false);

        var refreshed = await cached.LoadAsync("10006615", CoverageStart, true);
        var repeated = await cached.LoadAsync("10006615", CoverageStart, false);

        Assert.NotSame(original, refreshed);
        Assert.True(refreshed.Refreshed);
        Assert.Same(refreshed, repeated);
        Assert.Equal(2, source.CallCount);
    }

    [Fact]
    public async Task LoadAsync_failed_refresh_preserves_completed_analysis()
    {
        var source = new StubAnalysisSource((min, coverageStart, refresh) => refresh
            ? Task.FromException<CoupleAnalysis>(new HttpRequestException("Unavailable"))
            : Task.FromResult(CreateAnalysis(min, coverageStart, false)));
        var cached = new CachedWdsfAnalysisSource(source, new StubAnalysisCache());
        var original = await cached.LoadAsync("10006615", CoverageStart, false);

        await Assert.ThrowsAsync<HttpRequestException>(() => cached.LoadAsync("10006615", CoverageStart, true));
        var repeated = await cached.LoadAsync("10006615", CoverageStart, false);

        Assert.Same(original, repeated);
        Assert.Equal(2, source.CallCount);
    }

    [Fact]
    public async Task LoadAsync_coalesces_concurrent_requests_for_same_min()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new StubAnalysisSource(async (min, coverageStart, refresh) =>
        {
            started.SetResult();
            await release.Task;
            return CreateAnalysis(min, coverageStart, refresh);
        });
        var cached = new CachedWdsfAnalysisSource(source, new StubAnalysisCache());

        var firstTask = cached.LoadAsync("10006615", CoverageStart, false);
        await started.Task;
        var secondTask = cached.LoadAsync("10006615", CoverageStart, false);
        release.SetResult();
        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.Same(results[0], results[1]);
        Assert.Equal(1, source.CallCount);
    }

    [Fact]
    public async Task LoadAsync_replaces_snapshot_when_coverage_month_changes()
    {
        var source = new StubAnalysisSource((min, coverageStart, refresh) => Task.FromResult(CreateAnalysis(min, coverageStart, refresh)));
        var cached = new CachedWdsfAnalysisSource(source, new StubAnalysisCache());
        var laterCoverage = new DateOnly(2025, 8, 1);

        await cached.LoadAsync("10006615", CoverageStart, false);
        var changed = await cached.LoadAsync("10006615", laterCoverage, false);
        var repeated = await cached.LoadAsync("10023815", laterCoverage, false);

        Assert.Equal(laterCoverage, changed.CoverageStart);
        Assert.Same(changed, repeated);
        Assert.Equal(2, source.CallCount);
    }

    private static CoupleAnalysis CreateAnalysis(string min, DateOnly coverageStart, bool refreshed) => new(
        new AthleteProfile(Guid.NewGuid(), min, "Test Athlete", "Germany", "Senior III", new Uri("https://example.test/athlete")),
        new Partnership(
            "Test Partner",
            "Germany",
            null,
            "Active",
            new Uri("https://example.test/couple"),
            "10006615",
            "10023815"),
        [],
        0,
        coverageStart,
        DateTimeOffset.UtcNow,
        refreshed,
        []);

    private sealed class StubAnalysisSource(Func<string, DateOnly, bool, Task<CoupleAnalysis>> load) : IWdsfAnalysisSource
    {
        private int callCount;

        public int CallCount => callCount;

        public Task<CoupleAnalysis> LoadAsync(string min, DateOnly coverageStart, bool refresh, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref callCount);
            return load(min, coverageStart, refresh);
        }
    }

    private sealed class StubAnalysisCache : IWdsfAnalysisCache
    {
        private readonly Dictionary<string, CoupleAnalysis> analyses = new(StringComparer.Ordinal);

        public string? LastKey { get; private set; }

        public Task<CoupleAnalysis?> GetAsync(string min, CancellationToken cancellationToken = default) =>
            Task.FromResult(analyses.GetValueOrDefault(min));

        public Task SetAsync(CoupleAnalysis analysis, CancellationToken cancellationToken = default)
        {
            var partnership = Assert.IsType<Partnership>(analysis.Partnership);
            LastKey = partnership.CacheKey;
            analyses[partnership.ManMin!] = analysis;
            analyses[partnership.LadyMin!] = analysis;
            return Task.CompletedTask;
        }
    }
}