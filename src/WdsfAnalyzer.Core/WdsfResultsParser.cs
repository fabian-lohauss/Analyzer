using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace WdsfAnalyzer.Core;

public sealed partial class WdsfResultsParser
{
    public IReadOnlyList<PreliminaryObservation> Parse(
        string html,
        string athleteName,
        string? partnerName,
        string competitionId,
        DateOnly date)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var table = document.DocumentNode.SelectSingleNode("//table[contains(concat(' ', normalize-space(@class), ' '), ' results ')]");
        if (table is null)
        {
            return [];
        }

        var headerRows = table.SelectNodes("./thead/tr");
        if (headerRows is null || headerRows.Count < 2)
        {
            return [];
        }

        var targetRow = (table.SelectNodes("./tbody/tr") ?? Enumerable.Empty<HtmlNode>())
            .FirstOrDefault(row => IsTargetCouple(row, athleteName, partnerName));
        if (targetRow is null)
        {
            return [];
        }

        var values = (targetRow.SelectNodes("./td") ?? Enumerable.Empty<HtmlNode>()).Select(Text).ToList();
        var columnHeaders = (headerRows[1].SelectNodes("./th") ?? Enumerable.Empty<HtmlNode>()).ToList();
        if (values.Count < 3 || columnHeaders.Count < values.Count)
        {
            return [];
        }

        var observations = new List<PreliminaryObservation>();
        var columnOffset = 2;
        foreach (var roundHeader in headerRows[0].SelectNodes("./th[position() > 1]") ?? Enumerable.Empty<HtmlNode>())
        {
            var round = Text(roundHeader);
            var span = roundHeader.GetAttributeValue("colspan", 0);
            if (span <= 1 || columnOffset + span > values.Count)
            {
                continue;
            }

            var judgeColumns = Enumerable.Range(columnOffset, span)
                .Where(index => !columnHeaders[index].GetClasses().Contains("dSum"))
                .Where(index => decimal.TryParse(values[index], NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                .ToList();
            if (judgeColumns.Count == 0)
            {
                columnOffset += span;
                continue;
            }

            var average = judgeColumns.Average(index => decimal.Parse(values[index], CultureInfo.InvariantCulture));
            foreach (var index in judgeColumns)
            {
                var rawValue = decimal.Parse(values[index], CultureInfo.InvariantCulture);
                var publishedName = Tooltip(columnHeaders[index]);
                var judgeName = publishedName.StartsWith("Adjudicator:", StringComparison.OrdinalIgnoreCase)
                    ? publishedName["Adjudicator:".Length..].Trim()
                    : Text(columnHeaders[index]);
                observations.Add(new PreliminaryObservation(
                    competitionId,
                    date,
                    round,
                    judgeName,
                    Text(columnHeaders[index]),
                    rawValue,
                    average,
                    rawValue - average));
            }

            columnOffset += span;
        }

        return observations;
    }

    private static bool IsTargetCouple(HtmlNode row, string athleteName, string? partnerName)
    {
        var rankCell = row.SelectSingleNode("./td[contains(@class, 'rank')]");
        var publishedNames = rankCell is null ? string.Empty : Tooltip(rankCell);
        return publishedNames.Contains(athleteName, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(partnerName) || publishedNames.Contains(partnerName, StringComparison.OrdinalIgnoreCase));
    }

    private static string Text(HtmlNode node) =>
        WhitespaceRegex().Replace(WebUtility.HtmlDecode(node.InnerText), " ").Trim();

    private static string Tooltip(HtmlNode node) =>
        WebUtility.HtmlDecode(node.GetAttributeValue("title", node.GetAttributeValue("data-bs-original-title", string.Empty)));

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}