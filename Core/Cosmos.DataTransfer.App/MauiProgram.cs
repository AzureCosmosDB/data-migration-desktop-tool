using System.Reflection;
using BlazorState;
using Microsoft.Extensions.Logging;
using Cosmos.DataTransfer.App.Data;
using Cosmos.DataTransfer.Ui;

namespace Cosmos.DataTransfer.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

        var appSettings = new AppSettings
        {
            CoreAppPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "dmt.exe")
        };

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
		appSettings.CoreAppPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..\\..\\..\\..\\..\\..\\..", "Core\\Cosmos.DataTransfer.Core", "bin\\Debug\\net6.0\\dmt.exe"));
#endif

        builder.Services.AddSingleton(appSettings);
		
        builder.Services.AddSingleton<IAppDataService, AppDataService>();
        builder.Services.AddBlazorState(o =>
        {
			o.Assemblies = new[] { typeof(MauiProgram).GetTypeInfo().Assembly };
        });

        return builder.Build();
	}
}
