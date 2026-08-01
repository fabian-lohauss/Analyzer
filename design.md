# WDSF Judge Deviation Analyzer

## Purpose

The WDSF Judge Deviation Analyzer is a web application for analyzing how individual adjudicators scored a dance couple across official WDSF competitions.

The application focuses on detecting upward and downward deviations in adjudicator scores compared with the adjudicator field. It is designed for long-term analysis across all relevant competitions of the couple, not for manually inspecting a single event.

The initial scope is limited to official WDSF results from 1 January 2024 onward.

## Core Use Case

A user enters the MIN of one partner. The application resolves the current WDSF partnership for that athlete, loads the official couple competition history, filters all completed competitions from 2024 onward, and analyzes the official results and final skating reports.

The result is a judge-focused view showing which adjudicators scored the couple above, below, or close to the field across competitions, rounds, and dances.

## Primary Requirements

- Use official WDSF web pages as the data source.
- Use the MIN of one partner as the application entry point.
- Resolve only the current partnership for the entered MIN.
- Analyze competitions from `2024-01-01` onward.
- Exclude competitions with non-result statuses such as `Registered` and `Excused`.
- Analyze multiple competitions over months or years.
- Distinguish preliminary round scoring from final round scoring.
- Cache downloaded WDSF pages to avoid unnecessary repeated requests.
- Prepare the application for hosting as an Azure Web App.
- Implement the application in C#.

## Technology Stack

The recommended implementation stack is:

- C# with ASP.NET Core
- Razor Pages or Blazor Server for the web UI
- HtmlAgilityPack for HTML parsing
- Azure App Service for hosting
- Azure Blob Storage for cached raw HTML and processed JSON data
- CsvHelper for optional CSV export

For the first MVP, local file-based JSON caching is acceptable. Azure Blob Storage should be introduced before or during cloud deployment.

## High-Level Architecture

```text
ASP.NET Core Web App
	Pages / UI
	API endpoints
	Application services
	WDSF HTML parsers
	Judge analysis engine
	Cache storage

Official WDSF website
	Athlete / profile pages
	Couple pages
	Competition ranking pages
	Competition results pages
	Final skating pages

Azure
	Azure App Service
	Azure Blob Storage
```

## Data Flow

```text
1. User enters a partner MIN.
2. The app resolves the WDSF athlete page for that MIN.
3. The app identifies the athlete's current partnership.
4. The app opens the official WDSF couple page for that partnership.
5. The app extracts all competition links from the couple page.
6. The app filters competitions to dates from 2024-01-01 onward.
7. The app excludes Registered and Excused entries.
8. For each remaining competition, the app derives official Results and Final URLs.
9. The app also derives the official Scores URL for competitions that expose absolute final scores.
10. The app downloads or loads cached HTML for those pages.
11. The app extracts the couple's start number and judge marks.
12. The app resolves each adjudicator to a canonical WDSF official identity.
13. The app detects the scoring mode for each round.
14. The app calculates preliminary support and final deviations per judge, round, dance, and competition.
15. The app aggregates the results into judge summaries and detail views.
```

## WDSF URL Model

The WDSF couple page contains competition links in this form:

```text
/Competitions/Ranking/Open-Vienna-Senior-III-Standard-66039
```

The corresponding official result pages can be derived by replacing the route segment:

```text
/Competitions/Results/Open-Vienna-Senior-III-Standard-66039
/Competitions/Final/Open-Vienna-Senior-III-Standard-66039
/Competitions/Scores/Open-Vienna-Senior-III-Standard-66039
```

The application should store the original ranking URL and the derived results, final, and scores URLs for traceability. The `Scores` URL is needed for rare finals that use absolute scoring instead of only relative final placements.

## Judge Identity Normalization

WDSF publishes an official directory at:

```text
https://www.worlddancesport.org/Officials
```

Each official has a profile URL whose final slug contains a stable UUID, for example:

```text
/Officials/Ara-Mkhoyan-183e4e98-21a4-4058-8959-9e14011f0da9
```

The UUID from the official profile URL should be used as the canonical `JudgeId`. Names, diacritics, initials, spelling, country, and event panel letters must not be used as the primary identity because they may vary between pages or over time.

The application should preserve two levels of judge data:

- Canonical official data from the WDSF officials directory: `JudgeId`, canonical name, current country code, and profile URL.
- Competition-specific source data: name exactly as published, panel letter, country code, and source URL.

The panel letter is an assignment within a competition, not part of the official's identity. A judge may have different letters in different competitions.

Judge resolution should follow this order:

1. Use a linked WDSF official profile and extract its UUID when the results page provides one.
2. Otherwise, match the source name and country against a cached snapshot of the WDSF officials directory.
3. Automatically accept a fallback match only when it has exactly one candidate.
4. Keep ambiguous or unmatched officials as unresolved records; do not merge them automatically.

Directory snapshots and resolution outcomes should be cached. Every judge mark should retain its original source values so a resolution can be audited or rerun when directory data or matching rules change.

## Competition Filtering

Only competitions matching all of the following conditions are analyzed:

- The competition date is on or after `2024-01-01`.
- The couple has a completed result.
- The status is not `Registered`.
- The status is not `Excused`.
- The official WDSF results page is available.

The application may still keep excluded competitions in metadata for transparency, but they must not affect judge statistics.

## Scoring Model

The app must treat preliminary rounds and final rounds differently because they represent different judging systems.

### Preliminary Rounds

In preliminary rounds, the Results page supplies each judge's round total. The separate Marks page supplies the underlying binary cross per judge, round, and dance. Per-dance details show the panel's total crosses followed by `+` when the adjudicator awarded a mark, `-` when no mark was awarded, or `/` when the dance mark is unavailable.

Only the latest published preliminary round contributes to P for each competition. For finalists this is the semifinal; for non-finalists it is the last round reached. Average P gives each competition equal weight. Earlier rounds remain visible as detail but do not contribute to P.

Overall support is the raw `40% P + 60% F` weighted average, reweighted over whichever stages are available. Confidence is displayed separately using `n / (n + 3)` for each available stage with the same stage weights. Thus perfect P and F display as 100% support even for one competition, alongside 25% confidence.

The default adjudicator order remains confidence-aware. P and F are each shrunk toward zero by `n / (n + 3)` and percentile-ranked independently. Each percentile retains that confidence factor before the stages are combined as `0.4 × adjusted P percentile + 0.6 × adjusted F percentile`. This ranking value is not displayed as support. The UI also supports direct sorting by Final, Preliminary, and Coverage.

For preliminary rounds, P is the percentage of the possible dance marks awarded to the couple:

```text
preliminarySupport = awardedDanceMarks / availableDanceMarks * 100
```

Each adjudicator can award one mark per dance and must distribute a limited quota across the field. Therefore 100% means the adjudicator awarded the couple every available mark, while 0% means none. When dance-level Marks are unavailable, the published round total is divided by the five dances as a fallback.

### Relative Final Rounds

In normal finals, F normalizes each judge's placement across the size of the final and adjusts it by 50% of the normalized deviation from the achieved dance place:

```text
rankSupport  = (finalistCount - judgeRank) / (finalistCount - 1) * 100
rankDeviation = achievedDancePlace - judgeRank
finalSupport = clamp(rankSupport + 0.5 * 100 * rankDeviation / (finalistCount - 1), 0, 100)
```

Unanimous first place is therefore 100% support. A rank better than the achieved place raises F, while a worse rank lowers it. The app also displays the raw deviation because WDSF final results are determined by majority rules rather than a simple arithmetic mean:

For relative final rounds:

```text
rankDeviation = achievedDancePlace - judgeRank
```

A positive value means the judge placed the couple better than the achieved dance result. A negative value means the judge placed the couple worse than the achieved dance result.

The app should also calculate majority support:

```text
majoritySupport = judgeRank <= achievedDancePlace
```

This indicates whether the judge supported the majority required for the couple's achieved place or better.

### Absolute Score Finals

Rarely, a final exposes absolute scores instead of only relative placements. An example is the WDSF Open Standard Senior III in Blackpool on 28 March 2026, available through a `Scores` URL such as:

```text
/Competitions/Scores/Open-Blackpool-Senior-III-Standard-65660
```

These pages may contain an absolute decimal score and a derived rank per judge, dance, and couple. The app should detect this scoring mode from the `Scores` page and store both values where available.

For absolute score finals, F should apply the same final-support formula to the published derived rank so it remains comparable with other final formats. The app should also retain its achieved-place deviation:

```text
rankDeviation = achievedDancePlace - derivedJudgeRank
```

A positive value means the judge ranked the couple more favorably than its achieved place. A negative value means the judge ranked it less favorably.

The app should keep the raw absolute scores as secondary detail only because score scales and components are not reliably comparable between events:

```text
majoritySupport = derivedJudgeRank <= achievedDancePlace
```

Absolute score finals and relative final pages can be aggregated through their normalized F percentages because both use the published judge rank. Raw absolute scores must remain separate.

P and F are distinct support percentages and must remain separate top-level summary values. The stage-aware Overall index combines only their confidence-adjusted percentiles, not their raw values.

Final summaries should show average F as the primary value and average achieved-place deviation as `Δ`. When two adjudicators have equal F, the more favorable `Δ` is the secondary final-sort signal.

### Direction Convention

All displayed deviations should follow the same user-facing convention:

```text
positive = more favorable for the couple
negative = less favorable for the couple
zero     = aligned with the comparison value
```

This convention must be preserved even though preliminary rounds, relative finals, and absolute score finals use different raw scoring systems.

## Aggregations

The app should aggregate judge deviations at several levels:

- Judge across all competitions
- Judge per competition
- Judge per dance
- Judge per round type
- Judge per age class
- Judge per competition type

Recommended summary metrics include:

- Number of competitions observed
- Number of dances observed
- Average preliminary support percentage
- Average final support percentage
- Average relative final rank deviation
- Final majority support rate
- Count of favorable marks
- Count of unfavorable marks
- Largest positive deviation
- Largest negative deviation

Judge summaries should expose preliminary and final values separately. Both final page formats use the published judge rank:

```text
preliminarySupportSummary = average preliminary support percentage
finalSupportSummary       = average deviation-weighted normalized final-rank support
finalDeviationSummary     = average achieved-place deviation (secondary)
```

Raw absolute scores must not be averaged across events.

### Main Judge Matrix Display

The primary judge overview should be a matrix with one row per adjudicator. Competition columns should be ordered by competition date.

Recommended fixed columns:

- Adjudicator
- Preliminary summary
- Final summary
- Observation count

Each competition cell should display both stage values in a fixed order, with F first:

```text
F 58%  Δ +1.40
P 80%

If no final score exists:

F —
P 60%
```

The dash means no final evidence, not zero support. Competition-column sorting uses F when available and falls back to P when F is unavailable.

The visible cell value should include a compact scoring-mode marker:

```text
P = preliminary support percentage
F = deviation-weighted final support percentage from either final page format
Δ = final rank deviation from the achieved dance place
```

For example:

```text
P 80%
F 100% · Δ +0.00
```

Every competition cell shows F first and P second. If no final was reached, F displays `-`; hover or click exposes the full competition breakdown for that adjudicator.

Cell details use rounds as rows in reverse progression order, beginning with the final, and dances as columns:

- Final: achieved dance place followed by the adjudicator's rank in parentheses
- Preliminary: panel-average round total followed by the adjudicator's round total in parentheses
- Preliminary per dance: panel cross count followed by `+`, `-`, or `/` for awarded, not awarded, or unavailable

The detail surface does not repeat the calculated deviation. Hover provides a quick desktop preview; click, keyboard activation, or touch pins the detail until dismissed. On narrow screens it becomes a bottom sheet with horizontal table scrolling.

This keeps the main table compact while still allowing inspection of cases where preliminary and final behavior differ in the same competition.

The app should require a minimum observation count before highlighting a judge as notable. A sensible initial threshold is at least three observed competitions or five observed dances.

## Suggested Data Models

```csharp
public sealed record Athlete(
		string Min,
		string Name,
		Uri ProfileUrl
);

public sealed record CurrentPartnership(
		string CoupleId,
		string Partner1Name,
		string Partner2Name,
		string Discipline,
		string AgeClass,
		Uri CoupleUrl
);

public sealed record Competition(
		string Id,
		DateOnly Date,
		string Type,
		string Discipline,
		string AgeClass,
		string Location,
		int? Placement,
		int? Points,
		Uri RankingUrl,
		Uri ResultsUrl,
		Uri FinalUrl,
		Uri ScoresUrl
);

public sealed record OfficialJudge(
		Guid JudgeId,
		string CanonicalName,
		string CountryCode,
		Uri ProfileUrl
);

public sealed record JudgeAssignment(
		string CompetitionId,
		Guid? JudgeId,
		string SourceName,
		string PanelLetter,
		string? SourceCountryCode,
		Uri SourceUrl,
		JudgeResolutionStatus ResolutionStatus
);

public sealed record JudgeMark(
		string CompetitionId,
		DateOnly Date,
		string Round,
		string Dance,
		Guid? JudgeId,
		string PanelLetter,
		string CoupleNumber,
		decimal RawValue,
		decimal? DerivedRank,
		decimal Deviation,
		JudgeMarkKind Kind
);

public enum JudgeMarkKind
{
		PreliminaryScore,
		FinalRank,
		AbsoluteFinalScore
}

public enum ScoringMode
{
		Preliminary,
		RelativeFinal,
		AbsoluteScoreFinal
}

public enum JudgeResolutionStatus
{
		ResolvedByProfileId,
		ResolvedByUniqueDirectoryMatch,
		Unresolved,
		Ambiguous
}
```

## Core Services

```text
IWdsfAthleteService
	Resolves an athlete by MIN and finds the current partnership.

IWdsfCompetitionService
	Loads and parses the couple competition history.

IWdsfPageClient
	Downloads WDSF pages and applies rate limiting and caching.

IWdsfOfficialDirectoryService
	Loads official profiles and resolves source judge data to canonical WDSF JudgeIds.

IResultsParser
	Parses preliminary and results pages.

IFinalParser
	Parses final skating pages.

IScoresParser
	Parses absolute score pages when WDSF exposes a Scores URL.

IJudgeAnalysisService
	Calculates deviations and aggregate judge statistics.

ICacheStore
	Stores raw HTML and processed JSON data locally or in Azure Blob Storage.
```

## Caching Strategy

The app should avoid downloading WDSF pages on every user request.

Recommended cache layers:

1. Raw HTML cache for official WDSF pages.
2. Parsed official directory and judge resolution cache.
3. Parsed competition metadata cache.
4. Parsed judge mark cache.
5. Computed analysis result cache.

Older competitions should be treated as mostly immutable. Current-season competitions may be refreshed more often.

Suggested default behavior:

- Use cached data for normal dashboard views.
- Provide a manual refresh button.
- Refresh missing pages automatically.
- Avoid frequent repeated requests to WDSF.

## Azure Hosting

The target hosting model is Azure App Service.

Recommended Azure resources:

- Azure App Service for the ASP.NET Core application
- Azure Blob Storage for cached HTML and JSON data
- Application Insights for diagnostics
- GitHub Actions or Azure DevOps for deployment

Suggested configuration values:

```json
{
	"Wdsf": {
		"BaseUrl": "https://www.worlddancesport.org"
	},
	"Analysis": {
		"StartDate": "2024-01-01",
		"UseCurrentPartnershipOnly": true,
		"MinimumCompetitionsForHighlight": 3,
		"MinimumDancesForHighlight": 5
	},
	"Cache": {
		"UseAzureBlobStorage": false,
		"DefaultTtlHours": 168
	}
}
```

## MVP Scope

The first usable version should include:

- MIN input field
- Current partnership resolution
- Competition extraction from the official WDSF couple page
- Filtering from `2024-01-01` onward
- Exclusion of `Registered` and `Excused` competitions
- Results page parsing
- Final page parsing where available
- Scores page parsing for rare absolute-score finals
- Judge identity resolution against the WDSF officials directory
- Judge deviation calculation
- Judge summary table
- Competition detail table
- Manual refresh button
- Local JSON cache

## Later Enhancements

Potential future improvements include:

- Azure Blob Storage cache
- Authentication for private use
- CSV and Excel export
- Judge heatmaps by dance and competition
- Trend charts over time
- Comparison between preliminary and final behavior
- Configurable start date
- Support for analyzing other couples by MIN
- Automated scheduled refresh
- Application Insights dashboards

## Interpretation Notes

The app should present preliminary support and final deviations as analytical signals, not as proof of bias or misconduct. DanceSport judging involves subjective evaluation, event context, and majority rules. The application should make official WDSF source links available for every competition so that any highlighted result can be reviewed in context.

The preferred wording in the UI should be neutral, for example:

- `Awarded 80% of available preliminary marks`
- `Placed better than achieved dance result`
- `Placed worse than achieved dance result`
- `Higher than panel average absolute score`
- `Lower than panel average absolute score`
- `Supported majority for achieved place`

## Open Questions

- Which exact WDSF page pattern should be used to resolve an athlete profile from a MIN?
- How reliably does the WDSF profile page expose the current partnership in raw HTML?
- Are judge names always available alongside judge letters on all relevant pages?
- Do competition result pages link judge names directly to WDSF official profiles, or is directory matching required for some page formats?
- Should final majority support be shown per dance only, or also aggregated into a judge-level support rate?
- How often do relevant WDSF finals expose absolute scores, and should the Scores URL be probed for every competition or only when the Final page indicates score-based judging?
