using WdsfAnalyzer.Core;

namespace WdsfAnalyzer.Tests;

public sealed class WdsfMarksParserTests
{
    [Fact]
    public void Parse_returns_crosses_and_panel_count_per_round_and_dance()
    {
        const string html = """
            <table class="table marks"><thead>
            <tr><th colspan="3"></th><th colspan="3" class="adjudicator">Waltz</th><th colspan="3" class="adjudicator">Tango</th><th>Total</th></tr>
            <tr><th>Rank</th><th>Couple</th><th>Round</th><th class="ajud" title="Judge One">A</th><th class="ajud" title="Judge Two">B</th><th>=</th><th class="ajud" title="Judge One">A</th><th class="ajud" title="Judge Two">B</th><th>=</th><th>Total</th></tr>
            </thead><tbody><tr><td rowspan="1">1</td><td class="number" rowspan="1" title="Fabian Lohauss - Simone Braunschweig">222</td><td class="round" title="2. Round">2</td>
            <td data-info>+</td><td data-info></td><td class="dSum">1</td><td data-info>+</td><td data-info>+</td><td class="dSum">2</td><td>3</td></tr></tbody></table>
            """;

        var result = new WdsfMarksParser().Parse(html, "Fabian Lohauss", "Simone Braunschweig", "1", new DateOnly(2026, 1, 1));

        Assert.Equal(4, result.Count);
        Assert.Equal(1m, result.Single(mark => mark.Dance == "Waltz" && mark.JudgeName == "Judge One").PanelMarks);
        Assert.Equal(0m, result.Single(mark => mark.Dance == "Waltz" && mark.JudgeName == "Judge Two").RawValue);
        Assert.Equal(1m, result.Single(mark => mark.Dance == "Tango" && mark.JudgeName == "Judge Two").RawValue);
    }
}