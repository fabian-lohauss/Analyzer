using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using WdsfAnalyzer.Core;

namespace WdsfAnalyzer.Web.Pages;

public class IndexModel(IWdsfAnalysisSource analysisSource) : PageModel
{
    private const string DefaultMin = "10006615";

    [BindProperty]
    [Required(ErrorMessage = "Enter a WDSF MIN.")]
    [RegularExpression(@"^\d{8}$", ErrorMessage = "A MIN contains eight digits.")]
    public string Min { get; set; } = DefaultMin;

    public CoupleAnalysis? Analysis { get; private set; }
    public string? LoadError { get; private set; }

    public void OnGet(string? min)
    {
        Min = min ?? DefaultMin;
    }

    public async Task<IActionResult> OnPostAsync(bool refresh = false, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            Analysis = await analysisSource.LoadAsync(Min, refresh, cancellationToken);
        }
        catch (Exception exception) when (exception is WdsfDataException or HttpRequestException or TaskCanceledException)
        {
            LoadError = exception is TaskCanceledException
                ? "WDSF did not respond in time. Try again shortly."
                : exception.Message;
        }

        return Page();
    }
}
