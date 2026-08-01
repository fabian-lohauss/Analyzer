using WdsfAnalyzer.Core;

namespace WdsfAnalyzer.Tests;

public sealed class WdsfProfileParserTests
{
    [Fact]
    public void Parse_normalizes_profile_and_filters_competitions()
    {
        const string html = """
            <main><h1>Clemens Kalmer</h1>
            <dl><dt>Member Id number (MIN)</dt><dd>10004805</dd><dt>Current age group</dt><dd>Senior IIIb</dd><dt>Represents</dt><dd>Germany</dd></dl>
            <table><tr><td>info</td><td>Petra Kalmer</td><td>Germany</td><td>Germany</td><td>Active</td><td>19/10/2002</td><td></td></tr></table>
            <table>
              <tr><td>53.</td><td>10</td><td>3 July 2026</td><td><a href="/Competitions/Ranking/Open-Wuppertal-Senior-III-Standard-64793">Open</a></td><td>Standard</td><td>Senior III</td><td>Wuppertal, Germany</td></tr>
              <tr><td>Registered</td><td></td><td>15 August 2026</td><td><a href="/Competitions/Ranking/Open-Stuttgart-Senior-III-Latin-67563">Open</a></td><td>Latin</td><td>Senior III</td><td>Stuttgart, Germany</td></tr>
              <tr><td>1.</td><td>10</td><td>1 July 2023</td><td><a href="/Competitions/Ranking/Open-Test-1">Open</a></td><td>Standard</td><td>Senior III</td><td>Test</td></tr>
            </table></main>
            """;

        var result = new WdsfProfileParser().Parse(
            html,
            new Uri("https://www.worlddancesport.org/Athletes/Clemens-Kalmer-1b6f0824-ae43-4038-90a8-9e1401202f91"),
            "10004805",
            false);

        Assert.Equal("Clemens Kalmer", result.Athlete.Name);
        Assert.Equal("Petra Kalmer", result.Partnership?.PartnerName);
        var competition = Assert.Single(result.Competitions);
        Assert.Equal(53, competition.Placement);
        Assert.Equal("64793", competition.Id);
        Assert.Contains("/Competitions/Results/", competition.ResultsUrl.AbsoluteUri);
        Assert.Contains("/Competitions/Marks/", competition.MarksUrl.AbsoluteUri);
        Assert.Equal(2, result.ExcludedCompetitionCount);
    }

    [Fact]
    public void Parse_rejects_a_profile_for_another_min()
    {
        const string html = "<main><h1>Someone</h1><dl><dt>Member Id number (MIN)</dt><dd>10004805</dd></dl></main>";

        Assert.Throws<WdsfDataException>(() => new WdsfProfileParser().Parse(
            html,
            new Uri("https://www.worlddancesport.org/Athletes/Someone-1b6f0824-ae43-4038-90a8-9e1401202f91"),
            "99999999",
            false));
    }
}