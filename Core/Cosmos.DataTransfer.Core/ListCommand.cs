using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel.Composition.Hosting;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Core
{
    public class ListCommand : Command
    {
        public ListCommand()
            : base("list", "Loads and lists all available extensions")
        {
            AddListOptions(this);
        }

        public static void AddListOptions(Command command)
        {
            var sourcesOption = new Option<bool?>(
                aliases: new[] { "--sources" },
                description: "True to include source names");

            var sinksOption = new Option<bool?>(
                aliases: new[] { "--sinks" },
                description: "True to include sink names");

            command.AddOption(sourcesOption);
            command.AddOption(sinksOption);
        }

        public class CommandHandler : ICommandHandler
        {
            private readonly ILogger<CommandHandler> _logger;
            private readonly IExtensionLoader _extensionLoader;

            public bool? Sources { get; set; }
            public bool? Sinks { get; set; }

            public CommandHandler(IExtensionLoader extensionLoader, ILogger<CommandHandler> logger)
            {
                _logger = logger;
                _extensionLoader = extensionLoader;
            }

            public int Invoke(InvocationContext context)
            {
                string extensionsPath = _extensionLoader.GetExtensionFolderPath();
                CompositionContainer container = _extensionLoader.BuildExtensionCatalog(extensionsPath);

                var sources = _extensionLoader.LoadExtensions<IDataSourceExtension>(container);
                var sinks = _extensionLoader.LoadExtensions<IDataSinkExtension>(container);

                bool showSources = Sources ?? (Sources == null && Sinks == null);
                bool showSinks = Sinks ?? (Sources == null && Sinks == null);

                if (showSources)
                {
                    Console.WriteLine($"{sources.Count} Source Extensions");
                    foreach (var extension in sources)
                    {
                        Console.WriteLine($"\t{extension.DisplayName}");
                    }
                }

                if (showSinks)
                {
                    Console.WriteLine($"{sinks.Count} Sink Extensions");
                    foreach (var extension in sinks)
                    {
                        Console.WriteLine($"\t{extension.DisplayName}");
                    }
                }

                return 0;
            }

            public Task<int> InvokeAsync(InvocationContext context)
            {
                return Task.FromResult(Invoke(context));
            }
        }
    }
}