using System.Reflection;
using BlazorState;
using Microsoft.Extensions.Logging;
using Cosmos.DataTransfer.App.Data;
using Cosmos.DataTransfer.Ui;

namespace Cosmos.DataTransfer.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp(string platformCoreAppName = "dmt")
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        string? executionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string? searchDir = FindParentWithContents(executionDir, "Extensions");

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();

        searchDir = FindParentWithContents(executionDir, "Core", ".git");
#endif
        var dmtAppPath = FindPreferredCoreAppPath(searchDir, platformCoreAppName);

        var appSettings = new AppSettings
        {
            CoreAppPath = dmtAppPath
        };

        builder.Services.AddSingleton(appSettings);

        builder.Services.AddSingleton<IAppDataService, AppDataService>();
        builder.Services.AddBlazorState(o =>
        {
            o.Assemblies = new[] { typeof(MauiProgram).GetTypeInfo().Assembly };
        });

        return builder.Build();
    }

    private static string? FindParentWithContents(string? executionDir, params string[] markers)
    {
        string? searchDir = executionDir;
        while (searchDir != null && !markers.Any(m => Directory.Exists(Path.Combine(searchDir, m))))
        {
            searchDir = Path.GetDirectoryName(searchDir);
        }

        if (string.IsNullOrEmpty(searchDir))
        {
            return executionDir;
        }

        return searchDir;
    }

    private static string FindPreferredCoreAppPath(string? rootSearchFolder, string dmtFileName)
    {
        var dir = new DirectoryInfo(rootSearchFolder ?? Environment.CurrentDirectory);

        var fileList = dir.EnumerateFiles("*.*", SearchOption.AllDirectories);

        var candidates = fileList.Where(file => file.Name == dmtFileName).ToList();

        if (candidates.Count == 1)
            return candidates.Single().FullName;

        var preferred = candidates.Where(file => file.DirectoryName != null && new DirectoryInfo(file.DirectoryName).EnumerateDirectories().Any(d => d.Name == "Extensions")).ToList();
        if (preferred.Count == 1)
            return preferred.Single().FullName;

        preferred = (preferred.Any() ? preferred : candidates).Where(file => file.DirectoryName?.Contains("bin\\Debug\\net6.0") == true).ToList();
        if (preferred.Count == 1)
            return preferred.Single().FullName;

        return (preferred.FirstOrDefault() ?? candidates.FirstOrDefault())?.FullName ?? Path.Combine(dir.FullName, dmtFileName);
    }
}
