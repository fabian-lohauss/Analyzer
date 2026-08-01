using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace WdsfAnalyzer.Core;

public sealed partial class WdsfFinalParser
{
    public IReadOnlyList<RelativeFinalObservation> Parse(
        string html,
        string athleteName,
        string? partnerName,
        string competitionId,
        DateOnly date)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var observations = new List<RelativeFinalObservation>();

        foreach (var table in document.DocumentNode.SelectNodes("//table[contains(concat(' ', normalize-space(@class), ' '), ' skating ')]") ?? Enumerable.Empty<HtmlNode>())
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
            var columnOffset = 0;
            foreach (var header in headerRow.SelectNodes("./th") ?? Enumerable.Empty<HtmlNode>())
            {
                var span = header.GetAttributeValue("colspan", 1);
                if (header.GetClasses().Contains("ajud") && columnOffset < cells.Count &&
                    TryNumber(Text(cells[columnOffset]), out var judgeRank))
                {
                    observations.Add(new RelativeFinalObservation(
                        competitionId,
                        date,
                        dance,
                        Tooltip(header),
                        Text(header),
                        judgeRank,
                        achievedPlace,
                        achievedPlace - judgeRank,
                        finalistCount));
                }

                columnOffset += span;
            }
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

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}