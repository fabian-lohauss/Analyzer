namespace WdsfAnalyzer.Core;

public interface IWdsfAnalysisCache
{
    Task<CoupleAnalysis?> GetAsync(string min, CancellationToken cancellationToken = default);

    Task SetAsync(CoupleAnalysis analysis, CancellationToken cancellationToken = default);
}