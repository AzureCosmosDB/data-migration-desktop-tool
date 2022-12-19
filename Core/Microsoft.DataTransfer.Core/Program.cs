using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using System.CommandLine.Help;

namespace Microsoft.DataTransfer.Core;

class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Azure data migration tool") { TreatUnmatchedTokensAsErrors = false };
        rootCommand.AddCommand(new RunCommand());
        rootCommand.AddCommand(new ListCommand());

        var cmdlineBuilder = new CommandLineBuilder(rootCommand);

        var parser = cmdlineBuilder.UseHost(_ => Host.CreateDefaultBuilder(args),
            builder =>
            {
                builder.ConfigureAppConfiguration(cfg =>
                {
                    cfg.AddUserSecrets<Program>();
                }).ConfigureServices((hostContext, services) =>
                {
                    services.AddTransient<ExtensionLoader>();
                })
                    .UseCommandHandler<RunCommand, RunCommand.CommandHandler>()
                    .UseCommandHandler<ListCommand, ListCommand.CommandHandler>();
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
            if (helpContext.Command.GetType() == typeof(RunCommand))
            {
                layout.Add(ctx =>
                {
                    ctx.Output.WriteLine("Additional Arguments:");
                    ctx.Output.WriteLine("  Extension specific settings can be provided as additional arguments in the form:");
                    ctx.HelpBuilder.WriteColumns(new List<TwoColumnHelpRow> { new("--<extension><Source|Sink>Settings:<name> <value>", "ex: --JsonSourceSettings:FilePath MyDataFile.json") }.AsReadOnly(), ctx);
                });
            }

            return layout;
        });
    }
}
