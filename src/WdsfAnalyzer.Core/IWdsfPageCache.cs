namespace WdsfAnalyzer.Core;

public interface IWdsfPageCache
{
    Task<string?> GetAsync(Uri url, CancellationToken cancellationToken = default);

    Task SetAsync(Uri url, string content, CancellationToken cancellationToken = default);
}