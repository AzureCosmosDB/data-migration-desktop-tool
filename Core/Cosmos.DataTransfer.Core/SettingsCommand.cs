using System.CommandLine;
using System.CommandLine.Invocation;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmos.DataTransfer.Interfaces.Manifest;

namespace Cosmos.DataTransfer.Core
{
    public class SettingsCommand : Command
    {
        public SettingsCommand()
            : base("settings", "Loads and lists extension settings structure")
        {
            AddSettingsOptions(this);
        }

        public static void AddSettingsOptions(Command command)
        {
            var extensionOption = new Option<string?>(
                aliases: new[] { "--extension", "-e" },
                description: "The extension type to load.");

            var sourcesOption = new Option<bool?>(
                aliases: new[] { "--source" },
                description: "True to include source settings");

            var sinksOption = new Option<bool?>(
                aliases: new[] { "--sink" },
                description: "True to include sink settings");

            var outputOption = new Option<FileInfo?>(
                aliases: new[] { "--output", "-o" },
                description: "The output path to write a manifest file.");

            command.AddOption(extensionOption);
            command.AddOption(sourcesOption);
            command.AddOption(sinksOption);
            command.AddOption(outputOption);
        }

        public class CommandHandler : ICommandHandler
        {
            private readonly ILogger<CommandHandler> _logger;
            private readonly IExtensionManifestBuilder _manifestBuilder;
            private readonly IRawOutputWriter _writer;

            public string? Extension { get; set; }
            public bool? Source { get; set; }
            public bool? Sink { get; set; }
            public FileInfo? Output { get; set; }

            public CommandHandler(IExtensionManifestBuilder manifestBuilder, IRawOutputWriter rawOutputWriter, ILogger<CommandHandler> logger)
            {
                _logger = logger;
                _manifestBuilder = manifestBuilder;
                _writer = rawOutputWriter;
            }

            public int Invoke(InvocationContext context)
            {
                return InvokeAsync(context).GetAwaiter().GetResult();
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                if (Source.HasValue && Sink.HasValue || !(Source.HasValue || Sink.HasValue))
                {
                    throw new InvalidOperationException();
                }

                if (!string.IsNullOrWhiteSpace(Extension))
                {
                    if (Source == true)
                    {
                        var sources = _manifestBuilder.GetSources();
                        var selectedSource = sources.FirstOrDefault(s => string.Equals(s.DisplayName, Extension, StringComparison.CurrentCultureIgnoreCase));
                        if (selectedSource is IExtensionWithSettings extension)
                        {
                            var allProperties = _manifestBuilder.GetExtensionSettings(extension);

                            await WriteOutput(allProperties);
                        }
                    }
                    else if (Sink == true)
                    {
                        var sinks = _manifestBuilder.GetSinks();
                        var selectedSink = sinks.FirstOrDefault(s => string.Equals(s.DisplayName, Extension, StringComparison.CurrentCultureIgnoreCase));
                        if (selectedSink is IExtensionWithSettings extension)
                        {
                            var allProperties = _manifestBuilder.GetExtensionSettings(extension);

                            await WriteOutput(allProperties);
                        }
                    }
                }
                else
                {
                    var manifest = _manifestBuilder.BuildManifest(Source == true ? ExtensionDirection.Source : ExtensionDirection.Sink);
                    await WriteOutput(manifest);
                }

                return 0;
            }

            private async Task WriteOutput(object manifest)
            {
                var fullJson = JsonSerializer.Serialize(manifest, SerializerOptions);
                if (Output == null)
                {
                    _writer.WriteLine("<<<");
                    _writer.WriteLine(fullJson);
                    _writer.WriteLine(">>>");
                }
                else
                {
                    await File.WriteAllTextAsync(Output.FullName, fullJson);
                }
            }

            private static JsonSerializerOptions SerializerOptions => new()
            {
                Converters = { new JsonStringEnumConverter() },
                WriteIndented = true
            };
        }
    }

    public interface IRawOutputWriter
    {
        void WriteLine(string? value);
    }

    public class ConsoleOutputWriter : IRawOutputWriter
    {
        public void WriteLine(string? value)
        {
            Console.WriteLine(value);
        }
    }
}