using Microsoft.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel.Composition.Hosting;

namespace Microsoft.DataTransfer.Core
{
    public class RunCommand : Command
    {
        public RunCommand()
            : base("run", "Runs data transfer operation using selected source and sink")
        {
            var sourceOption = new Option<string?>(
                aliases: new[]{ "--source", "-from" },
                description: "The extension to read data.");
            var sourceSettingsOption = new Option<FileInfo?>(
                aliases: new[] { "--source-settings" },
                description: "The source settings file.");
            var sinkOption = new Option<string?>(
                aliases: new[] { "--sink", "-to" },
                description: "The extension to write data.");
            var sinkSettingsOption = new Option<FileInfo?>(
                aliases: new[] { "--sink-settings" },
                description: "The sink settings file.");

            AddOption(sourceOption);
            AddOption(sourceSettingsOption);
            AddOption(sinkOption);
            AddOption(sinkSettingsOption);

            TreatUnmatchedTokensAsErrors = false;

            // TODO: load extensions to use in completions
            //sourceOption.AddCompletions(ExtensionLoader.GetExtensionSourceNames());
            //sinkOption.AddCompletions(ExtensionLoader.GetExtensionSinkNames());
        }

        public class CommandHandler : ICommandHandler
        {
            private readonly ILogger<CommandHandler> _logger;
            private readonly ExtensionLoader _extensionLoader;
            private readonly IConfiguration _configuration;
            private readonly ILoggerFactory _loggerFactory;

            public string? Source { get; set; }
            public string? Sink { get; set; }
            public FileInfo? SourceSettings { get; set; }
            public FileInfo? SinkSettings { get; set; }

            public CommandHandler(ExtensionLoader extensionLoader, IConfiguration configuration, ILoggerFactory loggerFactory)
            {
                _logger = loggerFactory.CreateLogger<CommandHandler>();
                _extensionLoader = extensionLoader;
                _configuration = configuration;
                _loggerFactory = loggerFactory;
            }

            public int Invoke(InvocationContext context)
            {
                return InvokeAsync(context).GetAwaiter().GetResult();
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var configuredOptions = _configuration.Get<DataTransferOptions>();
                var options = new DataTransferOptions
                {
                    Source = Source ?? configuredOptions.Source,
                    Sink = Sink ?? configuredOptions.Sink,
                    SinkSettingsPath = SinkSettings?.FullName ?? configuredOptions.SinkSettingsPath,
                    SourceSettingsPath = SourceSettings?.FullName ?? configuredOptions.SourceSettingsPath,
                };

                string extensionsPath = _extensionLoader.GetExtensionFolderPath();
                CompositionContainer container = _extensionLoader.BuildExtensionCatalog(extensionsPath);

                var sources = _extensionLoader.LoadExtensions<IDataSourceExtension>(container);
                var sinks = _extensionLoader.LoadExtensions<IDataSinkExtension>(container);

                var source = GetExtensionSelection(options.Source, sources, "Source");
                var sourceConfig = BuildSettingsConfiguration(_configuration, options.SourceSettingsPath, $"{source.DisplayName}SourceSettings", options.Source == null);
                _logger.LogDebug("Loaded {SettingCount} settings for source {SourceName}:\n\t\t{SettingList}", 
                    sourceConfig.AsEnumerable().Count(), 
                    source.DisplayName, 
                    string.Join("\n\t\t", sourceConfig.AsEnumerable().Select(kvp => kvp.Key)));

                var sink = GetExtensionSelection(options.Sink, sinks, "Sink");
                var sinkConfig = BuildSettingsConfiguration(_configuration, options.SinkSettingsPath, $"{sink.DisplayName}SinkSettings", options.Sink == null);
                _logger.LogDebug("Loaded {SettingCount} settings for source {SinkName}:\n\t\t{SettingsList}", 
                    sinkConfig.AsEnumerable().Count(), 
                    sink.DisplayName,
                    string.Join("\n\t\t", sinkConfig.AsEnumerable().Select(kvp => kvp.Key)));

                var data = source.ReadAsync(sourceConfig, _loggerFactory.CreateLogger(source.GetType().Name));
                await sink.WriteAsync(data, sinkConfig, source, _loggerFactory.CreateLogger(sink.GetType().Name));

                _logger.LogInformation("Done");

                return 0;
            }

            private static T GetExtensionSelection<T>(string? selectionName, List<T> extensions, string inputPrompt)
                where T : class, IDataTransferExtension
            {
                if (!string.IsNullOrWhiteSpace(selectionName))
                {
                    var extension = extensions.FirstOrDefault(s => selectionName.Equals(s.DisplayName, StringComparison.OrdinalIgnoreCase));
                    if (extension != null)
                    {
                        Console.WriteLine($"Using {extension.DisplayName} {inputPrompt}");
                        return extension;
                    }
                }

                Console.WriteLine($"Select {inputPrompt}");
                for (var index = 0; index < extensions.Count; index++)
                {
                    var extension = extensions[index];
                    Console.WriteLine($"{index + 1}:{extension.DisplayName}");
                }

                string? selection = "";
                int input;
                while (!int.TryParse(selection, out input) || input > extensions.Count)
                {
                    selection = Console.ReadLine();
                }

                T selected = extensions[input - 1];
                Console.WriteLine($"Using {selected.DisplayName} {inputPrompt}");
                return selected;
            }

            private static IConfiguration BuildSettingsConfiguration(IConfiguration configuration, string? settingsPath, string configSection, bool promptForFile)
            {
                IConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
                if (!string.IsNullOrEmpty(settingsPath))
                {
                    configurationBuilder = configurationBuilder.AddJsonFile(settingsPath);
                }
                else if (promptForFile)
                {
                    Console.Write($"Load settings from a file? (y/n):");
                    var response = Console.ReadLine();
                    if (IsYesResponse(response))
                    {
                        Console.Write("Path to file: ");
                        var path = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            configurationBuilder = configurationBuilder.AddJsonFile(path);
                        }
                    }
                    else
                    {
                        Console.Write($"Configuration section to read settings? (default={configSection}):");
                        response = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(response))
                        {
                            configSection = response;
                        }
                    }
                }

                return configurationBuilder
                    .AddConfiguration(configuration.GetSection(configSection))
                    .Build();
            }

            private static bool IsYesResponse(string? response)
            {
                if (response?.Equals("y", StringComparison.CurrentCultureIgnoreCase) == true)
                    return true;
                if (response?.Equals("yes", StringComparison.CurrentCultureIgnoreCase) == true)
                    return true;

                return false;
            }
        }
    }
}