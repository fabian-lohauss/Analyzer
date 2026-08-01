using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace WdsfAnalyzer.Core;

public sealed partial class WdsfMarksParser
{
    public IReadOnlyList<PreliminaryDanceObservation> Parse(
        string html,
        string athleteName,
        string? partnerName,
        string competitionId,
        DateOnly date)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var table = document.DocumentNode.SelectSingleNode("//table[contains(concat(' ', normalize-space(@class), ' '), ' marks ')]");
        var headerRows = table?.SelectNodes("./thead/tr");
        var bodyRows = (table?.SelectNodes("./tbody/tr") ?? Enumerable.Empty<HtmlNode>()).ToList();
        if (headerRows is null || headerRows.Count < 2)
        {
            return [];
        }

        var firstRowIndex = bodyRows.FindIndex(row => IsTargetCouple(row, athleteName, partnerName));
        if (firstRowIndex < 0)
        {
            return [];
        }

        var firstRow = bodyRows[firstRowIndex];
        var coupleCell = firstRow.SelectSingleNode("./td[contains(concat(' ', normalize-space(@class), ' '), ' number ')]")!;
        var roundCount = coupleCell.GetAttributeValue("rowspan", 1);
        var targetRows = bodyRows.Skip(firstRowIndex).Take(roundCount);
        var danceHeaders = (headerRows[0].SelectNodes("./th[contains(concat(' ', normalize-space(@class), ' '), ' adjudicator ')]") ?? Enumerable.Empty<HtmlNode>()).ToList();
        var columnHeaders = (headerRows[1].SelectNodes("./th") ?? Enumerable.Empty<HtmlNode>()).ToList();
        var observations = new List<PreliminaryDanceObservation>();

        foreach (var row in targetRows)
        {
            var cells = (row.SelectNodes("./td") ?? Enumerable.Empty<HtmlNode>()).ToList();
            var roundIndex = cells.FindIndex(cell => cell.GetClasses().Contains("round"));
            if (roundIndex < 0)
            {
                continue;
            }

            var roundCell = cells[roundIndex];
            var round = WebUtility.HtmlDecode(roundCell.GetAttributeValue("title", Text(roundCell)));
            var headerOffset = 3;
            var dataOffset = 0;
            foreach (var danceHeader in danceHeaders)
            {
                var span = danceHeader.GetAttributeValue("colspan", 0);
                var judges = columnHeaders.Skip(headerOffset).Take(span)
                    .Select((header, index) => (Header: header, Index: index))
                    .Where(item => item.Header.GetClasses().Contains("ajud"))
                    .Where(item => roundIndex + 1 + dataOffset + item.Index < cells.Count)
                    .Select(item => (item.Header, Cell: cells[roundIndex + 1 + dataOffset + item.Index]))
                    .ToList();
                if (judges.Count > 0)
                {
                    var values = judges.Select(item => IsCross(Text(item.Cell)) ? 1m : 0m).ToList();
                    var panelMarks = values.Sum();
                    for (var index = 0; index < judges.Count; index++)
                    {
                        observations.Add(new PreliminaryDanceObservation(
                            competitionId,
                            date,
                            round,
                            Text(danceHeader),
                            Tooltip(judges[index].Header),
                            Text(judges[index].Header),
                            values[index],
                            panelMarks));
                    }
                }

                headerOffset += span;
                dataOffset += span;
            }
        }

        return observations;
    }

    private static bool IsCross(string value) => value is "+" or "*";

    private static bool IsTargetCouple(HtmlNode row, string athleteName, string? partnerName)
    {
        var coupleCell = row.SelectSingleNode("./td[contains(concat(' ', normalize-space(@class), ' '), ' number ')]");
        var names = coupleCell is null ? string.Empty : Tooltip(coupleCell);
        return names.Contains(athleteName, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(partnerName) || names.Contains(partnerName, StringComparison.OrdinalIgnoreCase));
    }

    private static string Text(HtmlNode node) =>
        WhitespaceRegex().Replace(WebUtility.HtmlDecode(node.InnerText), " ").Trim();

    private static string Tooltip(HtmlNode node) =>
        WebUtility.HtmlDecode(node.GetAttributeValue("title", node.GetAttributeValue("data-bs-original-title", string.Empty)));

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}