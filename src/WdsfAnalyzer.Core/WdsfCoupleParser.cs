using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace WdsfAnalyzer.Core;

public sealed partial class WdsfCoupleParser
{
    public Partnership Parse(string html, Partnership partnership)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var manMin = Min(document, "Man");
        var ladyMin = Min(document, "Woman");
        if (manMin is null || ladyMin is null)
        {
            throw new WdsfDataException("The WDSF couple profile has no canonical MIN pair.");
        }

        return partnership with { ManMin = manMin, LadyMin = ladyMin };
    }

    private static string? Min(HtmlDocument document, string term)
    {
        var termNode = (document.DocumentNode.SelectNodes("//dt") ?? Enumerable.Empty<HtmlNode>())
            .FirstOrDefault(node => node.InnerText.Trim().Equals(term, StringComparison.OrdinalIgnoreCase));
        var value = termNode?.SelectSingleNode("following-sibling::dd[1]")?.InnerText;
        var match = MinRegex().Match(value ?? string.Empty);
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"\b\d{8}\b")]
    private static partial Regex MinRegex();
}