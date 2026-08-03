using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using WdsfAnalyzer.Core;

namespace WdsfAnalyzer.Web.Pages;

public class IndexModel(IWdsfAnalysisSource analysisSource) : PageModel
{
    private const string DefaultMin = "10006615";

    [BindProperty]
    [Required(ErrorMessage = "Enter a WDSF MIN.")]
    [RegularExpression(@"^\d{8}$", ErrorMessage = "A MIN contains eight digits.")]
    public string Min { get; set; } = DefaultMin;

    [BindProperty]
    [Required]
    [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Select a coverage month.")]
    public string CoverageStart { get; set; } = DefaultCoverageStart();

    public CoupleAnalysis? Analysis { get; private set; }
    public string? LoadError { get; private set; }

    public void OnGet(string? min)
    {
        Min = min ?? DefaultMin;
        CoverageStart = DefaultCoverageStart();
    }

    public async Task<IActionResult> OnPostAsync(bool refresh = false, CancellationToken cancellationToken = default)
    {
        if (!TryParseCoverageStart(out var coverageStart) || !ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            Analysis = await analysisSource.LoadAsync(Min, coverageStart, refresh, cancellationToken);
        }
        catch (Exception exception) when (exception is WdsfDataException or HttpRequestException or TaskCanceledException)
        {
            LoadError = exception is TaskCanceledException
                ? "WDSF did not respond in time. Try again shortly."
                : exception.Message;
        }

        return Page();
    }

    public async Task<IActionResult> OnGetCellDetailAsync(
        string min,
        string coverageStart,
        string judgeName,
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseCoverageStart(coverageStart, out var parsedCoverage))
        {
            return BadRequest();
        }

        var analysis = await analysisSource.LoadAsync(min, parsedCoverage, false, cancellationToken);
        var competition = analysis.Competitions.FirstOrDefault(item => item.Id == competitionId);
        var judge = analysis.Judges?.FirstOrDefault(item => item.Name.Equals(judgeName, StringComparison.OrdinalIgnoreCase));
        if (competition is null || judge is null || !judge.CompetitionValues.TryGetValue(competitionId, out var value))
        {
            return NotFound();
        }

        return Partial("_CellDetail", new CellDetailViewModel(judge.Name, competition, value));
    }

    public string MaximumCoverageMonth => DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static string DefaultCoverageStart() =>
        new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddYears(-2).ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private bool TryParseCoverageStart(out DateOnly coverageStart)
    {
        if (TryParseCoverageStart(CoverageStart, out coverageStart))
        {
            return true;
        }

        ModelState.AddModelError(nameof(CoverageStart), "Select a valid coverage month.");
        coverageStart = default;
        return false;
    }

    private static bool TryParseCoverageStart(string value, out DateOnly coverageStart)
    {
        var maximum = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        if (DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) && parsed <= maximum)
        {
            coverageStart = DateOnly.FromDateTime(parsed);
            return true;
        }

        coverageStart = default;
        return false;
    }
}

public sealed record CellDetailViewModel(
    string JudgeName,
    CompetitionEntry Competition,
    JudgeCompetitionValue Value);
