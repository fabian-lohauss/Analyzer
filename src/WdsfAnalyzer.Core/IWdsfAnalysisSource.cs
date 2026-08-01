namespace WdsfAnalyzer.Core;

public interface IWdsfAnalysisSource
{
    Task<CoupleAnalysis> LoadAsync(string min, bool refresh, CancellationToken cancellationToken = default);
}