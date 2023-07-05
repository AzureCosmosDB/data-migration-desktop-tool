using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel.Composition.Hosting;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Core
{
    public class RunCommand : Command
    {
        public RunCommand()
            : base("run", "Runs data transfer operation using selected source and sink")
        {
            AddRunOptions(this);

            AddAlias("<default>");

            TreatUnmatchedTokensAsErrors = false;

            // TODO: load extensions to use in completions
            //sourceOption.AddCompletions(ExtensionLoader.GetExtensionSourceNames());
            //sinkOption.AddCompletions(ExtensionLoader.GetExtensionSinkNames());
        }

        public static void AddRunOptions(Command command)
        {
            var sourceOption = new Option<string?>(
                aliases: new[] { "--source", "-from" },
                description: "The extension to read data.")
            {
                ArgumentHelpName = "source"
            };
            var sinkOption = new Option<string?>(
                aliases: new[] { "--sink", "-to", "--target", "--destination" },
                description: "The extension to write data.")
            {
                ArgumentHelpName = "sink"
            };
            var settingsOption = new Option<FileInfo?>(
                aliases: new[] { "--settings" },
                description: "The settings file. (default: migrationsettings.json)");

            command.AddOption(sourceOption);
            command.AddOption(sinkOption);
            command.AddOption(settingsOption);
        }

        public class CommandHandler : ICommandHandler
        {
            private readonly ILogger<CommandHandler> _logger;
            private readonly IExtensionLoader _extensionLoader;
            private readonly IConfiguration _configuration;
            private readonly ILoggerFactory _loggerFactory;

            public string? Source { get; set; }
            public string? Sink { get; set; }
            public FileInfo? Settings { get; set; }

            public CommandHandler(IExtensionLoader extensionLoader, IConfiguration configuration, ILoggerFactory loggerFactory)
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
                CancellationToken cancellationToken = context.GetCancellationToken();

                try
                {
                    var configuredOptions = _configuration.Get<DataTransferOptions>() ?? new DataTransferOptions();
                    var combinedConfig = await BuildSettingsConfiguration(_configuration,
                        Settings?.FullName ?? configuredOptions.SettingsPath,
                        string.IsNullOrEmpty(Source ?? configuredOptions.Source) && string.IsNullOrEmpty(Sink ?? configuredOptions.Sink),
                        cancellationToken);

                    var options = combinedConfig.Get<DataTransferOptions>();

                    string extensionsPath = _extensionLoader.GetExtensionFolderPath();
                    CompositionContainer container = _extensionLoader.BuildExtensionCatalog(extensionsPath);

                    var sources = _extensionLoader.LoadExtensions<IDataSourceExtension>(container);
                    var sinks = _extensionLoader.LoadExtensions<IDataSinkExtension>(container);

                    cancellationToken.ThrowIfCancellationRequested();

                    var source = await GetExtensionSelection(Source ?? options.Source, sources, "Source", cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    var sink = await GetExtensionSelection(Sink ?? options.Sink, sinks, "Sink", cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    var sourceConfig = combinedConfig.GetSection("SourceSettings");
                    var sinkConfig = GetSinkConfig(combinedConfig);
                    var operationConfigs = combinedConfig.GetSection("Operations");
                    var operations = operationConfigs?.GetChildren().ToList();
                    bool succeeded = true;
                    if (operations?.Any() == true)
                    {
                        foreach (var operationConfig in operations)
                        {
                            var operationSource = operationConfig.GetSection("SourceSettings");
                            var sourceBuilder = new ConfigurationBuilder().AddConfiguration(sourceConfig);
                            if (operationSource.Exists())
                            {
                                sourceBuilder.AddConfiguration(operationSource);
                            }
                            var operationSink = GetSinkConfig(operationConfig);
                            var sinkBuilder = new ConfigurationBuilder().AddConfiguration(sinkConfig);
                            if (operationSink.Exists())
                            {
                                sinkBuilder.AddConfiguration(operationSink);
                            }
                            succeeded &= await ExecuteDataTransferOperation(source,
                                      sourceBuilder.Build(),
                                      sink,
                                      sinkBuilder.Build(),
                                      cancellationToken);
                        }
                    }
                    else
                    {
                        succeeded = await ExecuteDataTransferOperation(source, sourceConfig, sink, sinkConfig, cancellationToken);
                    }

                    return succeeded ? 0 : 1;
                }
                catch (OperationCanceledException ex)
                {
                    _logger.LogDebug(ex, "Operation canceled.");
                    Console.WriteLine();
                    Console.WriteLine("Operation canceled. Exiting.");
                    return 1;
                }
            }

            private static IConfigurationSection GetSinkConfig(IConfiguration combinedConfig)
            {
                var config = combinedConfig.GetSection("SinkSettings");
                if (config != null && config.Exists())
                {
                    return config;
                }

                config = combinedConfig.GetSection("TargetSettings");
                if (config != null && config.Exists())
                {
                    return config;
                }

                config = combinedConfig.GetSection("DestinationSettings");
                return config;
            }

            private async Task<bool> ExecuteDataTransferOperation(IDataSourceExtension source, IConfiguration sourceConfig, IDataSinkExtension sink, IConfiguration sinkConfig, CancellationToken cancellationToken)
            {
                _logger.LogDebug("Loaded {SettingCount} settings for source {SourceName}:\n\t\t{SettingList}",
                    sourceConfig.AsEnumerable().Count(),
                    source.DisplayName,
                    string.Join("\n\t\t", sourceConfig.AsEnumerable().Select(kvp => kvp.Key)));

                _logger.LogDebug("Loaded {SettingCount} settings for sink {SinkName}:\n\t\t{SettingsList}",
                    sinkConfig.AsEnumerable().Count(),
                    sink.DisplayName,
                    string.Join("\n\t\t", sinkConfig.AsEnumerable().Select(kvp => kvp.Key)));

                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var data = source.ReadAsync(sourceConfig, _loggerFactory.CreateLogger(source.GetType().Name), cancellationToken);
                    await sink.WriteAsync(data, sinkConfig, source, _loggerFactory.CreateLogger(sink.GetType().Name), cancellationToken);

                    _logger.LogInformation("Data transfer complete");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Data transfer failed");
                    return false;
                }
            }

            private static async Task<T> GetExtensionSelection<T>(string? selectionName, List<T> extensions, string inputPrompt, CancellationToken cancellationToken)
                where T : class, IDataTransferExtension
            {
                if (!string.IsNullOrWhiteSpace(selectionName))
                {
                    var extension = extensions.FirstOrDefault(s => s.MatchesExtensionSelection(selectionName));
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
                while (!int.TryParse(selection, out input) || input > extensions.Count || input <= 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    selection = await Console.In.ReadLineAsync(cancellationToken);
                }

                T selected = extensions[input - 1];
                Console.WriteLine($"Using {selected.DisplayName} {inputPrompt}");
                return selected;
            }

            private async Task<IConfiguration> BuildSettingsConfiguration(IConfiguration configuration, string? settingsPath, bool promptForFile, CancellationToken cancellationToken)
            {
                IConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
                if (!string.IsNullOrEmpty(settingsPath) && File.Exists(settingsPath))
                {
                    var fullFilePath = Path.GetFullPath(settingsPath);
                    _logger.LogInformation("Settings loading from file at configured path '{FilePath}'.", fullFilePath);
                    configurationBuilder = configurationBuilder.AddJsonFile(fullFilePath);
                }
                else if (promptForFile)
                {
                    Console.Write("Path to settings file? (leave empty to skip): ");
                    var path = await Console.In.ReadLineAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        var fullFilePath = Path.GetFullPath(path);
                        _logger.LogInformation("Settings loading from file at entered path '{FilePath}'.", fullFilePath);
                        configurationBuilder = configurationBuilder.AddJsonFile(fullFilePath);
                    }
                }

                return configurationBuilder
                    .AddConfiguration(configuration)
                    .Build();
            }
        }
    }
}