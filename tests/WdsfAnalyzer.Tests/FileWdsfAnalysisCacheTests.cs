using WdsfAnalyzer.Core;

namespace WdsfAnalyzer.Tests;

public sealed class FileWdsfAnalysisCacheTests
{
    [Fact]
    public async Task SetAsync_stores_one_couple_snapshot_with_two_min_aliases()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wdsf-analysis-{Guid.NewGuid():N}");
        try
        {
            var cache = new FileWdsfAnalysisCache(directory);
            var analysis = new CoupleAnalysis(
                new AthleteProfile(Guid.NewGuid(), "10006615", "Fabian Lohauss", "Germany", "Senior III", new Uri("https://example.test/athlete")),
                new Partnership(
                    "Simone Braunschweig",
                    "Germany",
                    null,
                    "Active",
                    new Uri("https://example.test/couple"),
                    "10006615",
                    "10023815"),
                [],
                0,
                DateTimeOffset.UtcNow,
                false,
                [new JudgeSummary(
                    "Test Judge",
                    1,
                    1,
                    0,
                    1,
                    80m,
                    null,
                    null,
                    80m,
                    0.25m,
                    0.1m,
                    new Dictionary<string, JudgeCompetitionValue>
                    {
                        ["64793"] = new(
                            "64793",
                            80m,
                            null,
                            1,
                            [new JudgeRoundDetail("Semi-final", "Waltz", 5m, 1m, JudgeValueKind.Preliminary, true)])
                    })]);

            await cache.SetAsync(analysis);
            var fromMan = await cache.GetAsync("10006615");
            var fromLady = await cache.GetAsync("10023815");

            Assert.Equal("10006615-10023815", fromMan?.Partnership?.CacheKey);
            Assert.Equal(fromMan?.Athlete, fromLady?.Athlete);
            Assert.Equal(fromMan?.Partnership, fromLady?.Partnership);
            Assert.Empty(fromMan?.Competitions ?? []);
            Assert.Empty(fromLady?.Competitions ?? []);
            Assert.Equal("Semi-final", fromMan?.Judges?.Single().CompetitionValues["64793"].Details.Single().Round);
            Assert.True(File.Exists(Path.Combine(directory, "analyses", "10006615-10023815.json")));
            Assert.Equal("10006615-10023815", await File.ReadAllTextAsync(Path.Combine(directory, "analysis-aliases", "10006615.txt")));
            Assert.Equal("10006615-10023815", await File.ReadAllTextAsync(Path.Combine(directory, "analysis-aliases", "10023815.txt")));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}