namespace WdsfAnalyzer.Core;

public interface IWdsfAnalysisSource
{
    Task<CoupleAnalysis> LoadAsync(string min, DateOnly coverageStart, bool refresh, CancellationToken cancellationToken = default);
}