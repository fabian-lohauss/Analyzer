using System.Net;
using WdsfAnalyzer.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSingleton<WdsfProfileParser>();
builder.Services.AddSingleton<WdsfResultsParser>();
builder.Services.AddSingleton<WdsfMarksParser>();
builder.Services.AddSingleton<WdsfFinalParser>();
builder.Services.AddSingleton<WdsfScoresParser>();
builder.Services.AddSingleton<IWdsfAnalysisSource>(services =>
{
    var handler = new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        CookieContainer = new CookieContainer()
    };
    var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WdsfJudgeDeviationAnalyzer/0.1 (+local analytical tool)");
    var environment = services.GetRequiredService<IWebHostEnvironment>();
    return new WdsfAnalysisSource(
        client,
        services.GetRequiredService<WdsfProfileParser>(),
        services.GetRequiredService<WdsfResultsParser>(),
        services.GetRequiredService<WdsfMarksParser>(),
        services.GetRequiredService<WdsfFinalParser>(),
        services.GetRequiredService<WdsfScoresParser>(),
        Path.Combine(environment.ContentRootPath, "App_Data", "cache"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
