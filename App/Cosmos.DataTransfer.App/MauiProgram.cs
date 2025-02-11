using System.Reflection;
using BlazorState;
using Microsoft.Extensions.Logging;
using Cosmos.DataTransfer.App.Data;
using Cosmos.DataTransfer.Ui.Common;

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

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        string dmtAppPath = DmtUtility.GetDmtAppPath(platformCoreAppName);

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

}
