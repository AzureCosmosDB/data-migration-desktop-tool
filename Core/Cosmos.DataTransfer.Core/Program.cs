using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Hosting;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Core;

class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Azure data migration tool") { TreatUnmatchedTokensAsErrors = false };
        rootCommand.AddCommand(new RunCommand());
        rootCommand.AddCommand(new ListCommand());
        rootCommand.AddCommand(new InitCommand());
        rootCommand.AddCommand(new SettingsCommand());

        // execute Run if no command provided
        RunCommand.AddRunOptions(rootCommand);
        rootCommand.SetHandler(async ctx =>
        {
            var host = ctx.GetHost();
            var logger = host.Services.GetService<ILoggerFactory>();
            var config = host.Services.GetService<IConfiguration>();
            var loader = host.Services.GetService<IExtensionLoader>();
            if (loader == null || config == null || logger == null)
            {
                ctx.Console.Error.WriteLine("Missing required command");
            }
            else
            {
                var handler = new RunCommand.CommandHandler(loader, config, logger)
                {
                    Source = ctx.BindingContext.ParseResult.GetValueForOption(rootCommand.Options.ElementAt(0)) as string,
                    Sink = ctx.BindingContext.ParseResult.GetValueForOption(rootCommand.Options.ElementAt(1)) as string,
                    Settings = ctx.BindingContext.ParseResult.GetValueForOption(rootCommand.Options.ElementAt(2)) as FileInfo
                };
                ctx.ExitCode = await handler.InvokeAsync(ctx);
            }
        });

        var cmdlineBuilder = new CommandLineBuilder(rootCommand);

        var parser = cmdlineBuilder.UseHost(_ => Host.CreateDefaultBuilder(args),
            builder =>
            {
                builder.ConfigureAppConfiguration((hostContext, cfg) =>
                {
                    var exeFolder = AppContext.BaseDirectory;
                    var appsettings = Path.Combine(exeFolder, "appsettings.json");
                    if (File.Exists(appsettings))
                    {
                        cfg.AddJsonFile(appsettings);
                    }
                    var appsettingsEnv = Path.Combine(exeFolder, $"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json");
                    if (File.Exists(appsettingsEnv))
                    {
                        cfg.AddJsonFile(appsettingsEnv);
                    }
                    cfg.AddUserSecrets<Program>();
                }).ConfigureServices((hostContext, services) =>
                {
                    services.AddTransient<IExtensionLoader, ExtensionLoader>();
                    services.AddTransient<IRawOutputWriter, ConsoleOutputWriter>();
                    services.AddTransient<IExtensionManifestBuilder, ExtensionManifestBuilder>();
                })
                    .UseCommandHandler<RunCommand, RunCommand.CommandHandler>()
                    .UseCommandHandler<ListCommand, ListCommand.CommandHandler>()
                    .UseCommandHandler<InitCommand, InitCommand.CommandHandler>()
                    .UseCommandHandler<SettingsCommand, SettingsCommand.CommandHandler>();
            })
            .UseHelp(AddAdditionalArgumentsHelp)
            .UseDefaults().Build();

        return await parser.InvokeAsync(args);
    }

    private static void AddAdditionalArgumentsHelp(HelpContext helpContext)
    {
        helpContext.HelpBuilder.CustomizeLayout(_ =>
        {
            var layout = HelpBuilder.Default.GetLayout().ToList();
            layout.Remove(HelpBuilder.Default.AdditionalArgumentsSection());
            bool runCommand = helpContext.Command.GetType() == typeof(RunCommand);
            bool rootCommand = helpContext.Command.GetType() == typeof(RootCommand);
            if (runCommand || rootCommand)
            {
                layout.Add(ctx =>
                {
                    if (rootCommand)
                    {
                        ctx.Output.WriteLine();
                    }

                    ctx.Output.WriteLine("Additional Arguments:");
                    ctx.Output.WriteLine("  Extension specific settings can be provided as additional arguments in the form:");
                    ctx.HelpBuilder.WriteColumns(new List<TwoColumnHelpRow> { new("--<Source|Sink>Settings:<name> <value>", "ex: --SourceSettings:FilePath MyDataFile.json") }.AsReadOnly(), ctx);
                });
            }

            return layout;
        });
    }
}
