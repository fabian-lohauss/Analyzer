using System.Security.Cryptography;
using System.Text;

namespace WdsfAnalyzer.Core;

public sealed class FileWdsfPageCache(string cacheDirectory) : IWdsfPageCache
{
    public async Task<string?> GetAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var path = GetPath(url);
        return File.Exists(path)
            ? await File.ReadAllTextAsync(path, cancellationToken)
            : null;
    }

    public async Task SetAsync(Uri url, string content, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(cacheDirectory);
        await File.WriteAllTextAsync(GetPath(url), content, cancellationToken);
    }

    private string GetPath(Uri url)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url.AbsoluteUri)));
        return Path.Combine(cacheDirectory, $"{key}.html");
    }
}