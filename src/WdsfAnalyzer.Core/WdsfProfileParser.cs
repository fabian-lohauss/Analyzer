using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace WdsfAnalyzer.Core;

public sealed partial class WdsfProfileParser
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    public CoupleAnalysis Parse(string html, Uri profileUrl, string expectedMin, bool refreshed)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var actualMin = Definition(document, "Member Id number (MIN)");
        if (!string.Equals(actualMin, expectedMin, StringComparison.Ordinal))
        {
            throw new WdsfDataException("The WDSF profile did not match the requested MIN.");
        }

        var idMatch = ProfileIdRegex().Match(profileUrl.AbsolutePath);
        if (!idMatch.Success || !Guid.TryParse(idMatch.Groups[1].Value, out var athleteId))
        {
            throw new WdsfDataException("The WDSF athlete profile has no canonical identity.");
        }

        var athlete = new AthleteProfile(
            athleteId,
            actualMin,
            Text(document.DocumentNode.SelectSingleNode("//main//h1") ?? document.DocumentNode.SelectSingleNode("//h1")),
            Definition(document, "Represents"),
            Definition(document, "Current age group"),
            profileUrl);

        var partnership = ParsePartnership(document);
        var allCompetitions = ParseCompetitions(document).ToList();
        var included = allCompetitions
            .Where(competition => competition.Date >= new DateOnly(2024, 1, 1))
            .Where(competition => !IsExcluded(competition.Status))
            .OrderByDescending(competition => competition.Date)
            .ToList();

        return new CoupleAnalysis(
            athlete,
            partnership,
            included,
            allCompetitions.Count - included.Count,
            DateTimeOffset.UtcNow,
            refreshed);
    }

    private static Partnership? ParsePartnership(HtmlDocument document)
    {
        foreach (var row in document.DocumentNode.SelectNodes("//tr") ?? Enumerable.Empty<HtmlNode>())
        {
            var cells = Cells(row);
            if (cells.Count < 6 || !cells.Any(cell => cell.Equals("Active", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            DateOnly? joined = DateOnly.TryParse(cells[5], English, DateTimeStyles.None, out var parsed) ? parsed : null;
            return new Partnership(cells[1], cells[2], joined, cells[4]);
        }

        return null;
    }

    private static IEnumerable<CompetitionEntry> ParseCompetitions(HtmlDocument document)
    {
        foreach (var row in document.DocumentNode.SelectNodes("//tr") ?? Enumerable.Empty<HtmlNode>())
        {
            var anchor = row.SelectSingleNode(".//a[contains(@href, '/Competitions/Ranking/')]");
            var cells = Cells(row);
            if (anchor is null || cells.Count < 7 || !DateOnly.TryParse(cells[2], English, DateTimeStyles.None, out var date))
            {
                continue;
            }

            var rankingUrl = new Uri($"https://www.worlddancesport.org{WebUtility.HtmlDecode(anchor.GetAttributeValue("href", ""))}");
            var slug = rankingUrl.AbsolutePath[(rankingUrl.AbsolutePath.LastIndexOf('/') + 1)..];
            var id = slug[(slug.LastIndexOf('-') + 1)..];
            var status = cells[0].Trim().TrimEnd('.');
            var placementText = status.Split('-', StringSplitOptions.TrimEntries)[0].TrimEnd('.');
            int? placement = int.TryParse(placementText, out var parsedPlacement) ? parsedPlacement : null;
            int? points = int.TryParse(cells[1], out var parsedPoints) ? parsedPoints : null;

            yield return new CompetitionEntry(
                id,
                date,
                status,
                placement,
                points,
                cells[3],
                cells[4],
                cells[5],
                cells[6],
                rankingUrl,
                ReplaceRoute(rankingUrl, "Results"),
                ReplaceRoute(rankingUrl, "Marks"),
                ReplaceRoute(rankingUrl, "Final"),
                ReplaceRoute(rankingUrl, "Scores"));
        }
    }

    private static bool IsExcluded(string status) =>
        status.Equals("Registered", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("Excused", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("Noshow", StringComparison.OrdinalIgnoreCase);

    private static Uri ReplaceRoute(Uri rankingUrl, string route) =>
        new(rankingUrl.AbsoluteUri.Replace("/Competitions/Ranking/", $"/Competitions/{route}/", StringComparison.Ordinal));

    private static List<string> Cells(HtmlNode row) =>
        (row.SelectNodes("./th|./td") ?? Enumerable.Empty<HtmlNode>()).Select(Text).ToList();

    private static string Definition(HtmlDocument document, string term)
    {
        var termNode = (document.DocumentNode.SelectNodes("//dt") ?? Enumerable.Empty<HtmlNode>())
            .FirstOrDefault(node => Text(node).Equals(term, StringComparison.OrdinalIgnoreCase));
        return termNode?.SelectSingleNode("following-sibling::dd[1]") is { } value ? Text(value) : string.Empty;
    }

    private static string Text(HtmlNode? node) => node is null
        ? string.Empty
        : WhitespaceRegex().Replace(WebUtility.HtmlDecode(node.InnerText), " ").Trim();

    [GeneratedRegex(@"([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$", RegexOptions.IgnoreCase)]
    private static partial Regex ProfileIdRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}