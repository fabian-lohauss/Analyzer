using WdsfAnalyzer.Core;

namespace WdsfAnalyzer.Tests;

public sealed class WdsfFinalParserTests
{
    [Fact]
    public void Relative_final_uses_achieved_dance_place_and_direction_convention()
    {
        const string html = """
            <h4>Waltz</h4><table class="table skating"><thead><tr>
            <th class="couple">Couple</th><th class="ajud" title="Judge One">A</th><th class="ajud" title="Judge Two">B</th><th class="place">Place</th>
            </tr></thead><tbody><tr><td class="number" title="Fabian Lohauss - Simone Braunschweig">273</td><td>1.</td><td>3.</td><td class="rankTotal">2</td></tr></tbody></table>
            """;

        var result = new WdsfFinalParser().Parse(html, "Fabian Lohauss", "Simone Braunschweig", "1", new DateOnly(2026, 7, 18));

        Assert.Collection(result,
            mark => Assert.Equal(1m, mark.Deviation),
            mark => Assert.Equal(-1m, mark.Deviation));
        Assert.All(result, mark => Assert.Equal("Waltz", mark.Dance));
    }

    [Fact]
    public void Absolute_final_uses_derived_rank_and_achieved_place()
    {
        const string html = """
            <h4>Waltz</h4><table class="table scores"><thead><tr>
            <th class="couple">Couple</th><th class="ajud" colspan="2" title="Judge One">A</th><th></th><th class="ajud" colspan="2" title="Judge Two">B</th><th></th><th class="place">Place</th>
            </tr></thead><tbody><tr><td class="number" title="Fabian Lohauss - Simone Braunschweig">222</td><td>5.800<br>1</td><td>5.800<br>1</td><td class="separator"></td><td>5.200<br>2</td><td>5.200<br>2</td><td class="separator"></td><td class="rankTotal">1</td></tr></tbody></table>
            """;

        var result = new WdsfScoresParser().Parse(html, "Fabian Lohauss", "Simone Braunschweig", "2", new DateOnly(2026, 3, 26));

        Assert.Collection(result,
            mark => { Assert.Equal(0m, mark.Deviation); Assert.Equal(1m, mark.DerivedRank); },
            mark => { Assert.Equal(-1m, mark.Deviation); Assert.Equal(2m, mark.DerivedRank); });
        Assert.All(result, mark => Assert.Equal(5.5m, mark.PanelAverage));
    }
}