using Cosmos.DataTransfer.Ui.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Cosmos.DataTransfer.App.Windows;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public new static App Current => (App)Application.Current;

    public App()
    {
        Services = ConfigureServices();
    }

    public AppSettings? Settings => Services.GetService<AppSettings>();

    public IServiceProvider Services { get; }

    /// <summary>
    /// Configures the services for the application.
    /// </summary>
    private static IServiceProvider ConfigureServices()
    {
        string dmtAppPath = DmtUtility.GetDmtAppPath("dmt.exe");

        var settings = new AppSettings
        {
            CoreAppPath = dmtAppPath
        };

        var services = new ServiceCollection();

        services.AddSingleton(settings);

        services.AddSingleton(new LogViewModel());

        return services.BuildServiceProvider();
    }
}
