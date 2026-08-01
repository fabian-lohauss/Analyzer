using WdsfAnalyzer.Core;

namespace WdsfAnalyzer.Tests;

public sealed class WdsfAnalysisSourceTests
{
    [Theory]
    [InlineData(100.0, 100.0, 100.0)]
    [InlineData(100.0, null, 100.0)]
    [InlineData(null, 80.0, 80.0)]
    [InlineData(50.0, 100.0, 80.0)]
    public void CalculateOverallSupport_weights_only_available_stages(
        double? preliminary,
        double? final,
        double expected)
    {
        var result = WdsfAnalysisSource.CalculateOverallSupport(
            preliminary.HasValue ? (decimal)preliminary.Value : null,
            final.HasValue ? (decimal)final.Value : null);

        Assert.Equal((decimal)expected, result);
    }

    [Fact]
    public void CalculatePreliminarySupport_returns_percentage_of_dance_marks()
    {
        var danceMarks = Enumerable.Range(1, 5)
            .Select(index => new PreliminaryDanceObservation(
                "1", new DateOnly(2026, 1, 1), "Semi-final", $"Dance {index}",
                "Judge One", "A", index <= 4 ? 1m : 0m, 5m));

        var result = WdsfAnalysisSource.CalculatePreliminarySupport([], danceMarks);

        Assert.Equal(80m, result);
    }

    [Fact]
    public void CalculatePreliminarySupport_uses_five_dance_total_as_fallback()
    {
        var totals = new[]
        {
            new PreliminaryObservation(
                "1", new DateOnly(2026, 1, 1), "Semi-final", "Judge One", "A",
                5m, 5m, 0m)
        };

        var result = WdsfAnalysisSource.CalculatePreliminarySupport(totals, []);

        Assert.Equal(100m, result);
    }

    [Theory]
    [InlineData(1, 1, 100)]
    [InlineData(3, 5, 80)]
    [InlineData(3, 3, 60)]
    [InlineData(3, 1, 40)]
    [InlineData(6, 6, 0)]
    public void CalculateFinalSupport_weights_rank_by_achieved_place_deviation(
        decimal rank,
        decimal achievedPlace,
        decimal expected)
    {
        var result = WdsfAnalysisSource.CalculateFinalSupport(rank, achievedPlace, 6);

        Assert.Equal(expected, result);
    }
}