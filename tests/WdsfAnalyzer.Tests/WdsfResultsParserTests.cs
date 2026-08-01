using WdsfAnalyzer.Core;

namespace WdsfAnalyzer.Tests;

public sealed class WdsfResultsParserTests
{
    [Fact]
    public void Parse_calculates_favorable_direction_from_round_panel_average()
    {
        const string html = """
            <table class="js-results table results"><thead>
              <tr><th colspan="2"></th><th colspan="4" class="adjudicator">1. Round</th><th colspan="4">2. Round</th></tr>
              <tr><th>Rank</th><th>Couple</th>
                <th title="Adjudicator: Judge One">A</th><th title="Adjudicator: Judge Two">B</th><th title="Adjudicator: Judge Three">C</th><th class="dSum">=</th>
                <th title="Adjudicator: Judge One">A</th><th title="Adjudicator: Judge Two">B</th><th title="Adjudicator: Judge Three">C</th><th class="dSum">=</th><th></th>
              </tr></thead><tbody>
              <tr data-couple-number="273"><td class="rank" title="Fabian Lohauss - Simone Braunschweig&lt;br&gt;Germany">4</td><td>273</td><td>5</td><td>4</td><td>3</td><td class="dSum">12</td><td>4</td><td>4</td><td>4</td><td class="dSum">12</td></tr>
            </tbody></table>
            """;

        var result = new WdsfResultsParser().Parse(
            html, "Fabian Lohauss", "Simone Braunschweig", "66039", new DateOnly(2026, 7, 18));

        Assert.Equal(6, result.Count);
        Assert.Equal(1m, result.Single(item => item.Round == "1. Round" && item.PanelLetter == "A").Deviation);
        Assert.Equal(-1m, result.Single(item => item.Round == "1. Round" && item.PanelLetter == "C").Deviation);
        Assert.All(result.Where(item => item.Round == "2. Round"), item => Assert.Equal(0m, item.Deviation));
    }
}