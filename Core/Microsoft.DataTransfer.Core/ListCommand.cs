using Microsoft.DataTransfer.Interfaces;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel.Composition.Hosting;

namespace Microsoft.DataTransfer.Core
{
    public class ListCommand : Command
    {
        public ListCommand()
            : base("list", "Loads and lists all available extensions")
        {
        }

        public class CommandHandler : ICommandHandler
        {
            private readonly ILogger<CommandHandler> _logger;
            private readonly ExtensionLoader _extensionLoader;

            public CommandHandler(ExtensionLoader extensionLoader, ILogger<CommandHandler> logger)
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

                Console.WriteLine($"{sources.Count} Source Extensions");
                foreach (var extension in sources)
                {
                    Console.WriteLine($"\t{extension.DisplayName}");
                }

                Console.WriteLine($"{sinks.Count} Sink Extensions");
                foreach (var extension in sinks)
                {
                    Console.WriteLine($"\t{extension.DisplayName}");
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