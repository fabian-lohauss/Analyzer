using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace WdsfAnalyzer.Core;

public sealed partial class WdsfAnalysisSource(
    HttpClient httpClient,
    WdsfProfileParser parser,
    WdsfResultsParser resultsParser,
    WdsfMarksParser marksParser,
    WdsfFinalParser finalParser,
    WdsfScoresParser scoresParser,
    WdsfCoupleParser coupleParser,
    IWdsfPageCache pageCache) : IWdsfAnalysisSource
{
    private static readonly Uri BaseUri = new("https://www.worlddancesport.org");

    public async Task<CoupleAnalysis> LoadAsync(string min, bool refresh, CancellationToken cancellationToken = default)
    {
        if (!MinRegex().IsMatch(min))
        {
            throw new WdsfDataException("Enter an eight-digit WDSF MIN.");
        }

        var profileUrl = await ResolveProfileUrlAsync(min, cancellationToken);
        var html = await GetCachedPageAsync(profileUrl, refresh, cancellationToken);
        var analysis = parser.Parse(html, profileUrl, min, refresh);
        if (analysis.Partnership is { } partnership)
        {
            var coupleHtml = await GetCachedPageAsync(partnership.CoupleUrl, refresh, cancellationToken);
            analysis = analysis with { Partnership = coupleParser.Parse(coupleHtml, partnership) };
        }
        var observations = await LoadObservationsAsync(analysis, cancellationToken);
        return analysis with { Judges = Summarize(observations) };
    }

    private async Task<AnalysisObservations> LoadObservationsAsync(
        CoupleAnalysis analysis,
        CancellationToken cancellationToken)
    {
        using var concurrency = new SemaphoreSlim(6);
        var tasks = analysis.Competitions.Select(async competition =>
        {
            await concurrency.WaitAsync(cancellationToken);
            try
            {
                var preliminaryTask = TryParsePageAsync(
                    competition.ResultsUrl,
                    html => resultsParser.Parse(html, analysis.Athlete.Name, analysis.Partnership?.PartnerName, competition.Id, competition.Date));
                var danceMarksTask = TryParsePageAsync(
                    competition.MarksUrl,
                    html => marksParser.Parse(html, analysis.Athlete.Name, analysis.Partnership?.PartnerName, competition.Id, competition.Date));
                var preliminary = await preliminaryTask;
                var danceMarks = await danceMarksTask;
                if (competition.Placement is null or > 6)
                {
                    return new CompetitionObservations(preliminary, danceMarks, [], []);
                }

                var absolute = await TryParsePageAsync(
                    competition.ScoresUrl,
                    html => scoresParser.Parse(html, analysis.Athlete.Name, analysis.Partnership?.PartnerName, competition.Id, competition.Date));
                if (absolute.Count > 0)
                {
                    return new CompetitionObservations(preliminary, danceMarks, [], absolute);
                }

                var relative = await TryParsePageAsync(
                    competition.FinalUrl,
                    html => finalParser.Parse(html, analysis.Athlete.Name, analysis.Partnership?.PartnerName, competition.Id, competition.Date));
                return new CompetitionObservations(preliminary, danceMarks, relative, []);
            }
            finally
            {
                concurrency.Release();
            }

            async Task<IReadOnlyList<T>> TryParsePageAsync<T>(Uri url, Func<string, IReadOnlyList<T>> parse)
            {
                try
                {
                    using var pageTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    pageTimeout.CancelAfter(TimeSpan.FromSeconds(12));
                    var html = await GetCachedPageAsync(url, false, pageTimeout.Token);
                    return parse(html);
                }
                catch (HttpRequestException)
                {
                    return [];
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return [];
                }
            }
        });

        var results = await Task.WhenAll(tasks);
        return new AnalysisObservations(
            results.SelectMany(result => result.Preliminary).ToList(),
            results.SelectMany(result => result.PreliminaryDance).ToList(),
            results.SelectMany(result => result.RelativeFinal).ToList(),
            results.SelectMany(result => result.AbsoluteFinal).ToList());
    }

    private static IReadOnlyList<JudgeSummary> Summarize(AnalysisObservations observations)
    {
        var decisiveRounds = observations.Preliminary
            .GroupBy(mark => mark.CompetitionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                competition => competition.Key,
                competition => competition.OrderByDescending(mark => RoundOrder(mark.Round)).First().Round,
                StringComparer.OrdinalIgnoreCase);
        var decisivePreliminary = observations.Preliminary
            .Where(mark => decisiveRounds.TryGetValue(mark.CompetitionId, out var round) && mark.Round.Equals(round, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var names = decisivePreliminary.Select(mark => mark.JudgeName)
            .Concat(observations.RelativeFinal.Select(mark => mark.JudgeName))
            .Concat(observations.AbsoluteFinal.Select(mark => mark.JudgeName))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var summaries = names.Select(name =>
            {
                var preliminary = observations.Preliminary.Where(mark => mark.JudgeName.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
                var scoredPreliminary = decisivePreliminary.Where(mark => mark.JudgeName.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
                var preliminaryDance = observations.PreliminaryDance.Where(mark => mark.JudgeName.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
                var relative = observations.RelativeFinal.Where(mark => mark.JudgeName.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
                var absolute = observations.AbsoluteFinal.Where(mark => mark.JudgeName.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
                var competitionIds = scoredPreliminary.Select(mark => mark.CompetitionId)
                    .Concat(relative.Select(mark => mark.CompetitionId))
                    .Concat(absolute.Select(mark => mark.CompetitionId))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                var competitionValues = competitionIds.ToDictionary(competitionId => competitionId, competitionId =>
                {
                    var preliminaryMarks = preliminary.Where(mark => mark.CompetitionId.Equals(competitionId, StringComparison.OrdinalIgnoreCase)).ToList();
                    var scoredPreliminaryMarks = scoredPreliminary.Where(mark => mark.CompetitionId.Equals(competitionId, StringComparison.OrdinalIgnoreCase)).ToList();
                    var preliminaryDanceMarks = preliminaryDance.Where(mark => mark.CompetitionId.Equals(competitionId, StringComparison.OrdinalIgnoreCase)).ToList();
                    var scoredPreliminaryDanceMarks = preliminaryDanceMarks
                        .Where(mark => decisiveRounds.TryGetValue(competitionId, out var decisiveRound) && decisiveRound.Equals(mark.Round, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    var relativeMarks = relative.Where(mark => mark.CompetitionId.Equals(competitionId, StringComparison.OrdinalIgnoreCase)).ToList();
                    var absoluteMarks = absolute.Where(mark => mark.CompetitionId.Equals(competitionId, StringComparison.OrdinalIgnoreCase)).ToList();
                    var finalDeviations = relativeMarks.Select(mark => mark.Deviation).Concat(absoluteMarks.Select(mark => mark.Deviation)).ToList();
                    var finalSupports = relativeMarks.Select(mark => CalculateFinalSupport(mark.JudgeRank, mark.AchievedDancePlace, mark.FinalistCount))
                        .Concat(absoluteMarks.Select(mark => CalculateFinalSupport(mark.DerivedRank!.Value, mark.AchievedDancePlace, mark.FinalistCount)))
                        .ToList();
                    var preliminaryDetails = preliminaryDanceMarks.Count > 0
                        ? preliminaryDanceMarks.Select(mark => new JudgeRoundDetail(
                            mark.Round, mark.Dance, mark.PanelMarks, mark.RawValue, JudgeValueKind.Preliminary,
                            decisiveRounds.TryGetValue(competitionId, out var decisiveRound) && decisiveRound.Equals(mark.Round, StringComparison.OrdinalIgnoreCase)))
                        : preliminaryMarks.Select(mark => new JudgeRoundDetail(
                            mark.Round, null, mark.PanelAverage, mark.RawValue, JudgeValueKind.Preliminary,
                            decisiveRounds.TryGetValue(competitionId, out var decisiveRound) && decisiveRound.Equals(mark.Round, StringComparison.OrdinalIgnoreCase)));
                    var details = preliminaryDetails
                        .Concat(relativeMarks.Select(mark => new JudgeRoundDetail(
                            "Final", mark.Dance, mark.AchievedDancePlace, mark.JudgeRank, JudgeValueKind.RelativeFinal)))
                        .Concat(absoluteMarks.Select(mark => new JudgeRoundDetail(
                            "Final", mark.Dance, mark.AchievedDancePlace, mark.DerivedRank!.Value, JudgeValueKind.RelativeFinal)))
                        .ToList();
                    var isFinal = finalDeviations.Count > 0;
                    return new JudgeCompetitionValue(
                        competitionId,
                        scoredPreliminaryMarks.Count > 0
                            ? CalculatePreliminarySupport(scoredPreliminaryMarks, scoredPreliminaryDanceMarks)
                            : null,
                        isFinal ? finalSupports.Average() : null,
                        details.Count,
                        details,
                        isFinal ? finalDeviations.Average() : null);
                }, StringComparer.OrdinalIgnoreCase);

                var finalObservationCount = relative.Count + absolute.Count;
                var averageFinalSupport = finalObservationCount > 0
                    ? relative.Select(mark => CalculateFinalSupport(mark.JudgeRank, mark.AchievedDancePlace, mark.FinalistCount))
                        .Concat(absolute.Select(mark => CalculateFinalSupport(mark.DerivedRank!.Value, mark.AchievedDancePlace, mark.FinalistCount)))
                        .Average()
                    : (decimal?)null;
                var averageFinalDeviation = finalObservationCount > 0
                    ? (relative.Sum(mark => mark.Deviation) + absolute.Sum(mark => mark.Deviation)) / finalObservationCount
                    : (decimal?)null;
                var averagePreliminarySupport = scoredPreliminary.Count > 0
                    ? scoredPreliminary.GroupBy(mark => mark.CompetitionId, StringComparer.OrdinalIgnoreCase)
                        .Average(competition => CalculatePreliminarySupport(
                            competition,
                            preliminaryDance.Where(mark =>
                                mark.CompetitionId.Equals(competition.Key, StringComparison.OrdinalIgnoreCase) &&
                                decisiveRounds.TryGetValue(competition.Key, out var decisiveRound) &&
                                decisiveRound.Equals(mark.Round, StringComparison.OrdinalIgnoreCase))))
                    : (decimal?)null;

                return new JudgeSummary(
                    name,
                    competitionValues.Count,
                    scoredPreliminary.Select(mark => mark.CompetitionId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    relative.Select(mark => mark.CompetitionId).Concat(absolute.Select(mark => mark.CompetitionId)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    preliminary.Count + relative.Count + absolute.Count,
                    averagePreliminarySupport,
                    averageFinalSupport,
                    averageFinalDeviation,
                    0,
                    0,
                    0,
                    competitionValues);
            })
            .ToList();

        var adjustedPreliminary = summaries
            .Where(summary => summary.AveragePreliminarySupport.HasValue)
            .ToDictionary(summary => summary.Name, summary => Adjust(summary.AveragePreliminarySupport!.Value, summary.PreliminaryCompetitionCount));
        var adjustedFinal = summaries
            .Where(summary => summary.AverageFinalSupport.HasValue)
            .ToDictionary(summary => summary.Name, summary => Adjust(summary.AverageFinalSupport!.Value, summary.FinalCompetitionCount));
        var preliminaryValues = adjustedPreliminary.Values.ToList();
        var finalValues = adjustedFinal.Values.ToList();

        return summaries.Select(summary => summary with
            {
                OverallSupport = CalculateOverallSupport(
                    summary.AveragePreliminarySupport,
                    summary.AverageFinalSupport),
                OverallConfidence = CalculateOverallSupport(
                    summary.AveragePreliminarySupport.HasValue ? Confidence(summary.PreliminaryCompetitionCount) : null,
                    summary.AverageFinalSupport.HasValue ? Confidence(summary.FinalCompetitionCount) : null),
                OverallRanking =
                    (adjustedPreliminary.TryGetValue(summary.Name, out var preliminary) ? 0.4m * Confidence(summary.PreliminaryCompetitionCount) * Percentile(preliminary, preliminaryValues) : 0) +
                    (adjustedFinal.TryGetValue(summary.Name, out var final) ? 0.6m * Confidence(summary.FinalCompetitionCount) * Percentile(final, finalValues) : 0)
            })
            .OrderByDescending(summary => summary.OverallSupport)
            .ThenByDescending(summary => summary.CompetitionCount)
            .ThenBy(summary => summary.Name)
            .ToList();
    }

    internal static decimal CalculateOverallSupport(decimal? preliminary, decimal? final)
    {
        var weight = (preliminary.HasValue ? 0.4m : 0) + (final.HasValue ? 0.6m : 0);
        return weight == 0
            ? 0
            : ((preliminary ?? 0) * 0.4m + (final ?? 0) * 0.6m) / weight;
    }

    internal static decimal CalculatePreliminarySupport(
        IEnumerable<PreliminaryObservation> totals,
        IEnumerable<PreliminaryDanceObservation> danceMarks)
    {
        var dances = danceMarks.ToList();
        var percentage = dances.Count > 0
            ? 100m * dances.Average(mark => mark.RawValue)
            : 20m * totals.Average(mark => mark.RawValue);
        return Math.Clamp(percentage, 0m, 100m);
    }

    internal static decimal CalculateFinalSupport(decimal judgeRank, decimal achievedPlace, int finalistCount) =>
        finalistCount <= 1
            ? 100m
            : Math.Clamp(
                100m * (finalistCount - judgeRank) / (finalistCount - 1) +
                50m * (achievedPlace - judgeRank) / (finalistCount - 1),
                0m,
                100m);

    private static decimal Adjust(decimal average, int competitionCount) =>
        average * Confidence(competitionCount);

    private static decimal Confidence(int competitionCount) =>
        competitionCount / (competitionCount + 3m);

    private static decimal Percentile(decimal value, IReadOnlyList<decimal> values)
    {
        if (values.Count <= 1)
        {
            return 1;
        }

        var below = values.Count(candidate => candidate < value);
        var equal = values.Count(candidate => candidate == value);
        return (below + (equal - 1) / 2m) / (values.Count - 1m);
    }

    private static int RoundOrder(string round)
    {
        var digits = new string(round.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var number) ? number : 0;
    }

    private sealed record CompetitionObservations(
        IReadOnlyList<PreliminaryObservation> Preliminary,
        IReadOnlyList<PreliminaryDanceObservation> PreliminaryDance,
        IReadOnlyList<RelativeFinalObservation> RelativeFinal,
        IReadOnlyList<AbsoluteFinalObservation> AbsoluteFinal);

    private sealed record AnalysisObservations(
        IReadOnlyList<PreliminaryObservation> Preliminary,
        IReadOnlyList<PreliminaryDanceObservation> PreliminaryDance,
        IReadOnlyList<RelativeFinalObservation> RelativeFinal,
        IReadOnlyList<AbsoluteFinalObservation> AbsoluteFinal);

    private async Task<Uri> ResolveProfileUrlAsync(string min, CancellationToken cancellationToken)
    {
        using var directoryResponse = await httpClient.GetAsync(new Uri(BaseUri, "/Athletes"), cancellationToken);
        directoryResponse.EnsureSuccessStatusCode();
        var directoryHtml = await directoryResponse.Content.ReadAsStringAsync(cancellationToken);
        var tokenMatch = TokenRegex().Match(directoryHtml);
        if (!tokenMatch.Success)
        {
            throw new WdsfDataException("WDSF athlete search is temporarily unavailable.");
        }

        var request = new
        {
            PageIndex = 1,
            Name = min,
            HasActiveLicense = "Yes",
            __RequestVerificationToken = WebUtility.HtmlDecode(tokenMatch.Groups[1].Value),
            PageSize = 8
        };

        using var content = new StringContent(
            JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = null }),
            Encoding.UTF8,
            "application/json");
        using var searchRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUri, "/api/listitems/athletes"))
        {
            Content = content
        };
        searchRequest.Headers.Accept.ParseAdd("application/json");
        searchRequest.Headers.Referrer = new Uri(BaseUri, "/Athletes");
        searchRequest.Headers.Add("Origin", BaseUri.GetLeftPart(UriPartial.Authority));
        searchRequest.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var response = await httpClient.SendAsync(searchRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AthleteSearchResult>(cancellationToken: cancellationToken);
        if (result?.Items.Count != 1 || string.IsNullOrWhiteSpace(result.Items[0].Url))
        {
            throw new WdsfDataException("No unique active WDSF athlete was found for this MIN.");
        }

        return new Uri(BaseUri, result.Items[0].Url);
    }

    private async Task<string> GetCachedPageAsync(Uri url, bool refresh, CancellationToken cancellationToken)
    {
        if (!refresh && await pageCache.GetAsync(url, cancellationToken) is { } cached)
        {
            return cached;
        }

        var html = await httpClient.GetStringAsync(url, cancellationToken);
        await pageCache.SetAsync(url, html, cancellationToken);
        return html;
    }

    private sealed record AthleteSearchResult([property: JsonPropertyName("items")] List<AthleteSearchItem> Items);
    private sealed record AthleteSearchItem([property: JsonPropertyName("url")] string Url);

    [GeneratedRegex("name=[\"']__RequestVerificationToken[\"'][^>]*value=[\"']([^\"']+)", RegexOptions.IgnoreCase)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex MinRegex();
}