using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace WdsfAnalyzer.Core;

public sealed partial class WdsfScoresParser
{
    public IReadOnlyList<AbsoluteFinalObservation> Parse(
        string html,
        string athleteName,
        string? partnerName,
        string competitionId,
        DateOnly date)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var observations = new List<AbsoluteFinalObservation>();

        foreach (var table in document.DocumentNode.SelectNodes("//table[contains(concat(' ', normalize-space(@class), ' '), ' scores ')]") ?? Enumerable.Empty<HtmlNode>())
        {
            var targetRow = (table.SelectNodes("./tbody/tr") ?? Enumerable.Empty<HtmlNode>())
                .FirstOrDefault(row => IsTargetCouple(row, athleteName, partnerName));
            var finalistCount = (table.SelectNodes("./tbody/tr") ?? Enumerable.Empty<HtmlNode>()).Count();
            var headerRow = table.SelectSingleNode("./thead/tr");
            if (targetRow is null || headerRow is null)
            {
                continue;
            }

            var cells = (targetRow.SelectNodes("./td") ?? Enumerable.Empty<HtmlNode>()).ToList();
            var placeOffset = ColumnOffset(headerRow, header => header.GetClasses().Contains("place"));
            if (placeOffset < 0 || placeOffset >= cells.Count || !TryNumber(Text(cells[placeOffset]), out var achievedPlace))
            {
                continue;
            }

            var dance = Text(table.SelectSingleNode("preceding::h4[1]"));
            var scores = new List<(string Name, string Letter, decimal Score, decimal? Rank)>();
            var columnOffset = 0;
            foreach (var header in headerRow.SelectNodes("./th") ?? Enumerable.Empty<HtmlNode>())
            {
                var span = header.GetAttributeValue("colspan", 1);
                if (header.GetClasses().Contains("ajud") && columnOffset < cells.Count &&
                    TryScore(cells[columnOffset], out var score, out var rank))
                {
                    scores.Add((Tooltip(header), Text(header), score, rank));
                }

                columnOffset += span;
            }

            if (scores.Count == 0)
            {
                continue;
            }

            var average = scores.Average(score => score.Score);
            observations.AddRange(scores.Where(score => score.Rank.HasValue).Select(score => new AbsoluteFinalObservation(
                competitionId,
                date,
                dance,
                score.Name,
                score.Letter,
                score.Score,
                average,
                achievedPlace - score.Rank!.Value,
                score.Rank,
                achievedPlace,
                finalistCount)));
        }

        return observations;
    }

    private static int ColumnOffset(HtmlNode headerRow, Func<HtmlNode, bool> predicate)
    {
        var offset = 0;
        foreach (var header in headerRow.SelectNodes("./th") ?? Enumerable.Empty<HtmlNode>())
        {
            if (predicate(header))
            {
                return offset;
            }

            offset += header.GetAttributeValue("colspan", 1);
        }

        return -1;
    }

    private static bool TryScore(HtmlNode cell, out decimal score, out decimal? rank)
    {
        score = 0;
        var match = ScoreRegex().Match(cell.InnerHtml);
        if (!match.Success || !decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out score))
        {
            rank = null;
            return false;
        }

        rank = decimal.TryParse(match.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedRank)
            ? parsedRank
            : null;
        return true;
    }

    private static bool IsTargetCouple(HtmlNode row, string athleteName, string? partnerName)
    {
        var coupleCell = row.SelectSingleNode("./td[contains(concat(' ', normalize-space(@class), ' '), ' number ')]");
        var names = coupleCell is null ? string.Empty : Tooltip(coupleCell);
        return names.Contains(athleteName, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(partnerName) || names.Contains(partnerName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryNumber(string value, out decimal number) =>
        decimal.TryParse(value.Trim().TrimEnd('.'), NumberStyles.Number, CultureInfo.InvariantCulture, out number);

    private static string Text(HtmlNode? node) => node is null
        ? string.Empty
        : WhitespaceRegex().Replace(WebUtility.HtmlDecode(node.InnerText), " ").Trim();

    private static string Tooltip(HtmlNode node) =>
        WebUtility.HtmlDecode(node.GetAttributeValue("title", node.GetAttributeValue("data-bs-original-title", string.Empty)));

    [GeneratedRegex(@"^\s*([0-9]+(?:\.[0-9]+)?)\s*<br\s*/?>\s*([0-9]+(?:\.[0-9]+)?)?", RegexOptions.IgnoreCase)]
    private static partial Regex ScoreRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}