using System.Net;
using Azure.Identity;
using Azure.Storage.Blobs;
using WdsfAnalyzer.Core;
using WdsfAnalyzer.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<WdsfProfileParser>();
builder.Services.AddSingleton<WdsfResultsParser>();
builder.Services.AddSingleton<WdsfMarksParser>();
builder.Services.AddSingleton<WdsfFinalParser>();
builder.Services.AddSingleton<WdsfScoresParser>();
builder.Services.AddSingleton<WdsfCoupleParser>();
builder.Services.AddSingleton<IWdsfPageCache>(services =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    if (Uri.TryCreate(configuration["Storage:ServiceUri"], UriKind.Absolute, out var serviceUri))
    {
        return new BlobWdsfPageCache(serviceUri, configuration["Storage:ContainerName"] ?? "cache");
    }

    var environment = services.GetRequiredService<IWebHostEnvironment>();
    return new FileWdsfPageCache(Path.Combine(environment.ContentRootPath, "App_Data", "cache"));
});
builder.Services.AddSingleton<IWdsfAnalysisCache>(services =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    if (Uri.TryCreate(configuration["Storage:ServiceUri"], UriKind.Absolute, out var serviceUri))
    {
        var containerName = configuration["Storage:ContainerName"] ?? "cache";
        var container = new BlobServiceClient(serviceUri, new DefaultAzureCredential()).GetBlobContainerClient(containerName);
        return new BlobWdsfAnalysisCache(container);
    }

    var environment = services.GetRequiredService<IWebHostEnvironment>();
    return new FileWdsfAnalysisCache(Path.Combine(environment.ContentRootPath, "App_Data", "cache"));
});
builder.Services.AddSingleton<WdsfAnalysisSource>(services =>
{
    var handler = new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        CookieContainer = new CookieContainer()
    };
    var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WdsfJudgeDeviationAnalyzer/0.1 (+local analytical tool)");
    return new WdsfAnalysisSource(
        client,
        services.GetRequiredService<WdsfProfileParser>(),
        services.GetRequiredService<WdsfResultsParser>(),
        services.GetRequiredService<WdsfMarksParser>(),
        services.GetRequiredService<WdsfFinalParser>(),
        services.GetRequiredService<WdsfScoresParser>(),
        services.GetRequiredService<WdsfCoupleParser>(),
        services.GetRequiredService<IWdsfPageCache>());
});
builder.Services.AddSingleton<IWdsfAnalysisSource>(services =>
    new CachedWdsfAnalysisSource(
        services.GetRequiredService<WdsfAnalysisSource>(),
        services.GetRequiredService<IWdsfAnalysisCache>()));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
