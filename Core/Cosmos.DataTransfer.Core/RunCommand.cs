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
                    var settingsPath = Settings?.FullName ?? configuredOptions.SettingsPath;
                    
                    // Check if settings file exists when no source/sink provided via command line
                    if (string.IsNullOrEmpty(Source) && string.IsNullOrEmpty(Sink))
                    {
                        var defaultSettingsPath = settingsPath ?? "migrationsettings.json";
                        if (!File.Exists(defaultSettingsPath))
                        {
                            Console.Error.WriteLine($"Error: Settings file '{defaultSettingsPath}' not found.");
                            Console.Error.WriteLine("Please provide a valid settings file or specify --source and --sink options.");
                            Console.Error.WriteLine();
                            Console.Error.WriteLine("Use --help for more information.");
                            return 1;
                        }
                    }

                    var combinedConfig = await BuildSettingsConfiguration(_configuration,
                        settingsPath,
                        cancellationToken);

                    var options = combinedConfig.Get<DataTransferOptions>();

                    // Validate that we have source and sink configured
                    var sourceValue = Source ?? options?.Source;
                    var sinkValue = Sink ?? options?.Sink;
                    
                    if (string.IsNullOrWhiteSpace(sourceValue) || string.IsNullOrWhiteSpace(sinkValue))
                    {
                        Console.Error.WriteLine("Error: Invalid configuration. Both Source and Sink must be specified.");
                        if (!string.IsNullOrEmpty(settingsPath) && File.Exists(settingsPath))
                        {
                            Console.Error.WriteLine($"The settings file '{settingsPath}' is missing required Source or Sink values.");
                        }
                        Console.Error.WriteLine("Please provide valid Source and Sink in the settings file or via command line arguments.");
                        Console.Error.WriteLine();
                        Console.Error.WriteLine("Use --help for more information.");
                        return 1;
                    }

                    string extensionsPath = _extensionLoader.GetExtensionFolderPath();
                    CompositionContainer container = _extensionLoader.BuildExtensionCatalog(extensionsPath);

                    var sources = _extensionLoader.LoadExtensions<IDataSourceExtension>(container);
                    var sinks = _extensionLoader.LoadExtensions<IDataSinkExtension>(container);

                    cancellationToken.ThrowIfCancellationRequested();

                    var source = await GetExtensionSelection(sourceValue, sources, "Source", cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    var sink = await GetExtensionSelection(sinkValue, sinks, "Sink", cancellationToken);
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

                // Validate that source and sink don't point to the same container when RecreateContainer is enabled
                ValidateSourceAndSinkNotSameWithRecreate(source, sourceConfig, sink, sinkConfig);

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

            private void ValidateSourceAndSinkNotSameWithRecreate(
                IDataSourceExtension source, 
                IConfiguration sourceConfig, 
                IDataSinkExtension sink, 
                IConfiguration sinkConfig)
            {
                // Only validate if both source and sink are Cosmos-nosql extensions
                const string cosmosExtensionName = "Cosmos-nosql";
                if (!source.DisplayName.Equals(cosmosExtensionName, StringComparison.OrdinalIgnoreCase) ||
                    !sink.DisplayName.Equals(cosmosExtensionName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // Check if RecreateContainer is enabled
                var recreateContainer = sinkConfig.GetValue<bool>("RecreateContainer");
                if (!recreateContainer)
                {
                    return;
                }

                // Compare connection details
                var sourceConnectionString = sourceConfig.GetValue<string>("ConnectionString");
                var sinkConnectionString = sinkConfig.GetValue<string>("ConnectionString");
                var sourceAccountEndpoint = sourceConfig.GetValue<string>("AccountEndpoint");
                var sinkAccountEndpoint = sinkConfig.GetValue<string>("AccountEndpoint");
                var sourceDatabase = sourceConfig.GetValue<string>("Database");
                var sinkDatabase = sinkConfig.GetValue<string>("Database");
                var sourceContainer = sourceConfig.GetValue<string>("Container");
                var sinkContainer = sinkConfig.GetValue<string>("Container");

                // Normalize account endpoints for comparison
                string? sourceAccount = GetAccountFromConnectionOrEndpoint(sourceConnectionString, sourceAccountEndpoint);
                string? sinkAccount = GetAccountFromConnectionOrEndpoint(sinkConnectionString, sinkAccountEndpoint);

                // Check if they point to the same container
                bool sameAccount = !string.IsNullOrEmpty(sourceAccount) && 
                                   !string.IsNullOrEmpty(sinkAccount) &&
                                   sourceAccount.Equals(sinkAccount, StringComparison.OrdinalIgnoreCase);
                bool sameDatabase = !string.IsNullOrEmpty(sourceDatabase) && 
                                    !string.IsNullOrEmpty(sinkDatabase) &&
                                    sourceDatabase.Equals(sinkDatabase, StringComparison.OrdinalIgnoreCase);
                bool sameContainer = !string.IsNullOrEmpty(sourceContainer) && 
                                     !string.IsNullOrEmpty(sinkContainer) &&
                                     sourceContainer.Equals(sinkContainer, StringComparison.OrdinalIgnoreCase);

                if (sameAccount && sameDatabase && sameContainer)
                {
                    var errorMessage = $"Invalid configuration: Source and Sink are configured to use the same Cosmos DB container " +
                                       $"(Database: '{sourceDatabase}', Container: '{sourceContainer}') with RecreateContainer enabled. " +
                                       $"This would delete the source container before the data transfer begins, resulting in data loss. " +
                                       $"Please use different containers for Source and Sink, or disable RecreateContainer in the Sink settings.";
                    _logger.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }
            }

            private static string? GetAccountFromConnectionOrEndpoint(string? connectionString, string? accountEndpoint)
            {
                if (!string.IsNullOrEmpty(accountEndpoint))
                {
                    // Normalize the endpoint URL
                    return NormalizeAccountEndpoint(accountEndpoint);
                }

                if (!string.IsNullOrEmpty(connectionString))
                {
                    // Extract AccountEndpoint from connection string
                    var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        if (part.Trim().StartsWith("AccountEndpoint=", StringComparison.OrdinalIgnoreCase))
                        {
                            var endpoint = part.Substring(part.IndexOf('=') + 1).Trim();
                            return NormalizeAccountEndpoint(endpoint);
                        }
                    }
                }

                return null;
            }

            private static string NormalizeAccountEndpoint(string endpoint)
            {
                // Remove trailing slashes and convert to lowercase for comparison
                return endpoint.TrimEnd('/').ToLowerInvariant();
            }

            private static async Task<T> GetExtensionSelection<T>(string? selectionName, List<T> extensions, string inputPrompt, CancellationToken cancellationToken)
                where T : class, IDataTransferExtension
            {
                await Task.CompletedTask; // Maintain async signature for compatibility
                cancellationToken.ThrowIfCancellationRequested();
                
                if (string.IsNullOrWhiteSpace(selectionName))
                {
                    throw new InvalidOperationException($"{inputPrompt} extension name is required. Use --source and --sink options or configure them in the settings file.");
                }

                var extension = extensions.FirstOrDefault(s => s.MatchesExtensionSelection(selectionName));
                if (extension == null)
                {
                    throw new InvalidOperationException($"{inputPrompt} extension '{selectionName}' not found. Use 'dmt list' to see available extensions.");
                }
                
                Console.WriteLine($"Using {extension.DisplayName} {inputPrompt}");
                return extension;
            }

            private async Task<IConfiguration> BuildSettingsConfiguration(IConfiguration configuration, string? settingsPath, CancellationToken cancellationToken)
            {
                await Task.CompletedTask; // Maintain async signature for compatibility
                cancellationToken.ThrowIfCancellationRequested();
                
                IConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
                if (!string.IsNullOrEmpty(settingsPath) && File.Exists(settingsPath))
                {
                    var fullFilePath = Path.GetFullPath(settingsPath);
                    _logger.LogInformation("Settings loading from file at configured path '{FilePath}'.", fullFilePath);
                    configurationBuilder = configurationBuilder.AddJsonFile(fullFilePath);
                }

                return configurationBuilder
                    .AddConfiguration(configuration)
                    .Build();
            }
        }
    }
}